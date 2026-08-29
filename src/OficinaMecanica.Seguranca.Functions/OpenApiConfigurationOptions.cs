using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Abstractions;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Configurations;
using Microsoft.OpenApi.Models;

namespace OficinaMecanica.Seguranca.Functions;

// Personalizo titulo/versao/descricao do documento OpenAPI e registro o ServerBasePathDocumentFilter
// (basepath do Swagger via APIM) - o resto (versao do spec, formato etc) fica no comportamento padrao
// de DefaultOpenApiConfigurationOptions. Descoberta automatica via reflection pelo proprio pacote
// (OpenApiConfigurationResolver.Resolve varre a assembly atras de um IOpenApiConfigurationOptions
// publico) - nao preciso registrar isso em DI no Program.cs.
public class OpenApiConfigurationOptions : DefaultOpenApiConfigurationOptions
{
    public override OpenApiInfo Info { get; set; } = new OpenApiInfo
    {
        Title = "OficinaMecanica.Seguranca",
        Version = "v1",
        Description = "Servico de autenticacao/autorizacao do ecossistema OficinaMecanica. Emite tokens JWT " +
            "assinados com RS256 (par de chaves assimetrico) e expoe o JWKS correspondente em " +
            "/.well-known/jwks.json, para que as demais APIs do ecossistema (ex.: OficinaMecanica) validem " +
            "esses tokens usando somente a chave publica, sem segredo compartilhado entre servicos."
    };

    public override List<IDocumentFilter> DocumentFilters { get; set; } = new List<IDocumentFilter>
    {
        new ServerBasePathDocumentFilter()
    };
}
