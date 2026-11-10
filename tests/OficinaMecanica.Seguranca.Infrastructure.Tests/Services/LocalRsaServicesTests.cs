using System.Security.Cryptography;
using FluentAssertions;
using Microsoft.IdentityModel.Tokens;
using OficinaMecanica.Seguranca.Domain.Enums;
using OficinaMecanica.Seguranca.Infrastructure.Services;
using OficinaMecanica.Seguranca.Infrastructure.Tests.Support;
using OficinaMecanica.Seguranca.Tests.Common.Builders;

namespace OficinaMecanica.Seguranca.Infrastructure.Tests.Services;

public sealed class LocalRsaServicesTests : IDisposable
{
    private readonly RSA _rsa = RSA.Create(2048);

    public void Dispose() => _rsa.Dispose();

    [Fact]
    public void ExpiracaoMinutos_DeveDevolverOValorConfigurado()
    {
        var servico = new LocalRsaTokenService(_rsa, 45, "emissor", "publico");

        servico.ExpiracaoMinutos.Should().Be(45);
    }

    [Fact]
    public async Task GerarTokenAsync_DeveEmitirJwtRs256ComOsClaimsDoUsuario()
    {
        var usuario = new UsuarioBuilder().ComCpf("52998224725").ComPerfil(PerfilUsuario.Admin).Build();
        var servico = new LocalRsaTokenService(_rsa, 30, "emissor-teste", "publico-teste");
        var antes = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var token = JwtDeTeste.Ler(await servico.GerarTokenAsync(usuario));

        token.Cabecalho.GetProperty("alg").GetString().Should().Be("RS256");
        token.Cabecalho.GetProperty("typ").GetString().Should().Be("JWT");
        token.Cabecalho.GetProperty("kid").GetString().Should().Be("local-dev");
        token.Corpo.GetProperty("sub").GetString().Should().Be(usuario.Id.ToString());
        token.Corpo.GetProperty("name").GetString().Should().Be("52998224725");
        token.Corpo.GetProperty("role").GetString().Should().Be("Admin");
        token.Corpo.GetProperty("iss").GetString().Should().Be("emissor-teste");
        token.Corpo.GetProperty("aud").GetString().Should().Be("publico-teste");
        Guid.TryParse(token.Corpo.GetProperty("jti").GetString(), out _).Should().BeTrue();

        var iat = token.Corpo.GetProperty("iat").GetInt64();
        var exp = token.Corpo.GetProperty("exp").GetInt64();
        iat.Should().BeGreaterThanOrEqualTo(antes);
        (exp - iat).Should().Be(30 * 60);
    }

    [Fact]
    public async Task GerarTokenAsync_DeveAssinarComAChavePrivadaECorrespondenteChavePublica()
    {
        var servico = new LocalRsaTokenService(_rsa, 60, "emissor", "publico");

        var token = JwtDeTeste.Ler(await servico.GerarTokenAsync(new UsuarioBuilder().Build()));

        token.AssinaturaConfere(_rsa).Should().BeTrue();
        using var outraChave = RSA.Create(2048);
        token.AssinaturaConfere(outraChave).Should().BeFalse();
    }

    [Fact]
    public async Task GerarTokenAsync_DuasChamadas_DevemTrazerJtiDiferente()
    {
        var servico = new LocalRsaTokenService(_rsa, 60, "emissor", "publico");
        var usuario = new UsuarioBuilder().Build();

        var primeiro = JwtDeTeste.Ler(await servico.GerarTokenAsync(usuario));
        var segundo = JwtDeTeste.Ler(await servico.GerarTokenAsync(usuario));

        primeiro.Corpo.GetProperty("jti").GetString().Should().NotBe(segundo.Corpo.GetProperty("jti").GetString());
    }

    [Fact]
    public async Task ObterJwksAsync_DeveExporSomenteAChavePublicaComOMesmoKidDoToken()
    {
        var provedor = new LocalRsaJwksProvider(_rsa);

        var jwks = await provedor.ObterJwksAsync();

        var chave = jwks.Keys.Should().ContainSingle().Subject;
        chave.Kty.Should().Be("RSA");
        chave.Use.Should().Be("sig");
        chave.Alg.Should().Be("RS256");
        chave.Kid.Should().Be("local-dev");

        var parametros = _rsa.ExportParameters(false);
        Base64UrlEncoder.DecodeBytes(chave.N).Should().Equal(parametros.Modulus);
        Base64UrlEncoder.DecodeBytes(chave.E).Should().Equal(parametros.Exponent);
    }

    [Fact]
    public async Task ChavePublicaDoJwks_DeveValidarATokenAssinadaPeloServico()
    {
        var servico = new LocalRsaTokenService(_rsa, 60, "emissor", "publico");
        var chave = (await new LocalRsaJwksProvider(_rsa).ObterJwksAsync()).Keys.Single();
        using var rsaPublica = RSA.Create();
        rsaPublica.ImportParameters(new RSAParameters
        {
            Modulus = Base64UrlEncoder.DecodeBytes(chave.N),
            Exponent = Base64UrlEncoder.DecodeBytes(chave.E)
        });

        var token = JwtDeTeste.Ler(await servico.GerarTokenAsync(new UsuarioBuilder().Build()));

        token.AssinaturaConfere(rsaPublica).Should().BeTrue();
    }
}
