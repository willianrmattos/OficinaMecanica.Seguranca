using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;
using OficinaMecanica.Seguranca.Application.DTOs;
using OficinaMecanica.Seguranca.Application.Interfaces;

namespace OficinaMecanica.Seguranca.Infrastructure.Services;

// Par do LocalRsaTokenService - expoe a chave publica da mesma instancia de RSA compartilhada via DI,
// pra bater com a chave privada usada pra assinar (ver comentario em LocalRsaTokenService).
public class LocalRsaJwksProvider : IJwksProvider
{
    private const string Kid = "local-dev";

    private readonly RSA _rsa;

    public LocalRsaJwksProvider(RSA rsa)
    {
        _rsa = rsa;
    }

    public Task<JwksDto> ObterJwksAsync(CancellationToken cancellationToken = default)
    {
        // false = sem a chave privada, so os parametros publicos (Modulus e Exponent).
        var parametros = _rsa.ExportParameters(false);

        var n = Base64UrlEncoder.Encode(parametros.Modulus!);
        var e = Base64UrlEncoder.Encode(parametros.Exponent!);

        var jwks = new JwksDto(new[] { new JwkDto("RSA", "sig", "RS256", Kid, n, e) });

        return Task.FromResult(jwks);
    }
}
