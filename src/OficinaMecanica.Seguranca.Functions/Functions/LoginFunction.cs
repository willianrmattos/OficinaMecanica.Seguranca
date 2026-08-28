using System.Net;
using System.Text.Json;
using MediatR;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using OficinaMecanica.Seguranca.Application.Commands.Login;
using OficinaMecanica.Seguranca.Application.DTOs;
using OficinaMecanica.Seguranca.Functions.Json;

namespace OficinaMecanica.Seguranca.Functions.Functions;

// So traduz HTTP <-> MediatR - toda a regra de autenticacao mora no LoginCommandHandler (Application).
// Anonima de proposito: e o proprio endpoint de login, nao faz sentido exigir autenticacao pra se autenticar.
public class LoginFunction
{
    private readonly IMediator _mediator;
    private readonly ILogger<LoginFunction> _logger;

    public LoginFunction(IMediator mediator, ILogger<LoginFunction> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    [Function("Login")]
    [OpenApiOperation(operationId: "Login", tags: new[] { "Autenticacao" },
        Summary = "Autentica um usuario e emite um token JWT",
        Description = "Valida nome de usuario e senha e, se corretos, retorna um JWT assinado com RS256 " +
            "(chave gerenciada no Key Vault), valido pelo tempo configurado em JwtSettings:ExpiracaoMinutos.")]
    [OpenApiRequestBody("application/json", typeof(LoginRequestDto),
        Description = "Credenciais do usuario (nome de usuario e senha em texto plano - por isso o endpoint so deve ser exposto via HTTPS).")]
    [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(LoginResponseDto),
        Description = "Token JWT emitido, junto do tipo do esquema de autenticacao e do tempo de expiracao em minutos.")]
    [OpenApiResponseWithoutBody(HttpStatusCode.Unauthorized,
        Description = "Nome de usuario ou senha invalidos.")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "login")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Requisição de login recebida.");

        var corpo = await JsonSerializer.DeserializeAsync<LoginRequestDto>(req.Body, SerializationDefaults.CamelCase, cancellationToken);

        var comando = new LoginCommand(corpo?.NomeUsuario ?? string.Empty, corpo?.Senha ?? string.Empty);
        var resultado = await _mediator.Send(comando, cancellationToken);

        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json; charset=utf-8");
        await response.WriteStringAsync(JsonSerializer.Serialize(resultado, SerializationDefaults.CamelCase), cancellationToken);
        return response;
    }
}

// DTO de transporte so pra desserializar o body do POST - o contrato de negocio real e o LoginCommand (Application).
internal record LoginRequestDto(string NomeUsuario, string Senha);
