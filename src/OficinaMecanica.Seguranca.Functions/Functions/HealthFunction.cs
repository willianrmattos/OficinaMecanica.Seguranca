using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using OficinaMecanica.Seguranca.Functions.Json;

namespace OficinaMecanica.Seguranca.Functions.Functions;

public class HealthFunction
{
    [Function("Health")]
    [OpenApiOperation(operationId: "Health", tags: new[] { "Sistema" },
        Summary = "Health check simples",
        Description = "Confirma que o processo da Function esta de pe, sem checar dependencias externas " +
            "(banco de dados, Key Vault etc) - suficiente pro readiness/liveness probe do orquestrador.")]
    [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(HealthResponseDto),
        Description = "Status fixo, sempre \"ok\" quando o proprio endpoint consegue responder.")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json; charset=utf-8");
        await response.WriteStringAsync(JsonSerializer.Serialize(new { status = "ok" }, SerializationDefaults.CamelCase), cancellationToken);
        return response;
    }
}

// DTO so pra documentacao do OpenAPI - o corpo real da resposta e um anonimo (ver acima), mas o atributo
// OpenApiResponseWithBody precisa de um tipo nomeado pra gerar um schema JSON legivel (nao um "object" vazio).
internal record HealthResponseDto(string Status);
