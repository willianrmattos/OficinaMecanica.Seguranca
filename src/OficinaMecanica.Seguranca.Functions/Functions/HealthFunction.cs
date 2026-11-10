using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OficinaMecanica.Seguranca.Functions.Health;

namespace OficinaMecanica.Seguranca.Functions.Functions;

// Mesmos dois endpoints dos outros servicos: /health (liveness, sem dependencias) e /health/ready (prontidao, com banco e
// Key Vault). Ambos anonimos de proposito: sao sondas de infraestrutura e ficam atras do wildcard da APIM.
public class HealthFunction
{
    private readonly HealthCheckService _health;

    public HealthFunction(HealthCheckService health)
    {
        _health = health;
    }

    [Function("Health")]
    [OpenApiOperation(operationId: "Health", tags: new[] { "Sistema" },
        Summary = "Liveness",
        Description = "Confirma que o processo da Function esta de pe, sem checar dependencias externas " +
            "(banco de dados, Key Vault etc).")]
    [OpenApiResponseWithBody(HttpStatusCode.OK, "text/plain", typeof(string),
        Description = "Sempre \"Healthy\" quando o proprio endpoint consegue responder.")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "text/plain; charset=utf-8");
        await response.WriteStringAsync("Healthy", cancellationToken);
        return response;
    }

    [Function("HealthReady")]
    [OpenApiOperation(operationId: "HealthReady", tags: new[] { "Sistema" },
        Summary = "Prontidao",
        Description = "Verifica as dependencias sem as quais o login nao funciona: o banco de dados (SegurancaDb) e a chave " +
            "RSA no Key Vault. Responde 200 quando tudo esta saudavel e 503 quando alguma dependencia essencial falha; o " +
            "resultado de cada check fica em cache por poucos segundos.")]
    [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(ProntidaoDto),
        Description = "Status agregado e o resultado de cada dependencia.")]
    [OpenApiResponseWithBody(HttpStatusCode.ServiceUnavailable, "application/json", typeof(ProntidaoDto),
        Description = "Alguma dependencia essencial esta indisponivel.")]
    public async Task<HttpResponseData> Ready(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health/ready")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var relatorio = await _health.CheckHealthAsync(check => check.Tags.Contains("ready"), cancellationToken);

        var response = req.CreateResponse(ProntidaoRelatorio.CodigoHttp(relatorio));
        response.Headers.Add("Content-Type", "application/json; charset=utf-8");
        await response.WriteStringAsync(ProntidaoRelatorio.Serializar(relatorio), cancellationToken);
        return response;
    }
}

// DTOs so pra documentacao do OpenAPI - o corpo real da resposta e um anonimo (ver ProntidaoRelatorio), mas o atributo
// OpenApiResponseWithBody precisa de um tipo nomeado pra gerar um schema JSON legivel (nao um "object" vazio).
internal record ProntidaoCheckDto(string Status, string? Descricao);

internal record ProntidaoDto(string Status, Dictionary<string, ProntidaoCheckDto> Checks);
