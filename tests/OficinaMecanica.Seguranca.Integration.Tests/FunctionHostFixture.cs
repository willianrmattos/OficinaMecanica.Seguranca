using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Data.SqlClient;

namespace OficinaMecanica.Seguranca.Integration.Tests;

// Fixture de colecao xunit - sobe TODA a infraestrutura real (Azurite + Azure Functions host via
// "func start" + LocalDB) uma unica vez e reaproveita entre todos os testes da classe, ja que cada
// start desses processos leva alguns segundos (o proprio "func start" precisa compilar o worker
// isolado e rodar a migration do EF Core no primeiro request). Nada aqui e fake/mock - os testes que
// consomem essa fixture batem via HttpClient num host real, contra um SQL Server real (LocalDB) e um
// storage account real (Azurite), exatamente como rodaria em producao (so trocando Key Vault por RSA
// local, que ja e o fallback automatico da propria Infrastructure quando "KeyVault:Uri" nao esta setado).
public class FunctionHostFixture : IAsyncLifetime
{
    // Portas alternativas de proposito - nao quero colidir com uma instancia de Azurite/func que
    // porventura ja esteja rodando na maquina do desenvolvedor nas portas padrao (10000/10001/10002
    // e 7071 respectivamente).
    private const int BlobPort = 11000;
    private const int QueuePort = 11001;
    private const int TablePort = 11002;
    private const int FuncPort = 7099;

    private const string DatabaseName = "SegurancaDb_IntegrationTests";
    private const string LocalDbConnectionString =
        $"Server=(localdb)\\MSSQLLocalDB;Database={DatabaseName};Trusted_Connection=True;TrustServerCertificate=True;";
    // Conexao separada, sem "Initial Catalog" nenhum (cai no master por padrao) - usada so pra
    // drop/create do banco de teste, que nao pode ser feito enquanto conectado a ele mesmo.
    private const string MasterConnectionString =
        "Server=(localdb)\\MSSQLLocalDB;Trusted_Connection=True;TrustServerCertificate=True;";

    public const string SeedAdminCpf = "52998224725";
    public const string SeedAdminSenhaInicial = "SenhaForte!Teste123";
    public const string JwtIssuer = "seguranca-integration-tests";
    public const string JwtAudience = "seguranca-integration-tests-audience";
    public const int JwtExpiracaoMinutos = 60;

    public HttpClient Client { get; private set; } = null!;

    private readonly string _azuriteDataDirectory =
        Path.Combine(Path.GetTempPath(), "oficinamecanica-seguranca-azurite-tests", Guid.NewGuid().ToString("N"));

    private Process? _azuriteProcess;
    private Process? _funcProcess;
    private readonly StringBuilder _funcOutput = new();
    private readonly StringBuilder _azuriteOutput = new();
    private readonly object _funcOutputLock = new();
    private readonly object _azuriteOutputLock = new();

