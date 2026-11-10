using System.Net;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Azure.Functions.Worker;
using Moq;
using OficinaMecanica.Seguranca.Application.Exceptions;
using OficinaMecanica.Seguranca.Functions.Middleware;

namespace OficinaMecanica.Seguranca.Functions.Tests.Middleware;

public class ExceptionHandlingMiddlewareTests
{
    [Fact]
    public async Task Invoke_SemExcecao_DeveApenasSeguirParaOProximoDaCadeia()
    {
        var chamouProximo = false;

        await new ExceptionHandlingMiddleware().Invoke(Mock.Of<FunctionContext>(), _ =>
        {
            chamouProximo = true;
            return Task.CompletedTask;
        });

        // O caminho de erro do Invoke (montar a resposta e gravar em GetInvocationResult) depende de IFunctionBindingsFeature,
        // que e interna do SDK: so o host real o preenche, entao ele fica para o teste de Integration (Azurite).
        chamouProximo.Should().BeTrue();
    }

    [Fact]
    public void MapearExcecao_ParaFalhaDeAutenticacao_DeveSer401ComAMensagemDaExcecao()
    {
        var (status, tipo, mensagem, erros) =
            ExceptionHandlingMiddleware.MapearExcecao(new AutenticacaoException("Usuário ou senha inválidos."));

        status.Should().Be(HttpStatusCode.Unauthorized);
        tipo.Should().Be("Erro de Autenticação");
        mensagem.Should().Be("Usuário ou senha inválidos.");
        erros.Should().BeNull();
    }

    [Fact]
    public void MapearExcecao_ParaFalhaDeValidacao_DeveSer400ListandoCampoEMensagemDeCadaErro()
    {
        var excecao = new ValidationException(new[]
        {
            new ValidationFailure("Cpf", "O CPF é obrigatório."),
            new ValidationFailure("Senha", "A senha é obrigatória.")
        });

        var (status, tipo, mensagem, erros) = ExceptionHandlingMiddleware.MapearExcecao(excecao);

        status.Should().Be(HttpStatusCode.BadRequest);
        tipo.Should().Be("Erro de Validação");
        mensagem.Should().Be("Erro de validação.");
        var itens = ((IEnumerable<object>)erros!).Select(e => e.ToString()).ToList();
        itens.Should().HaveCount(2);
        itens[0].Should().Contain("Cpf").And.Contain("O CPF é obrigatório.");
        itens[1].Should().Contain("Senha").And.Contain("A senha é obrigatória.");
    }

    [Fact]
    public void MapearExcecao_ParaQualquerOutraExcecao_DeveSer500SemVazarODetalheInterno()
    {
        var (status, tipo, mensagem, erros) =
            ExceptionHandlingMiddleware.MapearExcecao(new InvalidOperationException("segredo interno: connection string"));

        status.Should().Be(HttpStatusCode.InternalServerError);
        tipo.Should().Be("Erro Interno");
        mensagem.Should().Be("Ocorreu um erro interno no servidor.");
        mensagem.Should().NotContain("segredo");
        erros.Should().BeNull();
    }
}
