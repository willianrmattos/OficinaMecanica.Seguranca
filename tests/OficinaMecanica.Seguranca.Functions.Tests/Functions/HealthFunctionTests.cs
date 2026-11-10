using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OficinaMecanica.Seguranca.Functions.Functions;
using OficinaMecanica.Seguranca.Functions.Tests.Support;

namespace OficinaMecanica.Seguranca.Functions.Tests.Functions;

public class HealthFunctionTests
{
    private static HealthFunction Funcao(Action<IHealthChecksBuilder> checks)
    {
        var servicos = new ServiceCollection();
        servicos.AddLogging();
        checks(servicos.AddHealthChecks());
        return new HealthFunction(servicos.BuildServiceProvider().GetRequiredService<HealthCheckService>());
    }

    private static string[] Pronto => new[] { "ready" };

    [Fact]
    public async Task Liveness_DeveResponder200HealthyEmTextoSemOlharDependencias()
    {
        // Um check que falha de propósito: o /health não pode depender de nenhuma dependência.
        var funcao = Funcao(c => c.AddCheck("sqlserver", () => HealthCheckResult.Unhealthy("fora"), Pronto));

        var resposta = (FakeHttpResponseData)await funcao.Run(new FakeHttpRequestData(), CancellationToken.None);

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        resposta.Texto().Should().Be("Healthy");
        resposta.ContentType.Should().StartWith("text/plain");
    }

    [Fact]
    public async Task Prontidao_DependenciasSaudaveis_DeveResponder200ComOJsonDeCadaCheck()
    {
        var funcao = Funcao(c => c
            .AddCheck("sqlserver", () => HealthCheckResult.Healthy("Conexão com o banco de dados estabelecida."), Pronto)
            .AddCheck("keyvault", () => HealthCheckResult.Healthy("Chave de assinatura acessível no Key Vault."), Pronto));

        var resposta = (FakeHttpResponseData)await funcao.Ready(new FakeHttpRequestData(), CancellationToken.None);

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        resposta.ContentType.Should().StartWith("application/json");
        var json = JsonDocument.Parse(resposta.Texto()).RootElement;
        json.GetProperty("status").GetString().Should().Be("Healthy");
        json.GetProperty("checks").EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("sqlserver", "keyvault");
    }

    [Fact]
    public async Task Prontidao_AlgumaDependenciaFora_DeveResponder503()
    {
        var funcao = Funcao(c => c
            .AddCheck("sqlserver", () => HealthCheckResult.Healthy("ok"), Pronto)
            .AddCheck("keyvault", () => HealthCheckResult.Unhealthy("Não foi possível acessar a chave de assinatura no Key Vault."), Pronto));

        var resposta = (FakeHttpResponseData)await funcao.Ready(new FakeHttpRequestData(), CancellationToken.None);

        resposta.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        JsonDocument.Parse(resposta.Texto()).RootElement.GetProperty("status").GetString().Should().Be("Unhealthy");
    }

    [Fact]
    public async Task Prontidao_SoAvaliaOsChecksComATagReady()
    {
        // Sem a tag "ready" o check não entra na prontidão, mesmo falhando.
        var funcao = Funcao(c => c
            .AddCheck("sqlserver", () => HealthCheckResult.Healthy("ok"), Pronto)
            .AddCheck("diagnostico", () => HealthCheckResult.Unhealthy("fora")));

        var resposta = (FakeHttpResponseData)await funcao.Ready(new FakeHttpRequestData(), CancellationToken.None);

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonDocument.Parse(resposta.Texto()).RootElement.GetProperty("checks").EnumerateObject()
            .Select(p => p.Name).Should().BeEquivalentTo("sqlserver");
    }
}
