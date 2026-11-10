using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Abstractions;
using Microsoft.OpenApi.Models;
using Moq;

namespace OficinaMecanica.Seguranca.Functions.Tests.OpenApi;

public class OpenApiTests
{
    private static Mock<IHttpRequestDataObject> Requisicao(string esquema, string host, IDictionary<string, string>? headers = null)
    {
        var cabecalhos = new HeaderDictionary();
        foreach (var (nome, valor) in headers ?? new Dictionary<string, string>())
            cabecalhos[nome] = valor;

        var requisicao = new Mock<IHttpRequestDataObject>();
        requisicao.SetupGet(r => r.Headers).Returns(cabecalhos);
        requisicao.SetupGet(r => r.Scheme).Returns(esquema);
        requisicao.SetupGet(r => r.Host).Returns(new HostString(host));
        return requisicao;
    }

    private static string UrlDoServidor(IHttpRequestDataObject requisicao)
    {
        var documento = new OpenApiDocument();
        new ServerBasePathDocumentFilter().Apply(requisicao, documento);
        return documento.Servers.Should().ContainSingle().Subject.Url;
    }

    [Fact]
    public void Apply_SemHeadersDeGateway_DeveUsarOEsquemaEOHostDaPropriaRequisicao()
    {
        var url = UrlDoServidor(Requisicao("https", "funcsegurancafiap.azurewebsites.net").Object);

        url.Should().Be("https://funcsegurancafiap.azurewebsites.net");
    }

    [Fact]
    public void Apply_ComHeadersDoGateway_DeveMontarAUrlPublicaDaApim()
    {
        var requisicao = Requisicao("http", "funcsegurancafiap.azurewebsites.net", new Dictionary<string, string>
        {
            ["X-Gateway-Proto"] = "https",
            ["X-Gateway-Host"] = "apimfiap.azure-api.net",
            ["X-Gateway-Prefix"] = "/segurancaserver"
        });

        var url = UrlDoServidor(requisicao.Object);

        url.Should().Be("https://apimfiap.azure-api.net/segurancaserver");
    }

    [Fact]
    public void Apply_ComSoOPrefixoDoGateway_DeveManterEsquemaEHostDaRequisicao()
    {
        var requisicao = Requisicao("https", "localhost:7071", new Dictionary<string, string>
        {
            ["X-Gateway-Prefix"] = "/seguranca"
        });

        UrlDoServidor(requisicao.Object).Should().Be("https://localhost:7071/seguranca");
    }

    [Fact]
    public void Apply_DeveSubstituirOsServidoresQueJaExistiamNoDocumento()
    {
        var documento = new OpenApiDocument { Servers = new List<OpenApiServer> { new() { Url = "http://antigo" } } };

        new ServerBasePathDocumentFilter().Apply(Requisicao("https", "host").Object, documento);

        documento.Servers.Should().ContainSingle().Which.Url.Should().Be("https://host");
    }

    [Fact]
    public void OpcoesDoOpenApi_DevemDescreverOServicoERegistrarOFiltroDeBasePath()
    {
        var opcoes = new OpenApiConfigurationOptions();

        opcoes.Info.Title.Should().Be("OficinaMecanica.Seguranca");
        opcoes.Info.Version.Should().Be("v1");
        opcoes.Info.Description.Should().Contain("/.well-known/jwks.json");
        opcoes.DocumentFilters.Should().ContainSingle().Which.Should().BeOfType<ServerBasePathDocumentFilter>();
    }
}
