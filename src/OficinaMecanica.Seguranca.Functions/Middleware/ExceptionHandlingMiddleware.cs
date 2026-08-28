using System.Net;
using System.Text.Json;
using FluentValidation;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Middleware;
using OficinaMecanica.Seguranca.Application.Exceptions;
using OficinaMecanica.Seguranca.Functions.Json;

namespace OficinaMecanica.Seguranca.Functions.Middleware;

public class ExceptionHandlingMiddleware : IFunctionsWorkerMiddleware
{
    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            var requestData = await context.GetHttpRequestDataAsync();
            if (requestData == null)
                throw; // nao e uma invocacao HTTP (ex: outro tipo de trigger) - nao da pra montar resposta, deixo propagar

            var (statusCode, tipo, mensagem, erros) = MapearExcecao(ex);

            var response = requestData.CreateResponse(statusCode);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");

            var errorResponse = new { tipo, mensagem, erros };
            await response.WriteStringAsync(JsonSerializer.Serialize(errorResponse, SerializationDefaults.CamelCase));

            // No isolated worker, o retorno da Function ja foi computado (ou explodiu) antes desse
            // catch rodar - pra essa resposta de erro realmente virar o retorno HTTP da invocacao,
            // preciso sobrescrever o resultado guardado no proprio FunctionContext.
            context.GetInvocationResult().Value = response;
        }
    }

    private static (HttpStatusCode statusCode, string tipo, string mensagem, object? erros) MapearExcecao(Exception ex) => ex switch
    {
        AutenticacaoException autenticacaoEx => (HttpStatusCode.Unauthorized, "Erro de Autenticação", autenticacaoEx.Message, null),
        ValidationException validationEx => (HttpStatusCode.BadRequest, "Erro de Validação", "Erro de validação.", validationEx.Errors.Select(e => new { campo = e.PropertyName, mensagem = e.ErrorMessage })),
        _ => (HttpStatusCode.InternalServerError, "Erro Interno", "Ocorreu um erro interno no servidor.", null)
    };
}
