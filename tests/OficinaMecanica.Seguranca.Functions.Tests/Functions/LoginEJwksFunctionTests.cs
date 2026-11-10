using System.Net;
using System.Text.Json;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OficinaMecanica.Seguranca.Application.Commands.Login;
using OficinaMecanica.Seguranca.Application.DTOs;
using OficinaMecanica.Seguranca.Application.Exceptions;
using OficinaMecanica.Seguranca.Application.Queries.ObterJwks;
using OficinaMecanica.Seguranca.Functions.Functions;
using OficinaMecanica.Seguranca.Functions.Tests.Support;

namespace OficinaMecanica.Seguranca.Functions.Tests.Functions;

public class LoginEJwksFunctionTests
{
    private readonly Mock<IMediator> _mediator = new();

    private LoginFunction Login() => new(_mediator.Object, NullLogger<LoginFunction>.Instance);

    private JwksFunction Jwks() => new(_mediator.Object, NullLogger<JwksFunction>.Instance);

    [Fact]
    public async Task Login_ComCredenciais_DeveEnviarOComandoEResponder200ComOTokenEmCamelCase()
    {
        _mediator
            .Setup(m => m.Send(It.IsAny<LoginCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LoginResponseDto("token-gerado", "Bearer", 60));
        var requisicao = new FakeHttpRequestData("""{"cpf":"52998224725","senha":"senha-correta"}""");

        var resposta = (FakeHttpResponseData)await Login().Run(requisicao, CancellationToken.None);

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        resposta.ContentType.Should().StartWith("application/json");
        var json = JsonDocument.Parse(resposta.Texto()).RootElement;
        json.GetProperty("token").GetString().Should().Be("token-gerado");
        json.GetProperty("tipo").GetString().Should().Be("Bearer");
        json.GetProperty("expiracaoMinutos").GetInt32().Should().Be(60);
        _mediator.Verify(m => m.Send(
            It.Is<LoginCommand>(c => c.Cpf == "52998224725" && c.Senha == "senha-correta"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("null")]
    public async Task Login_SemCpfESenhaNoCorpo_DeveEnviarComandoComTextosVaziosParaOValidatorRecusar(string corpo)
    {
        _mediator
            .Setup(m => m.Send(It.IsAny<LoginCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LoginResponseDto("t", "Bearer", 1));

        await Login().Run(new FakeHttpRequestData(corpo), CancellationToken.None);

        _mediator.Verify(m => m.Send(
            It.Is<LoginCommand>(c => c.Cpf == string.Empty && c.Senha == string.Empty),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Login_QuandoOHandlerRecusaAsCredenciais_DevePropagarAExcecaoParaOMiddlewareTraduzir()
    {
        _mediator
            .Setup(m => m.Send(It.IsAny<LoginCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AutenticacaoException("Usuário ou senha inválidos."));

        var act = () => Login().Run(new FakeHttpRequestData("""{"cpf":"1","senha":"x"}"""), CancellationToken.None);

        await act.Should().ThrowAsync<AutenticacaoException>();
    }

    [Fact]
    public async Task Jwks_DeveResponder200ComAsChavesEmCamelCaseECacheDeDezMinutos()
    {
        var chave = new JwkDto("RSA", "sig", "RS256", "kid-1", "modulo", "expoente");
        _mediator
            .Setup(m => m.Send(It.IsAny<ObterJwksQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new JwksDto(new[] { chave }));

        var resposta = (FakeHttpResponseData)await Jwks().Run(new FakeHttpRequestData(), CancellationToken.None);

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        resposta.ContentType.Should().StartWith("application/json");
        resposta.Headers.GetValues("Cache-Control").Should().ContainSingle().Which.Should().Be("public, max-age=600");
        var primeira = JsonDocument.Parse(resposta.Texto()).RootElement.GetProperty("keys").EnumerateArray().Single();
        primeira.GetProperty("kty").GetString().Should().Be("RSA");
        primeira.GetProperty("use").GetString().Should().Be("sig");
        primeira.GetProperty("alg").GetString().Should().Be("RS256");
        primeira.GetProperty("kid").GetString().Should().Be("kid-1");
        primeira.GetProperty("n").GetString().Should().Be("modulo");
        primeira.GetProperty("e").GetString().Should().Be("expoente");
    }
}
