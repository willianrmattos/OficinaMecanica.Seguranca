# OficinaMecanica.Seguranca

Servico de autenticacao/autorizacao do ecossistema OficinaMecanica — Azure
Function (.NET 8, isolated worker) que emite tokens JWT assinados com
**RS256** e expoe um endpoint **JWKS** para que outras APIs validem tokens
usando somente a chave publica, sem segredo compartilhado.

Extraido do monolito [OficinaMecanica](../OficinaMecanica), que ate entao
tinha seu proprio `TokenService`/`AuthController` (JWT simetrico, 1 admin
hardcoded em config).

## Diagramas

| Documento | Arquivo |
|-----------|---------|
| Clean Architecture (camadas e dependencias) | [docs/clean-architecture.puml](docs/clean-architecture.puml) |
| C4 Nivel 1 - Contexto do sistema | [docs/c4-nivel1-contexto.puml](docs/c4-nivel1-contexto.puml) |
| C4 Nivel 2 - Containers | [docs/c4-nivel2-container.puml](docs/c4-nivel2-container.puml) |
| C4 Nivel 3 - Componentes | [docs/c4-nivel3-componente.puml](docs/c4-nivel3-componente.puml) |
| Diagrama de Sequencia (fluxo de autenticacao) | [docs/diagramas/diagrama-de-sequencia.jpg](docs/diagramas/diagrama-de-sequencia.jpg) |

## RFCs (decisoes de "por que essa direcao")

| RFC | Titulo |
|-----|--------|
| 0001 | [Estrategia de autenticacao (JWT RS256/JWKS via servico dedicado)](docs/rfc/0001-estrategia-de-autenticacao.md) |

## ADRs (decisoes tecnicas pontuais)

| ADR | Titulo |
|-----|--------|
| 0001 | [Function App Consumption sem deployment slots](docs/adr/0001-function-app-consumption-sem-slots.md) |

## Estrutura

Clean Architecture (Domain -> Application -> Infrastructure -> Functions),
DDD, CQRS via MediatR — mesmos padroes do repositorio `OficinaMecanica`. Ver
`CLAUDE.md` para detalhes.

## Infraestrutura

Toda a infraestrutura deste servico e provisionada em
[OficinaMecanica.Infra](../OficinaMecanica.Infra) (repositorio irmao,
centraliza o Terraform de todo o ecossistema) — este repositorio nao tem
pasta `infra/` propria.

## Ambiente de Homologação

A branch `main` (produção) e a branch `release` (homologação) disparam
deploy automático **pra a mesma Function App** (`funcsegurancafiap`) — não
há um ambiente de homologação fisicamente isolado (sem segunda Function
App, sem banco de dados separado).

