using Azure;
using Azure.Security.KeyVault.Keys;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Moq;
using OficinaMecanica.Seguranca.Infrastructure.Data;
using OficinaMecanica.Seguranca.Infrastructure.Health;

namespace OficinaMecanica.Seguranca.Functions.Tests.Health;

public class ChecksDeDependenciaTests
{
    // Porta 1 recusa a conexao na hora: o banco "fora do ar" nao depende de rede nem de Docker.
    private const string ConexaoRecusada = "Server=tcp:127.0.0.1,1;Database=x;User Id=x;Password=x;Connect Timeout=1;Encrypt=false";

    private static AppDbContext BancoEmMemoria() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, Mock.Of<IMediator>());

    private static AppDbContext BancoInacessivel() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(ConexaoRecusada).Options, Mock.Of<IMediator>());

    private static IConfiguration Configuracao() =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["KeyVault:RsaKeyName"] = "chave-de-teste" }).Build();

    [Fact]
    public async Task SqlServer_BancoAcessivel_DeveSerHealthy()
    {
        using var db = BancoEmMemoria();

        var resultado = await new SqlServerHealthCheck(db).CheckHealthAsync(new HealthCheckContext());

        resultado.Status.Should().Be(HealthStatus.Healthy);
        resultado.Description.Should().Contain("estabelecida");
    }

    [Fact]
    public async Task SqlServer_BancoInacessivel_DeveSerUnhealthy()
    {
        using var db = BancoInacessivel();

        var resultado = await new SqlServerHealthCheck(db).CheckHealthAsync(new HealthCheckContext());

        resultado.Status.Should().Be(HealthStatus.Unhealthy);
        resultado.Description.Should().Be("Não foi possível conectar ao banco de dados.");
    }

    [Fact]
    public async Task SqlServer_ChamadorCancelou_DevePropagarOCancelamentoEmVezDeRelatarBancoForaDoAr()
    {
        using var db = BancoInacessivel();
        using var cancelado = new CancellationTokenSource();
        cancelado.Cancel();

        var acao = () => new SqlServerHealthCheck(db).CheckHealthAsync(new HealthCheckContext(), cancelado.Token);

        await acao.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task KeyVault_ChaveAcessivel_DeveSerHealthy()
    {
        var cliente = new Mock<KeyClient>();
        cliente.Setup(c => c.GetKeyAsync("chave-de-teste", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue<KeyVaultKey>(null!, Mock.Of<Response>()));

        var resultado = await new KeyVaultHealthCheck(cliente.Object, Configuracao()).CheckHealthAsync(new HealthCheckContext());

        resultado.Status.Should().Be(HealthStatus.Healthy);
        cliente.Verify(c => c.GetKeyAsync("chave-de-teste", null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task KeyVault_AcessoNegado_DeveSerUnhealthySemVazarADescricaoDaExcecao()
    {
        var cliente = new Mock<KeyClient>();
        cliente.Setup(c => c.GetKeyAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException(403, "acesso negado em kvfiap.vault.azure.net"));

        var resultado = await new KeyVaultHealthCheck(cliente.Object, Configuracao()).CheckHealthAsync(new HealthCheckContext());

        resultado.Status.Should().Be(HealthStatus.Unhealthy);
        resultado.Description.Should().Be("Não foi possível acessar a chave de assinatura no Key Vault.");
        resultado.Description.Should().NotContain("kvfiap");
    }

    [Fact]
    public async Task KeyVault_ChamadorCancelou_DevePropagarOCancelamento()
    {
        var cliente = new Mock<KeyClient>();
        cliente.Setup(c => c.GetKeyAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .Returns<string, string?, CancellationToken>((_, _, ct) => Task.FromCanceled<Response<KeyVaultKey>>(ct));
        using var cancelado = new CancellationTokenSource();
        cancelado.Cancel();

        var acao = () => new KeyVaultHealthCheck(cliente.Object, Configuracao()).CheckHealthAsync(new HealthCheckContext(), cancelado.Token);

        await acao.Should().ThrowAsync<OperationCanceledException>();
    }
}
