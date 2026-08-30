using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using FluentAssertions;
using Microsoft.IdentityModel.Tokens;

namespace OficinaMecanica.Seguranca.Integration.Tests;

// Testes de integracao "de ponta a ponta" de verdade - sem fakes/mocks em nenhuma camada. A
// FunctionHostFixture sobe um host real do Azure Functions (func start), um Azurite real e usa o
// LocalDB real - esses testes batem via HttpClient nesse host, exatamente como um cliente externo
// bateria em producao (so trocando Key Vault por RSA local via KeyVault:Uri ausente).
[Collection(FunctionHostCollection.Name)]
public class AutenticacaoFluxoTests
{
    private readonly HttpClient _client;

    public AutenticacaoFluxoTests(FunctionHostFixture fixture)
    {
        _client = fixture.Client;
    }

    [Fact]
    public async Task Login_ComCredenciaisValidas_DeveRetornarTokenBearer()
    {
        var response = await _client.PostAsJsonAsync("/login", new
        {
            cpf = FunctionHostFixture.SeedAdminCpf,
            senha = FunctionHostFixture.SeedAdminSenhaInicial
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var corpo = await response.Content.ReadFromJsonAsync<JsonElement>();
        corpo.GetProperty("token").GetString().Should().NotBeNullOrWhiteSpace();
        corpo.GetProperty("tipo").GetString().Should().Be("Bearer");
        corpo.GetProperty("expiracaoMinutos").GetInt32().Should().Be(FunctionHostFixture.JwtExpiracaoMinutos);
    }

    [Fact]
    public async Task Login_ComSenhaIncorreta_DeveRetornar401()
    {
        var response = await _client.PostAsJsonAsync("/login", new
        {
            cpf = FunctionHostFixture.SeedAdminCpf,
            senha = "senha-completamente-errada"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Jwks_DeveRetornarPeloMenosUmaChaveRsaPublica()
    {
        var response = await _client.GetAsync("/.well-known/jwks.json");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var corpo = await response.Content.ReadFromJsonAsync<JsonElement>();
        var keys = corpo.GetProperty("keys");

        keys.GetArrayLength().Should().BeGreaterThanOrEqualTo(1);

        var primeiraChave = keys[0];
        primeiraChave.GetProperty("kty").GetString().Should().Be("RSA");
        primeiraChave.GetProperty("n").GetString().Should().NotBeNullOrWhiteSpace();
        primeiraChave.GetProperty("e").GetString().Should().NotBeNullOrWhiteSpace();
    }

    // Esse e o teste que realmente prova que o ciclo RS256 + JWKS funciona de ponta a ponta: pego o
    // token emitido pelo /login (assinado pela chave PRIVADA, que nunca sai do processo da Function)
    // e valido a assinatura dele usando SO a chave PUBLICA exposta pelo /.well-known/jwks.json -
    // exatamente como um resource server (ex: a API principal) faria pra validar o token sem
    // confiar cegamente nele. Se ValidateToken nao lancar excecao, a chave publica do JWKS bate
    // matematicamente com a chave privada usada pra assinar - nao ha como "forjar" esse resultado
    // com mocks, so um par de chaves RSA real e consistente entre os dois endpoints passa aqui.
    [Fact]
    public async Task TokenEmitidoNoLogin_DeveSerValidadoComChavePublicaExpostaNoJwks()
    {
        var loginResponse = await _client.PostAsJsonAsync("/login", new
        {
            cpf = FunctionHostFixture.SeedAdminCpf,
            senha = FunctionHostFixture.SeedAdminSenhaInicial
        });
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var loginCorpo = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        var token = loginCorpo.GetProperty("token").GetString()!;

        var jwksResponse = await _client.GetAsync("/.well-known/jwks.json");
        jwksResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var jwksCorpo = await jwksResponse.Content.ReadFromJsonAsync<JsonElement>();
        var primeiraChave = jwksCorpo.GetProperty("keys")[0];

        // JWKS representa Modulus/Exponent em Base64Url (RFC 7517) - preciso decodificar de volta
        // pra bytes brutos antes de montar o RSAParameters que o .NET entende.
        var modulus = Base64UrlEncoder.DecodeBytes(primeiraChave.GetProperty("n").GetString());
        var expoente = Base64UrlEncoder.DecodeBytes(primeiraChave.GetProperty("e").GetString());

        using var rsa = RSA.Create(new RSAParameters { Modulus = modulus, Exponent = expoente });
        var chavePublica = new RsaSecurityKey(rsa);

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = FunctionHostFixture.JwtIssuer,
            ValidateAudience = true,
            ValidAudience = FunctionHostFixture.JwtAudience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = chavePublica
        };

        var handler = new JwtSecurityTokenHandler();

        // Se a assinatura nao bater com a chave publica do JWKS (ou o token estiver expirado/com
        // issuer/audience errados), isso lanca SecurityTokenException e o teste falha - nao preciso
        // de nenhum Assert explicito alem de "nao lancou".
        var acao = () => handler.ValidateToken(token, validationParameters, out _);
        acao.Should().NotThrow();

        handler.ValidateToken(token, validationParameters, out var securityToken);
        var jwt = (JwtSecurityToken)securityToken;
        jwt.Claims.Should().Contain(c => c.Type == "name" && c.Value == FunctionHostFixture.SeedAdminCpf);
    }
}
