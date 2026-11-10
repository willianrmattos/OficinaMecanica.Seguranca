using System.Security.Cryptography;
using Azure.Core;
using Azure.Security.KeyVault.Keys;
using Azure.Security.KeyVault.Keys.Cryptography;
using FluentAssertions;
using Microsoft.IdentityModel.Tokens;
using Moq;
using OficinaMecanica.Seguranca.Domain.Enums;
using OficinaMecanica.Seguranca.Infrastructure.Services;
using OficinaMecanica.Seguranca.Infrastructure.Tests.Support;
using OficinaMecanica.Seguranca.Tests.Common.Builders;

namespace OficinaMecanica.Seguranca.Infrastructure.Tests.Services;

public sealed class KeyVaultServicesTests : IDisposable
{
    private readonly RSA _rsa = RSA.Create(2048);
    private readonly Mock<KeyClient> _keyClient;
    private readonly Mock<CryptographyClient> _cryptoClient = new();
    private int _clientesCriados;

    public KeyVaultServicesTests()
    {
        _keyClient = KeyVaultDeTeste.CriarKeyClient(KeyVaultDeTeste.CriarChave(_rsa));

        // Assina de verdade com o RSA local no lugar do Key Vault, para o teste poder conferir a assinatura do JWT.
        _cryptoClient
            .Setup(c => c.SignDataAsync(SignatureAlgorithm.RS256, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SignatureAlgorithm _, byte[] dados, CancellationToken _) =>
                CryptographyModelFactory.SignResult(
                    signature: _rsa.SignData(dados, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)));
    }

    public void Dispose() => _rsa.Dispose();

    private KeyVaultTokenService CriarServicoDeToken(int expiracaoMinutos = 60) =>
        new(_keyClient.Object, "chave-jwt", Mock.Of<TokenCredential>(), expiracaoMinutos, "emissor-kv", "publico-kv",
            _ =>
            {
                _clientesCriados++;
                return _cryptoClient.Object;
            });

    [Fact]
    public async Task GerarTokenAsync_DeveEmitirJwtComKidIgualAVersaoDaChaveEClaimsDoUsuario()
    {
        var usuario = new UsuarioBuilder().ComCpf("52998224725").ComPerfil(PerfilUsuario.Admin).Build();
        var servico = CriarServicoDeToken(expiracaoMinutos: 15);

        var token = JwtDeTeste.Ler(await servico.GerarTokenAsync(usuario));

        token.Cabecalho.GetProperty("alg").GetString().Should().Be("RS256");
        token.Cabecalho.GetProperty("kid").GetString().Should().Be(KeyVaultDeTeste.VersaoDaChave);
        token.Corpo.GetProperty("sub").GetString().Should().Be(usuario.Id.ToString());
        token.Corpo.GetProperty("name").GetString().Should().Be("52998224725");
        token.Corpo.GetProperty("role").GetString().Should().Be("Admin");
        token.Corpo.GetProperty("iss").GetString().Should().Be("emissor-kv");
        token.Corpo.GetProperty("aud").GetString().Should().Be("publico-kv");
        (token.Corpo.GetProperty("exp").GetInt64() - token.Corpo.GetProperty("iat").GetInt64()).Should().Be(15 * 60);
        token.AssinaturaConfere(_rsa).Should().BeTrue();
        servico.ExpiracaoMinutos.Should().Be(15);
    }

