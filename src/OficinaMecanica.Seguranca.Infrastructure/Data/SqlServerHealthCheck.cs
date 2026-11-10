using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace OficinaMecanica.Seguranca.Infrastructure.Data;

// O SQL Server é a dependência sem a qual o login não funciona (é onde moram os usuários): com o banco fora, a Function não
// está pronta. O tempo é limitado porque o banco serverless pode demorar a acordar e a probe não pode ficar pendurada.
public sealed class SqlServerHealthCheck : IHealthCheck
{
    private static readonly TimeSpan LimiteDaConexao = TimeSpan.FromSeconds(3);

    private readonly AppDbContext _db;

    public SqlServerHealthCheck(AppDbContext db)
    {
        _db = db;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        using var limite = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limite.CancelAfter(LimiteDaConexao);

        try
        {
            if (await _db.Database.CanConnectAsync(limite.Token))
                return HealthCheckResult.Healthy("Conexão com o banco de dados estabelecida.");

            // O CanConnectAsync engole o cancelamento e devolve falso: se quem cancelou foi o chamador (a probe abortou),
            // propago o cancelamento em vez de relatar um banco fora do ar que não foi medido.
            cancellationToken.ThrowIfCancellationRequested();
            return HealthCheckResult.Unhealthy("Não foi possível conectar ao banco de dados.");
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy("Não foi possível conectar ao banco de dados.", ex);
        }
    }
}
