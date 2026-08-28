using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Azure.Functions.Worker.OpenTelemetry;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OficinaMecanica.Seguranca.Application;
using OficinaMecanica.Seguranca.Functions.Middleware;
using OficinaMecanica.Seguranca.Infrastructure;
using OficinaMecanica.Seguranca.Infrastructure.Data;
using OficinaMecanica.Seguranca.Infrastructure.Services;
using OpenTelemetry;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication()
    .UseMiddleware<ExceptionHandlingMiddleware>();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Capturado antes do Build() porque o IHost retornado pelo builder de uma Function isolated-worker
// nao expoe Environment direto (diferente do WebApplication do ASP.NET Core) - builder.Environment
// e o unico lugar onde da pra ler isso. No isolated worker, quem equivale ao ASPNETCORE_ENVIRONMENT
// e a variavel AZURE_FUNCTIONS_ENVIRONMENT, que o "func start" ja seta como "Development" sozinho.
var isDevelopment = builder.Environment.IsDevelopment();

if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING")))
{
    builder.Services.AddOpenTelemetry()
        .UseFunctionsWorkerDefaults()
        .UseAzureMonitorExporter();
}

var app = builder.Build();

if (isDevelopment)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (db.Database.IsRelational())
        db.Database.Migrate();
    else
        db.Database.EnsureCreated();
}

// Seed do usuario admin inicial roda de forma sincrona no startup, antes do host comecar a atender
// requisicoes - assim a primeira invocacao ja encontra o usuario admin criado, sem depender de um
// passo manual/externo (SeedAdminService ja e idempotente, so cria se nao existir nenhum usuario).
using (var scope = app.Services.CreateScope())
{
    var seedService = scope.ServiceProvider.GetRequiredService<SeedAdminService>();
    await seedService.SeedAsync();
}

app.Run();