    public async Task InitializeAsync()
    {
        var solutionRoot = EncontrarRaizDaSolucao();
        var functionsProjectPath = Path.Combine(solutionRoot, "src", "OficinaMecanica.Seguranca.Functions",
            "OficinaMecanica.Seguranca.Functions.csproj");
        var functionsOutputDir = Path.Combine(solutionRoot, "src", "OficinaMecanica.Seguranca.Functions",
            "bin", "Debug", "net8.0");

        // 1) Builda o projeto Functions explicitamente (nao confio soh na ProjectReference do proprio
        // projeto de teste - quero garantir que o binario em bin/Debug/net8.0, que e o que o "func
        // start" de fato vai rodar, esta atualizado com o codigo-fonte atual).
        await BuildarProjetoFunctionsAsync(functionsProjectPath);

        // 2) Garante um banco de teste limpo - dropa se ja existir de uma execucao anterior (ex: um
        // teardown que nao rodou por causa de um crash) e deixa a proria Function recriar via
        // Database.Migrate() no proprio startup da Function (Program.cs), igual rodaria em qualquer ambiente novo.
        await RecriarBancoDeTesteAsync();

        // 3) Sobe o Azurite (emulador de storage) - o Functions host precisa de um AzureWebJobsStorage
        // valido mesmo sem nenhuma Function baseada em trigger de storage, ja que o proprio host usa
        // esse storage internamente (locks, etc).
        Directory.CreateDirectory(_azuriteDataDirectory);
        _azuriteProcess = IniciarAzurite();
        await EsperarPortaAbrirAsync(BlobPort, TimeSpan.FromSeconds(15), _azuriteProcess, _azuriteOutputLock, _azuriteOutput);
        await EsperarPortaAbrirAsync(QueuePort, TimeSpan.FromSeconds(15), _azuriteProcess, _azuriteOutputLock, _azuriteOutput);
        await EsperarPortaAbrirAsync(TablePort, TimeSpan.FromSeconds(15), _azuriteProcess, _azuriteOutputLock, _azuriteOutput);

        // 4) Sobe o host real do Azure Functions (func start) a partir do diretorio de SAIDA do build
        // (nao da pasta fonte) - passar a pasta fonte deixa o "func" ambiguo sobre qual .csproj usar
        // quando ha mais de um projeto por perto, alem de exigir compilar de novo por conta propria.
        _funcProcess = IniciarFuncHost(functionsOutputDir);

        Client = new HttpClient { BaseAddress = new Uri($"http://localhost:{FuncPort}") };

        // 5) So devolve o controle pros testes quando o host realmente estiver respondendo - o
        // primeiro start e mais lento por causa da migration do EF Core rodando contra o LocalDB.
        await EsperarHostFicarProntoAsync(TimeSpan.FromSeconds(60));
    }

    public async Task DisposeAsync()
    {
        Client?.Dispose();

        // entireProcessTree: true e essencial aqui - tanto o "func start" quanto o "azurite" sao
        // shims (cmd.exe rodando um script Node) que sobem processos filhos de verdade (o worker
        // isolado dotnet.exe, no caso do func) - matar so o processo pai deixaria esses filhos orfaos
        // rodando indefinidamente, ocupando as portas pra proxima execucao dos testes.
        MatarProcesso(_funcProcess);
        MatarProcesso(_azuriteProcess);

        await RecriarBancoDeTesteAsync(apenasDropar: true);

        try
        {
            if (Directory.Exists(_azuriteDataDirectory))
                Directory.Delete(_azuriteDataDirectory, recursive: true);
        }
        catch
        {
            // Best-effort - arquivo de log do Azurite pode ainda estar com handle aberto por um
            // instante apos o Kill(); nao vale a pena falhar o teardown do teste por causa disso.
        }
    }

    // Sobe a partir do diretorio de saida do build (AppContext.BaseDirectory) e procura o .sln subindo
    // na arvore - mais robusto do que contar niveis fixos de pasta, que quebraria se a configuracao de
    // build mudasse (Debug/Release) ou o projeto de teste rodasse de outro lugar (ex: "dotnet test" vs
    // Visual Studio).
    private static string EncontrarRaizDaSolucao()
    {
        var diretorioAtual = new DirectoryInfo(AppContext.BaseDirectory);

        while (diretorioAtual != null && diretorioAtual.GetFiles("*.sln").Length == 0)
            diretorioAtual = diretorioAtual.Parent;

        if (diretorioAtual == null)
            throw new InvalidOperationException(
                "Nao foi possivel localizar o arquivo .sln subindo a partir de " + AppContext.BaseDirectory);

        return diretorioAtual.FullName;
    }

    private static async Task BuildarProjetoFunctionsAsync(string csprojPath)
    {
        var processInfo = new ProcessStartInfo("dotnet", $"build \"{csprojPath}\" -c Debug")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(processInfo)
            ?? throw new InvalidOperationException("Nao foi possivel iniciar 'dotnet build' do projeto Functions.");

        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
            throw new InvalidOperationException(
                $"'dotnet build' do projeto Functions falhou (exit code {process.ExitCode}).\n--- stdout ---\n{stdout}\n--- stderr ---\n{stderr}");
    }

