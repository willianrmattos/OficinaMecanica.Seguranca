using System.Security.Cryptography;
using Azure.Core;
using Azure.Identity;
using Azure.Security.KeyVault.Keys;
using OficinaMecanica.Seguranca.Application.Interfaces;
using OficinaMecanica.Seguranca.Domain.Interfaces;
using OficinaMecanica.Seguranca.Infrastructure.Data;
using OficinaMecanica.Seguranca.Infrastructure.Repositories;
using OficinaMecanica.Seguranca.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace OficinaMecanica.Seguranca.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // EF Core
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
                b => b.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)
            ));

        // Unit of Work
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<AppDbContext>());

        // Repositories
        services.AddScoped<IUsuarioRepository, UsuarioRepository>();

        // Password Hasher
        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();

        // Seed do admin inicial
        services.AddScoped<SeedAdminService>();

        // KeyVault:Uri ausente = dev local/testes, sem depender de um Key Vault real na nuvem
        // registro um fallback que assina com uma chave RSA efemera gerada em memoria em vez do
        // caminho de producao via Key Vault. Nao mexo em nada dentro de KeyVaultTokenService/
        // KeyVaultJwksProvider, so decido aqui qual implementacao registrar.
        var keyVaultUriValue = configuration["KeyVault:Uri"];

        if (string.IsNullOrWhiteSpace(keyVaultUriValue))
        {
            // RSA como singleton compartilhado entre TokenService e JwksProvider - os dois
            // precisam da MESMA instancia, senao a chave publica exposta no JWKS nao bateria
            // com a chave privada usada pra assinar.
            services.AddSingleton(RSA.Create(2048));

            services.AddSingleton<ITokenService>(provider => new LocalRsaTokenService(
                provider.GetRequiredService<RSA>(),
                configuration.GetValue("JwtSettings:ExpiracaoMinutos", 60),
                configuration["JwtSettings:Issuer"]!,
                configuration["JwtSettings:Audience"]!));

            services.AddSingleton<IJwksProvider>(provider => new LocalRsaJwksProvider(
                provider.GetRequiredService<RSA>()));
        }
        else
        {
            // Key Vault - a chave RSA privada nunca sai de la, so e usada via CryptographyClient
            var keyVaultUri = new Uri(keyVaultUriValue);
            var credential = new DefaultAzureCredential();
            var keyName = configuration["KeyVault:RsaKeyName"]!;

            services.AddSingleton(new KeyClient(keyVaultUri, credential));
            services.AddSingleton<TokenCredential>(credential);

            // Token Service (assinatura RS256 remota via Key Vault)
            services.AddSingleton<ITokenService>(provider => new KeyVaultTokenService(
                provider.GetRequiredService<KeyClient>(),
                keyName,
                provider.GetRequiredService<TokenCredential>(),
                configuration.GetValue("JwtSettings:ExpiracaoMinutos", 60),
                configuration["JwtSettings:Issuer"]!,
                configuration["JwtSettings:Audience"]!));

            // JWKS Provider (chave publica exposta pro validador do JWT)
            services.AddSingleton<IJwksProvider>(provider => new KeyVaultJwksProvider(
                provider.GetRequiredService<KeyClient>(),
                keyName));
        }

        return services;
    }
}
