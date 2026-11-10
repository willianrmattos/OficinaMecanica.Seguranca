using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;

namespace OficinaMecanica.Seguranca.Infrastructure.Tests.Support;

// Decodifica e confere um JWT na mao: os servicos de token montam o JWT sem biblioteca de JWT, entao o teste
// tambem le as tres partes direto, sem depender de uma lib que "perdoe" um formato errado.
internal sealed class JwtDeTeste
{
    public JsonElement Cabecalho { get; }
    public JsonElement Corpo { get; }
    public byte[] Assinatura { get; }
    public string DadosAssinados { get; }

    private JwtDeTeste(JsonElement cabecalho, JsonElement corpo, byte[] assinatura, string dadosAssinados)
    {
        Cabecalho = cabecalho;
        Corpo = corpo;
        Assinatura = assinatura;
        DadosAssinados = dadosAssinados;
    }

    public static JwtDeTeste Ler(string token)
    {
        var partes = token.Split('.');
        if (partes.Length != 3)
            throw new InvalidOperationException($"JWT com {partes.Length} partes; esperava 3.");

        return new JwtDeTeste(
            JsonDocument.Parse(Base64UrlEncoder.DecodeBytes(partes[0])).RootElement.Clone(),
            JsonDocument.Parse(Base64UrlEncoder.DecodeBytes(partes[1])).RootElement.Clone(),
            Base64UrlEncoder.DecodeBytes(partes[2]),
            $"{partes[0]}.{partes[1]}");
    }

    public bool AssinaturaConfere(RSA chavePublica) =>
        chavePublica.VerifyData(Encoding.UTF8.GetBytes(DadosAssinados), Assinatura, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
}
