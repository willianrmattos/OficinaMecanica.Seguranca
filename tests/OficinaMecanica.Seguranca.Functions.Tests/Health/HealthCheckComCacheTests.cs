using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using OficinaMecanica.Seguranca.Infrastructure.Health;

namespace OficinaMecanica.Seguranca.Functions.Tests.Health;

public class HealthCheckComCacheTests
{
    private sealed class CheckContador : IHealthCheck
    {
        public int Chamadas { get; private set; }
        public Func<int, HealthCheckResult> Resultado { get; set; } = _ => HealthCheckResult.Healthy("ok");
        public Exception? Falha { get; set; }

        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            Chamadas++;
            if (Falha is not null)
                throw Falha;
            return Task.FromResult(Resultado(Chamadas));
        }
    }

    private static HealthCheckComCache Criar(CheckContador interno, FakeTimeProvider relogio, int cacheEmSegundos) =>
        new(interno, new EstadoDoCacheDeProntidao(), Options.Create(new OpcoesDaProntidao { ProntidaoCacheSegundos = cacheEmSegundos }), relogio);

    [Fact]
    public async Task DentroDaJanela_DeveDevolverOResultadoGuardadoSemReavaliar()
    {
        var interno = new CheckContador();
        var check = Criar(interno, new FakeTimeProvider(), 5);

        await check.CheckHealthAsync(new HealthCheckContext());
        await check.CheckHealthAsync(new HealthCheckContext());
        await check.CheckHealthAsync(new HealthCheckContext());

        interno.Chamadas.Should().Be(1);
    }

    [Fact]
    public async Task DepoisDaValidade_DeveReavaliarEMostrarOResultadoNovo()
    {
        var relogio = new FakeTimeProvider();
        var interno = new CheckContador
        {
            Resultado = chamada => chamada == 1 ? HealthCheckResult.Healthy("primeira") : HealthCheckResult.Unhealthy("segunda")
        };
        var check = Criar(interno, relogio, 5);

        var primeira = await check.CheckHealthAsync(new HealthCheckContext());
        relogio.Advance(TimeSpan.FromSeconds(6));
        var segunda = await check.CheckHealthAsync(new HealthCheckContext());

        primeira.Description.Should().Be("primeira");
        segunda.Status.Should().Be(HealthStatus.Unhealthy);
        interno.Chamadas.Should().Be(2);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task CacheDesligado_DeveAvaliarEmToda_Chamada(int cacheEmSegundos)
    {
        var interno = new CheckContador();
        var check = Criar(interno, new FakeTimeProvider(), cacheEmSegundos);

        await check.CheckHealthAsync(new HealthCheckContext());
        await check.CheckHealthAsync(new HealthCheckContext());

        interno.Chamadas.Should().Be(2);
    }

    [Fact]
    public async Task ExcecaoDoCheck_NaoDeveSerGuardadaNoCache()
    {
        var interno = new CheckContador { Falha = new OperationCanceledException() };
        var check = Criar(interno, new FakeTimeProvider(), 5);

        var primeira = () => check.CheckHealthAsync(new HealthCheckContext());
        await primeira.Should().ThrowAsync<OperationCanceledException>();

        interno.Falha = null;
        var segunda = await check.CheckHealthAsync(new HealthCheckContext());

        segunda.Status.Should().Be(HealthStatus.Healthy);
        interno.Chamadas.Should().Be(2);
    }

    [Fact]
    public async Task Registro_DeveAvaliarOCheckRealDentroDoCachePeloHealthCheckService()
    {
        var servicos = new ServiceCollection();
        servicos.AddLogging();
        servicos.AddSingleton<FakeTimeProvider>();
        servicos.AddSingleton<TimeProvider>(sp => sp.GetRequiredService<FakeTimeProvider>());
        servicos.AddOptions<OpcoesDaProntidao>().Configure(o => o.ProntidaoCacheSegundos = 5);
        servicos.AddSingleton<CheckContador>();
        servicos.AddHealthChecks().AddCheckComCache<CheckContador>("dependencia", HealthStatus.Unhealthy, new[] { "ready" });
        using var provedor = servicos.BuildServiceProvider();
        var servico = provedor.GetRequiredService<HealthCheckService>();

        var primeiro = await servico.CheckHealthAsync(c => c.Tags.Contains("ready"));
        var segundo = await servico.CheckHealthAsync(c => c.Tags.Contains("ready"));

        primeiro.Entries.Should().ContainKey("dependencia");
        segundo.Status.Should().Be(HealthStatus.Healthy);
        provedor.GetRequiredService<CheckContador>().Chamadas.Should().Be(1);
    }
}
