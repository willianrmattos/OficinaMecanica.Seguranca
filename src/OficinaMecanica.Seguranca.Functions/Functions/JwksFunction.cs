using System.Net;
using System.Text.Json;
using MediatR;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using OficinaMecanica.Seguranca.Application.DTOs;
using OficinaMecanica.Seguranca.Application.Queries.ObterJwks;
using OficinaMecanica.Seguranca.Functions.Json;

namespace OficinaMecanica.Seguranca.Functions.Functions;

// So traduz HTTP <-> MediatR - a chave publica em si vem do ObterJwksQueryHandler (Application/Infrastructure).
// Anonima de proposito: JWKS e informacao publica por definicao (e assim que qualquer validador de JWT externo
// descobre a chave publica pra conferir a assinatura RS256).
public class JwksFunction
{
    private readonly IMediator _mediator;
    private readonly ILogger<JwksFunction> _logger;

    public JwksFunction(IMediator mediator, ILogger<JwksFunction> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    [Function("Jwks")]
    [OpenApiOperation(operationId: "Jwks", tags: new[] { "Autenticacao" },
        Summary = "Expoe o JWKS (JSON Web Key Set) publico",
        Description = "Retorna o conjunto de chaves publicas RSA (formato JWKS, RFC 7517) usado para validar " +
            "a assinatura RS256 dos tokens JWT emitidos por este servico - endpoint publico por definicao, e " +
            "assim que qualquer validador de JWT externo descobre a chave publica correspondente ao 'kid' do token.")]
    [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(JwksDto),
        Description = "Lista de chaves publicas (JWK) atualmente validas para verificacao de assinatura.")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = ".well-known/jwks.json")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Requisição de JWKS recebida.");

        var resultado = await _mediator.Send(new ObterJwksQuery(), cancellationToken);

        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json; charset=utf-8");
        // JWKS pode ser cacheado por quem consome (validadores de JWT) - reduz carga no Key Vault.
        response.Headers.Add("Cache-Control", "public, max-age=600");
        await response.WriteStringAsync(JsonSerializer.Serialize(resultado, SerializationDefaults.CamelCase), cancellationToken);
        return response;
    }
}
