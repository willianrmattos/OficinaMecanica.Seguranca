using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Abstractions;
using Microsoft.OpenApi.Models;

namespace OficinaMecanica.Seguranca.Functions;

// O pacote OpenApi do Functions nao le nenhum header de proxy sozinho
// (diferente do Swashbuckle usado no OficinaMecanica) - preciso desse filtro
// pra montar o servers[] com a URL publica da APIM (proto/host/prefixo), em
// vez do hostname cru da Function (que o backend "seguranca" da APIM acessa
// direto, sem passar por nenhum gateway/ingress no meio).
//
// Uso nomes de header proprios (X-Gateway-*), NAO X-Forwarded-* - confirmado
// na pratica que o App Service intercepta qualquer header client-supplied
// comecando com "X-Forwarded-" antes mesmo de chegar no worker isolado
// (Headers da funcao real, nao so do OpenApi, mostraram isso: sobra so
// X-AppService-Proto/X-Original-* do hop interno host->worker, nada do
// X-Forwarded-Proto/-Host/-Prefix que a policy da APIM injeta ou que um
// cliente manda manualmente - fica completamente descartado no meio do
// caminho). X-Gateway-* nao colide com essa protecao, sobrevive intacto.
//
// Sem esses headers (acesso direto na Function, sem APIM), cai no proprio
// scheme/host da requisicao, sem prefixo nenhum - continua correto tambem
// nesse caso.
public class ServerBasePathDocumentFilter : IDocumentFilter
{
    public void Apply(IHttpRequestDataObject req, OpenApiDocument document)
    {
        var scheme = req.Headers.TryGetValue("X-Gateway-Proto", out var proto)
            ? proto.ToString()
            : req.Scheme;

        var host = req.Headers.TryGetValue("X-Gateway-Host", out var gatewayHost)
            ? gatewayHost.ToString()
            : req.Host.ToString();

        var prefix = req.Headers.TryGetValue("X-Gateway-Prefix", out var gatewayPrefix)
            ? gatewayPrefix.ToString()
            : string.Empty;

        document.Servers = new List<OpenApiServer>
        {
            new() { Url = $"{scheme}://{host}{prefix}" }
        };
    }
}
