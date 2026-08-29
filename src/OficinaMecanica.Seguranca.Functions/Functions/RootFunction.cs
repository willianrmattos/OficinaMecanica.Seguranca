using System.Linq;
using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;

namespace OficinaMecanica.Seguranca.Functions.Functions;

// Redireciona a raiz pro Swagger UI - mesma ideia do RoutePrefix = string.Empty
// usado no OficinaMecanica. Uma rota HttpTrigger com Route = "" ou "/"
// nao funciona pra isso no Functions isolated worker (cai no nome da function
// ou gera uma rota duplicada quebrada "//") - a alternativa real e um catch-all
// de menor prioridade: rotas literais (health, login, swagger/ui etc) sempre
// casam primeiro, esse so pega o que sobrar. So redireciona quando o caminho
// capturado vem vazio (raiz de verdade); qualquer outro caminho desconhecido
// continua virando 404 normalmente.
public class RootFunction
{
    [Function("Root")]
    [OpenApiIgnore]
    public HttpResponseData Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "{*caminho}")] HttpRequestData req,
        string? caminho)
    {
        if (string.IsNullOrEmpty(caminho))
        {
            // Mesmo header X-Gateway-Prefix que o ServerBasePathDocumentFilter le - sem ele
            // (acesso direto na Function, sem APIM) o redirect fica relativo a raiz do proprio
            // host, sem prefixo nenhum, que tambem esta correto nesse caso.
            var prefix = req.Headers.TryGetValues("X-Gateway-Prefix", out var valores)
                ? valores.FirstOrDefault() ?? string.Empty
                : string.Empty;

            var redirect = req.CreateResponse(HttpStatusCode.Redirect);
            redirect.Headers.Add("Location", $"{prefix}/swagger/ui");
            return redirect;
        }

        return req.CreateResponse(HttpStatusCode.NotFound);
    }
}