    private static async Task RecriarBancoDeTesteAsync(bool apenasDropar = false)
    {
        await using var connection = new SqlConnection(MasterConnectionString);
        await connection.OpenAsync();

        // SINGLE_USER WITH ROLLBACK IMMEDIATE derruba qualquer conexao residual antes do DROP -
        // sem isso, um teardown anterior que nao tenha fechado a conexao do EF Core deixaria o DROP
        // preso esperando. "DROP DATABASE IF EXISTS" (SQL Server 2016+) evita ter que checar
        // sys.databases na mao.
        var sql = $@"
IF EXISTS (SELECT 1 FROM sys.databases WHERE name = N'{DatabaseName}')
BEGIN
    ALTER DATABASE [{DatabaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [{DatabaseName}];
END";

        await using (var command = new SqlCommand(sql, connection))
            await command.ExecuteNonQueryAsync();

        // No teardown so preciso dropar (limpeza) - quem recria o banco no setup e a propria Function
        // via Database.Migrate() no seu startup, entao nao faco CREATE DATABASE aqui.
        if (apenasDropar)
            return;
    }

    private Process IniciarAzurite()
    {
        // "azurite" na verdade e um shim .cmd (instalado via npm) - Process.Start direto sem passar
        // por cmd.exe nao resolve a extensao .cmd (CreateProcess do Windows nao faz a expansao de
        // PATHEXT que o cmd.exe faz), entao preciso invocar via "cmd /c".
        var processInfo = new ProcessStartInfo("cmd.exe",
            $"/c azurite --silent --location \"{_azuriteDataDirectory}\" " +
            $"--blobPort {BlobPort} --queuePort {QueuePort} --tablePort {TablePort}")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        var process = Process.Start(processInfo)
            ?? throw new InvalidOperationException("Nao foi possivel iniciar o Azurite.");

        // Guardo a saida (mesmo padrao do "func start" abaixo) pra anexar numa excecao caso o
        // processo morra sozinho antes das portas abrirem - por exemplo uma porta ja ocupada por
        // outro Azurite/processo alheio na maquina, que sem isso viraria so um timeout sem pista.
        process.OutputDataReceived += (_, e) => { if (e.Data != null) lock (_azuriteOutputLock) _azuriteOutput.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) lock (_azuriteOutputLock) _azuriteOutput.AppendLine("[stderr] " + e.Data); };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        return process;
    }

    private Process IniciarFuncHost(string workingDirectory)
    {
        // Connection string com barra invertida dupla (\\) porque essa string vira argumento de um
        // ProcessStartInfo.Environment (nao passa por interpretacao de shell nenhuma) - uma unica
        // barra bastaria no valor final, mas aqui e literal C#.
        var connectionString =
            "Server=(localdb)\\MSSQLLocalDB;Database=" + DatabaseName +
            ";Trusted_Connection=True;TrustServerCertificate=True;";

        // Storage account bem conhecida do Azurite (mesma chave publica documentada pela Microsoft
        // pro emulador) - so aponto os tres endpoints pras portas alternativas escolhidas em vez de
        // usar o atalho "UseDevelopmentStorage=true" (que sempre assume as portas padrao 10000/01/02).
        var azureWebJobsStorage =
            "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;" +
            "AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;" +
            $"BlobEndpoint=http://127.0.0.1:{BlobPort}/devstoreaccount1;" +
            $"QueueEndpoint=http://127.0.0.1:{QueuePort}/devstoreaccount1;" +
            $"TableEndpoint=http://127.0.0.1:{TablePort}/devstoreaccount1;";

        var processInfo = new ProcessStartInfo("cmd.exe", $"/c func start --port {FuncPort}")
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        // Variaveis de ambiente do processo filho tem precedencia sobre o local.settings.json (o
        // proprio func confirma isso no log: "Skipping 'X' from local settings as it's already
        // defined in current environment variables") - mais facil de controlar por execucao de teste
        // do que editar um arquivo compartilhado. IMPORTANTE: "KeyVault:Uri" fica de proposito FORA
        // dessa lista - a ausencia dele e o que ativa o fallback LocalRsaTokenService/
        // LocalRsaJwksProvider (ver Infrastructure/DependencyInjection.cs).
        processInfo.EnvironmentVariables["AzureWebJobsStorage"] = azureWebJobsStorage;
        processInfo.EnvironmentVariables["FUNCTIONS_WORKER_RUNTIME"] = "dotnet-isolated";
        processInfo.EnvironmentVariables["ConnectionStrings__DefaultConnection"] = connectionString;
        processInfo.EnvironmentVariables["JwtSettings__Issuer"] = JwtIssuer;
        processInfo.EnvironmentVariables["JwtSettings__Audience"] = JwtAudience;
        processInfo.EnvironmentVariables["JwtSettings__ExpiracaoMinutos"] = JwtExpiracaoMinutos.ToString();
        processInfo.EnvironmentVariables["SeedAdmin__Cpf"] = SeedAdminCpf;
        processInfo.EnvironmentVariables["SeedAdmin__SenhaInicial"] = SeedAdminSenhaInicial;

        var process = Process.Start(processInfo)
            ?? throw new InvalidOperationException("Nao foi possivel iniciar o 'func start'.");

        // Guardo a saida pra anexar na excecao caso o polling do /health estoure o timeout - sem
        // isso, um "func start" que falhou silenciosamente (ex: porta ocupada, erro de configuracao)
        // vira so um timeout sem nenhuma pista do motivo real.
        process.OutputDataReceived += (_, e) => { if (e.Data != null) lock (_funcOutputLock) _funcOutput.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) lock (_funcOutputLock) _funcOutput.AppendLine("[stderr] " + e.Data); };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        return process;
    }

