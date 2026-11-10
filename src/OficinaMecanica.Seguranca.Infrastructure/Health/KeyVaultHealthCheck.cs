using Azure.Security.KeyVault.Keys;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace OficinaMecanica.Seguranca.Infrastructure.Health;

// A chave RSA do Key Vault é o que assina os tokens e alimenta o JWKS: sem acesso a ela o login falha, então a Function não
// está pronta. Leio a própria chave (a mesma operação do JWKS), o que prova de uma vez a rede, a identidade e a permissão.
// O tempo é limitado para a probe não ficar pendurada se o Key Vault demorar.
public sealed class KeyVaultHealthCheck : IHealthCheck
{
    private static readonly TimeSpan LimiteDaChamada = TimeSpan.FromSeconds(5);

    private readonly KeyClient _cliente;
    private readonly string _nomeDaChave;

    public KeyVaultHealthCheck(KeyClient cliente, IConfiguration configuration)
    {
        _cliente = cliente;
        _nomeDaChave = configuration["KeyVault:RsaKeyName"]!;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        using var limite = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limite.CancelAfter(LimiteDaChamada);

        try
        {
            await _cliente.GetKeyAsync(_nomeDaChave, cancellationToken: limite.Token);
            return HealthCheckResult.Healthy("Chave de assinatura acessível no Key Vault.");
        }
        // Se quem cancelou foi o chamador (a probe abortou), o cancelamento sobe: não relato um Key Vault fora do ar que
        // não foi medido. O estouro do meu próprio limite cai aqui e vira Unhealthy.
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy("Não foi possível acessar a chave de assinatura no Key Vault.", ex);
        }
    }
}
