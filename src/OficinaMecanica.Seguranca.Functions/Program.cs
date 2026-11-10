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
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

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

// Mesmo desenho dos outros servicos: so liga a telemetria quando Otel:Endpoint esta configurado (Otel__Endpoint no app
// setting). Aqui o endpoint e o OTLP do proprio New Relic, e nao o Collector do AKS, porque a Function App roda fora do
// cluster e nao alcanca o Service interno dele; por isso existe Otel:Headers, que leva o "api-key=<license key>" (vem de um
// segredo do Key Vault via Key Vault Reference).
var otelEndpoint = builder.Configuration["Otel:Endpoint"];
if (!string.IsNullOrWhiteSpace(otelEndpoint))
{
    var otelHeaders = builder.Configuration["Otel:Headers"];
    void ExportarOtlp(OtlpExporterOptions otlp)
    {
        otlp.Endpoint = new Uri(otelEndpoint);
        if (!string.IsNullOrWhiteSpace(otelHeaders))
            otlp.Headers = otelHeaders;
    }

    builder.Services.AddOpenTelemetry()
        .UseFunctionsWorkerDefaults()
        .ConfigureResource(resource => resource.AddService("OficinaMecanica.Seguranca"))
        .WithTracing(tracing => tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddSqlClientInstrumentation()
            .AddOtlpExporter(ExportarOtlp))
        .WithMetrics(metrics => metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation()
            .AddOtlpExporter(ExportarOtlp))
        .WithLogging(logging => logging.AddOtlpExporter(ExportarOtlp));
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
