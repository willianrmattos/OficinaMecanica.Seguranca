using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;
using OficinaMecanica.Seguranca.Application.Interfaces;
using OficinaMecanica.Seguranca.Domain.Entities;

namespace OficinaMecanica.Seguranca.Infrastructure.Services;

// Essa e a implementacao usada quando KeyVault:Uri nao esta configurado (dev local/testes) - assino
// o JWT com uma chave RSA gerada em memoria em vez de chamar o Key Vault remotamente.
// A chave e efemera - gerada a cada start do processo, nunca persiste em disco - entao tokens
// emitidos antes de um restart do processo ficam invalidos depois. NAO adequado pra producao de
// verdade, por isso so e escolhida quando o Key Vault explicitamente NAO esta configurado
// (ver DependencyInjection.cs).
public class LocalRsaTokenService : ITokenService
{
    private const string Kid = "local-dev";

    private readonly RSA _rsa;
    private readonly int _expiracaoMinutos;
    private readonly string _issuer;
    private readonly string _audience;

    public int ExpiracaoMinutos => _expiracaoMinutos;

    public LocalRsaTokenService(RSA rsa, int expiracaoMinutos, string issuer, string audience)
    {
        _rsa = rsa;
        _expiracaoMinutos = expiracaoMinutos;
        _issuer = issuer;
        _audience = audience;
    }

    public Task<string> GerarTokenAsync(Usuario usuario, CancellationToken cancellationToken = default)
    {
        // Kid fixo - so existe uma chave nesse modo, entao nao precisa de versionamento
        // como no Key Vault (onde o kid e a versao da chave).
        var header = new
        {
            alg = "RS256",
            typ = "JWT",
            kid = Kid
        };

        var agora = DateTimeOffset.UtcNow;
        var payload = new
        {
            sub = usuario.Id.ToString(),
            name = usuario.NomeUsuario,
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

        // RS256 = PKCS#1 v1.5 com SHA-256 - equivalente ao que o Key Vault faz remotamente
        // via CryptographyClient.SignDataAsync(SignatureAlgorithm.RS256, ...).
        var signature = _rsa.SignData(
            Encoding.UTF8.GetBytes(signingInput),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        var signatureB64 = Base64UrlEncoder.Encode(signature);

        return Task.FromResult($"{signingInput}.{signatureB64}");
    }
}
