using System.Text;
using System.Text.Json;
using Azure.Core;
using Azure.Security.KeyVault.Keys;
using Azure.Security.KeyVault.Keys.Cryptography;
using Microsoft.IdentityModel.Tokens;
using OficinaMecanica.Seguranca.Application.Interfaces;
using OficinaMecanica.Seguranca.Domain.Entities;

namespace OficinaMecanica.Seguranca.Infrastructure.Services;

// Assino o JWT remotamente via CryptographyClient (chamada ao Key Vault) em vez de montar
// SigningCredentials com a chave privada em memoria: a chave RSA nunca sai do Key Vault,
// so entra/sai o material a ser assinado e a assinatura resultante.
public class KeyVaultTokenService : ITokenService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);

    private readonly KeyClient _keyClient;
    private readonly string _keyName;
    private readonly TokenCredential _credential;
    private readonly int _expiracaoMinutos;
    private readonly string _issuer;
    private readonly string _audience;

    private readonly SemaphoreSlim _cacheLock = new(1, 1);
    private KeyVaultKey? _cachedKey;
    private CryptographyClient? _cachedCryptoClient;
    private DateTimeOffset _cachedAt;

    public int ExpiracaoMinutos => _expiracaoMinutos;

    public KeyVaultTokenService(
        KeyClient keyClient,
        string keyName,
        TokenCredential credential,
        int expiracaoMinutos,
        string issuer,
        string audience)
    {
        _keyClient = keyClient;
        _keyName = keyName;
        _credential = credential;
        _expiracaoMinutos = expiracaoMinutos;
        _issuer = issuer;
        _audience = audience;
    }

    public async Task<string> GerarTokenAsync(Usuario usuario, CancellationToken cancellationToken = default)
    {
        var (key, cryptoClient) = await ObterChaveAtualAsync(cancellationToken);

        // O kid do JWT precisa ser so a versao da chave (ultimo segmento da URI do Key Vault),
        // pra bater com o kid exposto no JWKS (ver KeyVaultJwksProvider).
        var kid = key.Id!.Segments[^1].TrimEnd('/');

        var header = new
        {
            alg = "RS256",
            typ = "JWT",
            kid
        };

        var agora = DateTimeOffset.UtcNow;
        var payload = new
        {
            sub = usuario.Id.ToString(),
            name = usuario.Cpf.Numero,
            role = usuario.Perfil.ToString(),
            jti = Guid.NewGuid().ToString(),
            iat = agora.ToUnixTimeSeconds(),
            exp = agora.AddMinutes(_expiracaoMinutos).ToUnixTimeSeconds(),
            iss = _issuer,
            aud = _audience
        };

        var headerB64 = Base64UrlEncoder.Encode(JsonSerializer.SerializeToUtf8Bytes(header));
        var payloadB64 = Base64UrlEncoder.Encode(JsonSerializer.SerializeToUtf8Bytes(payload));
        var signingInput = $"{headerB64}.{payloadB64}";

        // SignDataAsync (nao SignAsync) porque recebe os dados crus e faz o hash SHA-256 internamente,
        // exatamente o algoritmo que RS256 exige - evita eu ter que hashear na mao antes de enviar.
        var signResult = await cryptoClient.SignDataAsync(
            SignatureAlgorithm.RS256,
            Encoding.UTF8.GetBytes(signingInput),
            cancellationToken);

        var signatureB64 = Base64UrlEncoder.Encode(signResult.Signature);

        return $"{signingInput}.{signatureB64}";
    }

    private async Task<(KeyVaultKey key, CryptographyClient client)> ObterChaveAtualAsync(CancellationToken cancellationToken)
    {
        if (_cachedKey is not null && _cachedCryptoClient is not null && DateTimeOffset.UtcNow - _cachedAt < CacheTtl)
            return (_cachedKey, _cachedCryptoClient);

        await _cacheLock.WaitAsync(cancellationToken);
        try
        {
            if (_cachedKey is not null && _cachedCryptoClient is not null && DateTimeOffset.UtcNow - _cachedAt < CacheTtl)
                return (_cachedKey, _cachedCryptoClient);

            var response = await _keyClient.GetKeyAsync(_keyName, cancellationToken: cancellationToken);
            var key = response.Value;
            var cryptoClient = new CryptographyClient(key.Id, _credential);

            _cachedKey = key;
            _cachedCryptoClient = cryptoClient;
            _cachedAt = DateTimeOffset.UtcNow;

            return (key, cryptoClient);
        }
        finally
        {
            _cacheLock.Release();
        }
    }
}
