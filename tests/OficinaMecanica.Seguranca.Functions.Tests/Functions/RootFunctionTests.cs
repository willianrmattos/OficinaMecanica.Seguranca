using System.Net;
using FluentAssertions;
using OficinaMecanica.Seguranca.Functions.Functions;
using OficinaMecanica.Seguranca.Functions.Tests.Support;

namespace OficinaMecanica.Seguranca.Functions.Tests.Functions;

public class RootFunctionTests
{
    private readonly RootFunction _funcao = new();

    private static string? Location(FakeHttpResponseData resposta) =>
        resposta.Headers.TryGetValues("Location", out var valores) ? valores.Single() : null;

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Run_NaRaiz_DeveRedirecionarParaOSwaggerSemPrefixoQuandoNaoHaGateway(string? caminho)
    {
        var resposta = (FakeHttpResponseData)_funcao.Run(new FakeHttpRequestData(), caminho);

        resposta.StatusCode.Should().Be(HttpStatusCode.Redirect);
        Location(resposta).Should().Be("/swagger/ui");
    }

    [Fact]
    public void Run_NaRaizAtrasDoGateway_DeveRedirecionarComOPrefixoDoGateway()
    {
        var requisicao = new FakeHttpRequestData();
        requisicao.Headers.Add("X-Gateway-Prefix", "/segurancaserver");

        var resposta = (FakeHttpResponseData)_funcao.Run(requisicao, caminho: null);

        resposta.StatusCode.Should().Be(HttpStatusCode.Redirect);
        Location(resposta).Should().Be("/segurancaserver/swagger/ui");
    }

    [Theory]
    [InlineData("qualquer-coisa")]
    [InlineData("api/inexistente")]
    public void Run_ComCaminhoDesconhecido_DeveResponder404SemRedirecionar(string caminho)
    {
        var resposta = (FakeHttpResponseData)_funcao.Run(new FakeHttpRequestData(), caminho);

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
        Location(resposta).Should().BeNull();
    }
}
