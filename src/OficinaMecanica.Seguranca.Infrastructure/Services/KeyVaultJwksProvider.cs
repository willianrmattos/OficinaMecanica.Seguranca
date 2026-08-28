using Azure.Security.KeyVault.Keys;
using Microsoft.IdentityModel.Tokens;
using OficinaMecanica.Seguranca.Application.DTOs;
using OficinaMecanica.Seguranca.Application.Interfaces;

namespace OficinaMecanica.Seguranca.Infrastructure.Services;

public class KeyVaultJwksProvider : IJwksProvider
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);

    private readonly KeyClient _keyClient;
    private readonly string _keyName;

    private readonly SemaphoreSlim _cacheLock = new(1, 1);
    private JwksDto? _cachedJwks;
    private DateTimeOffset _cachedAt;

    public KeyVaultJwksProvider(KeyClient keyClient, string keyName)
    {
        _keyClient = keyClient;
        _keyName = keyName;
    }

    public async Task<JwksDto> ObterJwksAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedJwks is not null && DateTimeOffset.UtcNow - _cachedAt < CacheTtl)
            return _cachedJwks;

        await _cacheLock.WaitAsync(cancellationToken);
        try
        {
            if (_cachedJwks is not null && DateTimeOffset.UtcNow - _cachedAt < CacheTtl)
                return _cachedJwks;

            var response = await _keyClient.GetKeyAsync(_keyName, cancellationToken: cancellationToken);
            var key = response.Value;

            // Mesma logica de kid do KeyVaultTokenService - tem que bater os dois pro validador
            // do JWT conseguir achar a chave publica certa pelo kid do header.
            var kid = key.Id!.Segments[^1].TrimEnd('/');
            var n = Base64UrlEncoder.Encode(key.Key.N);
            var e = Base64UrlEncoder.Encode(key.Key.E);

            var jwks = new JwksDto(new[] { new JwkDto("RSA", "sig", "RS256", kid, n, e) });

            _cachedJwks = jwks;
            _cachedAt = DateTimeOffset.UtcNow;

            return jwks;
        }
        finally
        {
            _cacheLock.Release();
        }
    }
}
