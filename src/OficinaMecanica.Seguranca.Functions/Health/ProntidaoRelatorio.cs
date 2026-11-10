using System.Net;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace OficinaMecanica.Seguranca.Functions.Health;

// Corpo e status HTTP de /health/ready, no mesmo formato dos outros servicos: o status agregado e o de cada check. Escreve so
// a descricao de cada check, nunca a excecao, que traz host e detalhes de conexao.
public static class ProntidaoRelatorio
{
    private static readonly JsonSerializerOptions Opcoes = new(JsonSerializerDefaults.Web)
    {
        // Mantem os acentos das descricoes em portugues legiveis (o padrao os escapa em hexadecimal); e JSON puro, nunca HTML.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static string Serializar(HealthReport relatorio)
    {
        var corpo = new
        {
            status = relatorio.Status.ToString(),
            checks = relatorio.Entries.ToDictionary(
                e => e.Key,
                e => new { status = e.Value.Status.ToString(), descricao = e.Value.Description })
        };

        return JsonSerializer.Serialize(corpo, Opcoes);
    }

    // Healthy e Degraded respondem 200 (Degraded e um aviso, a Function segue atendendo); so Unhealthy tira a Function de rotacao.
    public static HttpStatusCode CodigoHttp(HealthReport relatorio) =>
        relatorio.Status == HealthStatus.Unhealthy ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK;
}