Essa é uma limitação real de recurso, não uma escolha de melhor prática: o
cluster/assinatura roda numa cota gratuita/limitada (Azure for Students),
sem espaço pra manter uma segunda instância completa da infraestrutura só
pra homologação. Num cenário real de produção, o recomendável seria
infraestrutura dedicada por ambiente (Function App e banco de dados
próprios por ambiente — o tier Consumption usado aqui, aliás, não suporta
[deployment slots](https://learn.microsoft.com/azure/azure-functions/functions-deployment-slots),
que seria a forma nativa do Azure de resolver isso sem duplicar recursos),
pra eliminar qualquer risco de um deploy de teste afetar produção de
verdade. Como este é um projeto de estudo, a separação demonstrada aqui é
só a nível de **processo**: branch protegida, PR obrigatório, deploy
automático disparado por cada branch — não isolamento de infraestrutura.

## CI/CD

O deploy e automatizado via GitHub Actions
([.github/workflows/ci.yml](.github/workflows/ci.yml)), em 2 jobs
sequenciais. Roda automaticamente em todo push/PR pras branches `main`
(producao) e `release` (homologacao - ver
[Ambiente de Homologacao](#ambiente-de-homologação)), ou sob demanda pelo
botao **Run workflow** na aba *Actions* (`workflow_dispatch`).

| Job | Quando roda | O que faz |
|-----|-------------|-----------|
| `build-and-test` | Todo push, PR ou disparo manual | Restore, build, testes Domain + Application |
| `deploy` | So em push direto na `main`/`release` ou disparo manual (nao em PR) | Publica o projeto Functions, autentica no Azure (OIDC), aplica migrations pendentes (`dotnet ef database update`, connection string do Key Vault) e faz deploy via zip (`Azure/functions-action`) na mesma Function App (`funcsegurancafiap`) |

**Autenticacao sem secrets de longa duracao**: o job `deploy` autentica via
**OIDC** (`OficinaMecanica.Infra/github_oidc_seguranca/`) - Federated
Identity Credential restrita a `ref:refs/heads/main` e
`ref:refs/heads/release`, sem client secret armazenado no repositorio.

**Migracao aplicada explicitamente no CI**: `Program.cs` so roda
`Database.Migrate()` automaticamente quando `AZURE_FUNCTIONS_ENVIRONMENT=
Development` (nunca em producao real) - o step `Aplicar migrations` do
`ci.yml` e o unico mecanismo que aplica schema novo no Azure.

## Endpoints

- `POST /login` — autentica e retorna um JWT RS256
- `GET /.well-known/jwks.json` — chave publica pra validacao de tokens
- `GET /health` — health check
- `GET /swagger/ui` — Swagger UI (documentacao interativa dos 3 endpoints acima, via `Microsoft.Azure.Functions.Worker.Extensions.OpenApi`); spec em `GET /openapi/v3.json` (ou `/swagger.json`/`/swagger.yaml`)
- `GET /` — redireciona (302) pro Swagger UI, mesma ideia do `RoutePrefix = string.Empty` do monolito (Swashbuckle); qualquer outro caminho desconhecido continua 404 normalmente (ver `CLAUDE.md` pro motivo de precisar de uma rota catch-all pra isso)

(Sem prefixo `/api` — `host.json` define `routePrefix: ""` de proposito,
pra `/.well-known/jwks.json` ficar exatamente nesse path convencional.)

**Em producao**, o acesso e via APIM (nao direto no hostname da Function
App): https://apimfiap.azure-api.net/segurancaserver/swagger/ui (ver
[API Gateway](../OficinaMecanica.Infra/README.md#api-gateway) no
`OficinaMecanica.Infra`).

## Regras de Negocio

- **Anti user enumeration**: falha de login (usuario inexistente, inativo ou
  senha errada) sempre retorna a mesma mensagem (`"Usuário ou senha
  inválidos."`, HTTP 401) — nao da pra descobrir se um CPF esta cadastrado
  so pela resposta (`LoginCommandHandler.cs`).
- **Login por CPF**: o `Usuario` e identificado pelo CPF (Value Object
  `Cpf`, com validacao de checksum real - nao um campo de texto livre),
  nao por um nome de usuario arbitrario.
- **Usuario admin via seed idempotente**: no cold start da Function,
  `SeedAdminService` verifica se ja existe algum usuario na tabela; se nao
  existir, cria 1 admin com `Cpf`/senha vindos de
  `SeedAdmin:Cpf`/`SeedAdmin:SenhaInicial` (config ou Key Vault, ver
  `CLAUDE.md`). Roda toda vez que o processo sobe, mas so tem efeito uma vez
  — sem CRUD de usuarios ainda (fora do escopo do MVP).
- **Token RS256**: `LoginResponseDto` retorna `token` (JWT), `tipo`
  (`"Bearer"`) e `expiracaoMinutos` (config `JwtSettings:ExpiracaoMinutos`).
  Claims incluem `sub`/`name`/`role`, `iss` (`OficinaMecanica.Seguranca`) e
  `aud` (`OficinaMecanica.Client`) — outras APIs validam so contra esses
  valores + a chave publica do JWKS, sem segredo compartilhado.
- **Rotacao de chave**: o header `kid` do JWT identifica a versao da chave
  usada pra assinar; `JwksFunction` sempre expoe a chave publica
  correspondente, permitindo rotacionar a chave no Key Vault no futuro sem
  invalidar tokens ja emitidos com o `kid` anterior (na producao real — o
  fallback de dev local usa um `kid` fixo, ver `CLAUDE.md` pra uma pegadinha
  conhecida disso).

## Rodando localmente

### Via Docker Compose (recomendado)

Pre-requisito: Docker Desktop. Sobe a Function, um SQL Server local e o
Azurite (emulador de storage), tudo self-contained (sem depender de nenhum
recurso Azure real) — o `KeyVault:Uri` fica de proposito sem valor, o que
ativa o fallback de chave RSA local (ver `CLAUDE.md`).

```bash
cp .env.example .env
# edite o .env com senhas de teste
docker compose up -d
```

- `GET http://localhost:7071/health`
- `POST http://localhost:7071/login` (body `{"cpf":"...","senha":"..."}`)
- `GET http://localhost:7071/.well-known/jwks.json`
- `GET http://localhost:7071/swagger/ui` — Swagger UI (mesmo `routePrefix` vazio via `func start`, so muda a porta se nao for a padrao)
- `GET http://localhost:7071/` — redireciona pro Swagger UI acima

### Via Azure Functions Core Tools (alternativa)

Pre-requisitos: [.NET 8 SDK](https://dotnet.microsoft.com/download),
[Azure Functions Core Tools v4](https://learn.microsoft.com/azure/azure-functions/functions-run-local),
[Azurite](https://learn.microsoft.com/azure/storage/common/storage-use-azurite) (emulador de storage).

```bash
dotnet build
cd src/OficinaMecanica.Seguranca.Functions
func start
```

## Executar Testes

```bash
dotnet test tests/OficinaMecanica.Seguranca.Domain.Tests
dotnet test tests/OficinaMecanica.Seguranca.Application.Tests
```

`Integration.Tests` e um smoke test real (nao mockado): sobe Azurite +
`func start` como processo real + LocalDB, e testa via `HttpClient` comum
contra o host de verdade (isolated worker nao tem equivalente ao
`WebApplicationFactory` do ASP.NET Core — ver `CLAUDE.md`). Requer
[Azure Functions Core Tools v4](https://learn.microsoft.com/azure/azure-functions/functions-run-local)
e [Azurite](https://learn.microsoft.com/azure/storage/common/storage-use-azurite)
instalados localmente:

```bash
dotnet test tests/OficinaMecanica.Seguranca.Integration.Tests
```

## Stack Tecnologica

| Categoria | Tecnologia | Versao |
|-----------|-----------|--------|
| Runtime | .NET / Azure Functions Worker (isolated) | 8.0 / 2.52.0 |
| ORM | Entity Framework Core (SQL Server) | 8.0.0 |
| CQRS | MediatR | 12.2.0 |
| Validacao | FluentValidation | 11.9.0 |
| Hash de senha | BCrypt.Net-Next | 4.2.0 |
| Assinatura RS256 | Azure.Security.KeyVault.Keys + Azure.Identity | 4.10.0 / 1.21.0 |
| Validacao de token | Microsoft.IdentityModel.Tokens | 8.22.0 |
| API Docs | Microsoft.Azure.Functions.Worker.Extensions.OpenApi | 1.6.0 |
| Tracing | OpenTelemetry + Azure.Monitor.OpenTelemetry.Exporter | 1.7.0 |
| Testes | xUnit + FluentAssertions + Moq + Bogus | 2.9.2 / 6.12.0 / 4.20.70 / 35.6.1 |
| Infra (app) | Docker + Docker Compose | — |
| Infra (cloud) | Terraform (`OficinaMecanica.Infra`: Function App Consumption, Azure SQL Database, Key Vault) | — |
