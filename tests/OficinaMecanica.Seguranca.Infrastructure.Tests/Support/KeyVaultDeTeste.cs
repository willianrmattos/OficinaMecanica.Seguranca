using System.Security.Cryptography;
using Azure;
using Azure.Security.KeyVault.Keys;
using Azure.Security.KeyVault.Keys.Cryptography;
using Moq;

namespace OficinaMecanica.Seguranca.Infrastructure.Tests.Support;

// Monta os objetos do SDK do Key Vault sem rede: KeyClient e CryptographyClient tem construtor protegido para mock,
// e o KeyModelFactory cria o KeyVaultKey com o id (a URI com a versao) e o material publico da chave.
internal static class KeyVaultDeTeste
{
    public const string VersaoDaChave = "a1b2c3d4e5f60718293a4b5c6d7e8f90";

    public static KeyVaultKey CriarChave(RSA rsa, string nome = "chave-jwt", string versao = VersaoDaChave)
    {
        var id = new Uri($"https://kvfiap.vault.azure.net/keys/{nome}/{versao}");
        return KeyModelFactory.KeyVaultKey(
            KeyModelFactory.KeyProperties(id: id, name: nome, version: versao),
            new JsonWebKey(rsa));
    }

    public static Mock<KeyClient> CriarKeyClient(KeyVaultKey chave)
    {
        var keyClient = new Mock<KeyClient>();
        keyClient
            .Setup(c => c.GetKeyAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(chave, Mock.Of<Response>()));
        return keyClient;
    }
}