    private async Task EsperarHostFicarProntoAsync(TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);

        while (!cts.IsCancellationRequested)
        {
            if (_funcProcess!.HasExited)
            {
                string saida;
                lock (_funcOutputLock) saida = _funcOutput.ToString();
                throw new InvalidOperationException(
                    $"O processo 'func start' encerrou sozinho antes de ficar pronto (exit code {_funcProcess.ExitCode}).\n--- saida ---\n{saida}");
            }

            try
            {
                var response = await Client.GetAsync("/health", cts.Token);
                if (response.StatusCode == HttpStatusCode.OK)
                    return;
            }
            catch
            {
                // Host ainda nao esta aceitando conexoes - normal enquanto o worker isolado sobe;
                // so tento de novo no proximo loop.
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500), cts.Token);
        }

        string saidaTimeout;
        lock (_funcOutputLock) saidaTimeout = _funcOutput.ToString();
        throw new TimeoutException(
            $"O host 'func start' nao respondeu 200 em GET /health dentro de {timeout.TotalSeconds}s.\n--- saida ---\n{saidaTimeout}");
    }

    // "process"/"outputLock"/"output" identificam o processo que deveria estar abrindo essa porta -
    // sem checar HasExited aqui, um Azurite que morre na largada (porta ja ocupada por outro processo
    // alheio na maquina, por exemplo) passaria batido: a porta already-open de quem a esta ocupando
    // faria esse metodo retornar normalmente, mascarando a falha e deixando o processo morto sem dono
    // rastreado (nunca cairia no MatarProcesso do DisposeAsync).
    private static async Task EsperarPortaAbrirAsync(int port, TimeSpan timeout, Process process, object outputLock, StringBuilder output)
    {
        using var cts = new CancellationTokenSource(timeout);

        while (!cts.IsCancellationRequested)
        {
            if (process.HasExited)
            {
                string saida;
                lock (outputLock) saida = output.ToString();
                throw new InvalidOperationException(
                    $"O processo do Azurite encerrou sozinho antes da porta {port} abrir (exit code {process.ExitCode}).\n--- saida ---\n{saida}");
            }

            try
            {
                using var tcpClient = new TcpClient();
                await tcpClient.ConnectAsync(IPAddress.Loopback, port, cts.Token);
                return;
            }
            catch
            {
                await Task.Delay(TimeSpan.FromMilliseconds(300), cts.Token);
            }
        }

        throw new TimeoutException($"A porta {port} (Azurite) nao abriu dentro de {timeout.TotalSeconds}s.");
    }

    private static void MatarProcesso(Process? process)
    {
        if (process == null)
            return;

        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch
        {
            // Best-effort - processo pode ja ter saido sozinho entre o HasExited e o Kill.
        }
        finally
        {
            process.Dispose();
        }
    }
}

// Colecao xunit que amarra a fixture na classe(s) de teste - "ICollectionFixture" garante que
// InitializeAsync/DisposeAsync rodem uma unica vez pra todas as classes marcadas com
// [Collection(FunctionHostCollection.Name)], nao uma vez por classe/teste.
[CollectionDefinition(Name)]
public class FunctionHostCollection : ICollectionFixture<FunctionHostFixture>
{
    public const string Name = "FunctionHost";
}
