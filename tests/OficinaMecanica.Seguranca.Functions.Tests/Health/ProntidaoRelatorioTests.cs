using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OficinaMecanica.Seguranca.Functions.Health;

namespace OficinaMecanica.Seguranca.Functions.Tests.Health;

public class ProntidaoRelatorioTests
{
    private static HealthReport Relatorio(params (string Nome, HealthStatus Status, string? Descricao, Exception? Excecao)[] itens) =>
        new(
            itens.ToDictionary(i => i.Nome, i => new HealthReportEntry(i.Status, i.Descricao, TimeSpan.Zero, i.Excecao, null)),
            TimeSpan.Zero);

    [Theory]
    [InlineData(HealthStatus.Healthy, HttpStatusCode.OK)]
    [InlineData(HealthStatus.Degraded, HttpStatusCode.OK)]
    [InlineData(HealthStatus.Unhealthy, HttpStatusCode.ServiceUnavailable)]
    public void CodigoHttp_SoUnhealthyTiraAFunctionDeRotacao(HealthStatus status, HttpStatusCode esperado)
    {
        var relatorio = Relatorio(("sqlserver", status, "descrição", null));

        ProntidaoRelatorio.CodigoHttp(relatorio).Should().Be(esperado);
    }

    [Fact]
    public void Serializar_DeveTrazerOStatusAgregadoEODeCadaCheckNoFormatoDosOutrosServicos()
    {
        var relatorio = Relatorio(
            ("sqlserver", HealthStatus.Healthy, "Conexão com o banco de dados estabelecida.", null),
            ("keyvault", HealthStatus.Unhealthy, "Não foi possível acessar a chave de assinatura no Key Vault.", null));

        var json = JsonDocument.Parse(ProntidaoRelatorio.Serializar(relatorio)).RootElement;

        json.GetProperty("status").GetString().Should().Be("Unhealthy");
        json.GetProperty("checks").GetProperty("sqlserver").GetProperty("status").GetString().Should().Be("Healthy");
        json.GetProperty("checks").GetProperty("keyvault").GetProperty("status").GetString().Should().Be("Unhealthy");
        json.GetProperty("checks").GetProperty("sqlserver").GetProperty("descricao").GetString()
            .Should().Be("Conexão com o banco de dados estabelecida.");
    }

    [Fact]
    public void Serializar_DeveManterOsAcentosLegiveisENuncaVazarAExcecao()
    {
        var excecao = new InvalidOperationException("falha em svsfiap.database.windows.net");
        var relatorio = Relatorio(("sqlserver", HealthStatus.Unhealthy, "Não foi possível conectar.", excecao));

        var texto = ProntidaoRelatorio.Serializar(relatorio);

        texto.Should().Contain("Não foi possível conectar.");
        texto.Should().NotContain("svsfiap").And.NotContain("InvalidOperationException");
    }
}