    private sealed class CredencialQueRecusa : TokenCredential
    {
        public int Chamadas { get; private set; }

        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            throw new CredencialUsadaException();

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            Chamadas++;
            throw new CredencialUsadaException();
        }
    }

    private sealed class CredencialUsadaException : Exception { }

    [Fact]
    public async Task GerarTokenAsync_SemFabricaInjetada_DeveCriarOClienteReal_UsandoACredencialRecebida()
    {
        // O padrao de producao e new CryptographyClient(chave.Id, credential): o cliente real nao abre rede ao ser criado, so ao
        // assinar, e ai pede o token a credencial. A credencial recusa de proposito; se ela for chamada, o padrao a repassou.
        var credencial = new CredencialQueRecusa();
        var servico = new KeyVaultTokenService(_keyClient.Object, "chave-jwt", credencial, 60, "emissor-kv", "publico-kv");

        var act = () => servico.GerarTokenAsync(new UsuarioBuilder().Build());

        await act.Should().ThrowAsync<CredencialUsadaException>();
        credencial.Chamadas.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GerarTokenAsync_DuasChamadasDentroDoCache_DevemBuscarAChaveECriarOClienteUmaVezSo()
    {
        var servico = CriarServicoDeToken();
        var usuario = new UsuarioBuilder().Build();

        await servico.GerarTokenAsync(usuario);
        await servico.GerarTokenAsync(usuario);

        _keyClient.Verify(c => c.GetKeyAsync("chave-jwt", It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        _clientesCriados.Should().Be(1);
        _cryptoClient.Verify(c => c.SignDataAsync(SignatureAlgorithm.RS256, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task GerarTokenAsync_ComChamadasConcorrentes_DeveBuscarAChaveUmaVezSo()
    {
        var servico = CriarServicoDeToken();
        var usuario = new UsuarioBuilder().Build();

        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => servico.GerarTokenAsync(usuario)));

        _keyClient.Verify(c => c.GetKeyAsync("chave-jwt", It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GerarTokenAsync_QuandoOKeyVaultFalha_DevePropagarAExcecaoEPermitirNovaTentativa()
    {
        var chamadas = 0;
        _keyClient
            .Setup(c => c.GetKeyAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(() => ++chamadas == 1
                ? throw new InvalidOperationException("Key Vault fora do ar")
                : Task.FromResult(Azure.Response.FromValue(KeyVaultDeTeste.CriarChave(_rsa), Mock.Of<Azure.Response>())));
        var servico = CriarServicoDeToken();
        var usuario = new UsuarioBuilder().Build();

        var primeira = () => servico.GerarTokenAsync(usuario);
        await primeira.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Key Vault fora do ar*");

        var token = JwtDeTeste.Ler(await servico.GerarTokenAsync(usuario));
        token.AssinaturaConfere(_rsa).Should().BeTrue();
    }

    [Fact]
    public async Task ObterJwksAsync_DeveExporAChavePublicaComKidIgualAVersaoDaChave()
    {
        var provedor = new KeyVaultJwksProvider(_keyClient.Object, "chave-jwt");

        var jwks = await provedor.ObterJwksAsync();

        var chave = jwks.Keys.Should().ContainSingle().Subject;
        chave.Kty.Should().Be("RSA");
        chave.Use.Should().Be("sig");
        chave.Alg.Should().Be("RS256");
        chave.Kid.Should().Be(KeyVaultDeTeste.VersaoDaChave);
        var parametros = _rsa.ExportParameters(false);
        Base64UrlEncoder.DecodeBytes(chave.N).Should().Equal(parametros.Modulus);
        Base64UrlEncoder.DecodeBytes(chave.E).Should().Equal(parametros.Exponent);
    }

    [Fact]
    public async Task ObterJwksAsync_DentroDoCache_NaoDeveConsultarOKeyVaultDeNovo()
    {
        var provedor = new KeyVaultJwksProvider(_keyClient.Object, "chave-jwt");

        var primeiro = await provedor.ObterJwksAsync();
        var segundo = await provedor.ObterJwksAsync();

        segundo.Should().BeSameAs(primeiro);
        _keyClient.Verify(c => c.GetKeyAsync("chave-jwt", It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ObterJwksAsync_ComChamadasConcorrentes_DeveConsultarOKeyVaultUmaVezSo()
    {
        var provedor = new KeyVaultJwksProvider(_keyClient.Object, "chave-jwt");

        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => provedor.ObterJwksAsync()));

        _keyClient.Verify(c => c.GetKeyAsync("chave-jwt", It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task KidDoTokenEDoJwks_DevemSerOMesmoParaOValidadorAcharAChave()
    {
        var servico = CriarServicoDeToken();
        var provedor = new KeyVaultJwksProvider(_keyClient.Object, "chave-jwt");

        var token = JwtDeTeste.Ler(await servico.GerarTokenAsync(new UsuarioBuilder().Build()));
        var jwks = await provedor.ObterJwksAsync();

        token.Cabecalho.GetProperty("kid").GetString().Should().Be(jwks.Keys.Single().Kid);
    }
}
