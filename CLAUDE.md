# OficinaMecanica.Seguranca - Servico de Autenticacao/Autorizacao

## Visao Geral

Azure Function (.NET 8, isolated worker) responsavel por autenticacao e
autorizacao do ecossistema OficinaMecanica — extraido do monolito
`OficinaMecanica` (que ate entao tinha um `TokenService`/`AuthController`
proprios, JWT simetrico com 1 admin hardcoded em config). Emite tokens JWT
assinados com **RS256** (par de chaves assimetrico) e expoe um endpoint
**JWKS** (`/.well-known/jwks.json`) pra qualquer API do ecossistema validar
tokens usando so a chave publica — sem segredo compartilhado entre
servicos.

Repositorio irmao de `OficinaMecanica` (a aplicacao original) e
`OficinaMecanica.Infra` (Terraform centralizado de todo o ecossistema,
inclusive a infra desse servico — ver secao "Infraestrutura" abaixo).

## Estrutura do Projeto

```
src/
  OficinaMecanica.Seguranca.Domain/          # Usuario, PerfilUsuario, IUsuarioRepository
  OficinaMecanica.Seguranca.Application/     # Commands/Queries (MediatR), Validators (FluentValidation),
                                              # interfaces (ITokenService, IPasswordHasher, IJwksProvider)
  OficinaMecanica.Seguranca.Infrastructure/  # EF Core, UsuarioRepository, TokenService (RS256 via Key Vault),
                                              # JwksProvider, BCryptPasswordHasher
  OficinaMecanica.Seguranca.Functions/       # Azure Functions isolated worker - LoginFunction, JwksFunction,
                                              # HealthFunction (so traduzem HTTP <-> MediatR, sem logica de negocio)
tests/
  OficinaMecanica.Seguranca.Domain.Tests/
  OficinaMecanica.Seguranca.Application.Tests/  # Moq
  OficinaMecanica.Seguranca.Integration.Tests/  # ver secao "Testes" abaixo
  OficinaMecanica.Seguranca.Tests.Common/       # Builders com Bogus (UsuarioBuilder)
```

Mesmos principios do `OficinaMecanica`: Clean Architecture (Domain ->
Application -> Infrastructure -> Functions, dependencias de fora pra
dentro), DDD, CQRS via MediatR, `ValidationBehavior` (pipeline MediatR que
roda FluentValidation antes dos handlers), Repository pattern (interface no
Domain, implementacao na Infrastructure).

## Convencoes

- **Idioma do dominio**: entidades, propriedades, enums, mensagens de erro
  em portugues (ex.: `Usuario`, `Cpf`, `PerfilUsuario.Admin`)
- **Idioma da arquitetura**: namespaces, design patterns, interfaces
  genericas, nomes de camada em ingles

## Escopo do MVP (Usuario)

So o admin inicial, via **seed idempotente** no cold start da Function
(Infrastructure verifica se a tabela `Usuarios` esta vazia e insere 1 admin
com hash da senha vinda de config/Key Vault) — sem CRUD de usuarios ainda.
Mantem paridade funcional com o admin unico hardcoded que existia no
monolito, mas a entidade/repositorio/hash ja ficam prontos pra CRUD futuro
sem redesenho.

## Assinatura RS256 e JWKS

Chave privada como `azurerm_key_vault_key` de verdade (RSA-2048,
software-protected) no Key Vault (`kvfiap`, provisionado em
`OficinaMecanica.Infra`) — assinatura via `CryptographyClient.SignAsync`,
a chave nunca sai do Key Vault. `TokenService` seta o header `kid` do JWT
com a versao da chave usada, permitindo rotacionar no futuro sem invalidar
tokens ja emitidos. `JwksFunction` expoe a chave publica correspondente em
`/.well-known/jwks.json`, com cache curto (`IMemoryCache`) pra nao bater no
Key Vault a cada request.

Quem consome (a API do `OficinaMecanica`, futuras APIs) troca a
`IssuerSigningKey` fixa por um `ConfigurationManager<JsonWebKeySet>`
apontando pra essa URL + `IssuerSigningKeyResolver` filtrando por `kid`.

## Documentacao OpenAPI/Swagger

`LoginFunction`/`JwksFunction`/`HealthFunction` sao decoradas com atributos
do pacote `Microsoft.Azure.Functions.Worker.Extensions.OpenApi` (o
Swashbuckle usado no `OficinaMecanica` nao funciona em isolated worker) —
`OpenApiOperationAttribute`/`OpenApiRequestBodyAttribute`/
`OpenApiResponseWithBodyAttribute`/`OpenApiResponseWithoutBodyAttribute`
descrevem operacao, request body e respostas (incluindo o 401 do login,
mapeado pelo `ExceptionHandlingMiddleware` a partir de
`AutenticacaoException`). `OpenApiConfigurationOptions.cs` (raiz do projeto
Functions) customiza titulo/versao/descricao do documento, herdando de
`DefaultOpenApiConfigurationOptions` — descoberto automaticamente via
reflection pelo proprio pacote, sem precisar registrar nada em DI no
`Program.cs`. O pacote injeta sozinho 3 functions extras
(`RenderSwaggerDocument`/`RenderSwaggerUI`/`RenderOpenApiDocument`) que
respeitam o mesmo `routePrefix: ""` do `host.json` — Swagger UI fica em
`GET /swagger/ui`, o spec em `GET /openapi/v3.json` (tambem
`/openapi/v2.json`, `/swagger.json`, `/swagger.yaml`), confirmado rodando a
stack local (`docker compose up`, mesmo caminho via `func start`).

`GET /` redireciona (302) pro Swagger UI — `RootFunction.cs`. Um
`HttpTrigger` com `Route = ""` ou `Route = "/"` nao funciona pra isso no
isolated worker (o primeiro cai no nome da function, `/Root`; o segundo gera
uma rota duplicada quebrada `//` que trava a invocacao em timeout) — a
solucao real e uma rota catch-all de menor prioridade
(`Route = "{*caminho}"`): rotas literais (`health`, `login`, `swagger/ui`
etc) sempre casam primeiro por serem mais especificas, essa so pega o que
sobra. So redireciona quando o parametro capturado vem vazio (raiz de
verdade); qualquer outro caminho desconhecido continua virando 404 normal,
em vez de redirecionar tudo pro Swagger.

## Docker Compose local

`docker-compose.yml` na raiz sobe este servico 100% self-contained (sem
depender de nenhum recurso Azure real): `seguranca` (build do `Dockerfile`,
multi-stage com a imagem oficial do Functions host pra dotnet-isolated
8.0), `sqlserver` (mesma imagem/padrao de healthcheck/volume nomeado do
`docker-compose.yml` do `OficinaMecanica`, container isolado deste repo) e
`azurite` (emulador de storage, dados efemeros). Simplesmente nao seto
`KeyVault__Uri` nas env vars do container `seguranca` — a ausencia dessa
config e o que já ativa o fallback `LocalRsaTokenService`/
`LocalRsaJwksProvider` descrito acima, sem precisar de nenhum truque
adicional. `AZURE_FUNCTIONS_ENVIRONMENT=Development` e critico (equivalente
ao `ASPNETCORE_ENVIRONMENT`, ver `Program.cs`) — sem ele a migration nunca
roda e o seed do admin falha contra um schema inexistente.

## Testes

Isolated worker do Azure Functions nao tem equivalente ao
`WebApplicationFactory` do ASP.NET Core (`HttpRequestData`/`FunctionContext`
sao dificeis de fakear sem perder realismo). Por isso: toda logica testavel
fica na Application (handlers via MediatR, Moq — mesmo padrao do
`OficinaMecanica`), `LoginFunction`/`JwksFunction` ficam finas o bastante
pra so precisarem de um smoke test. Esse smoke test
(`Integration.Tests`) sobe Azurite + Azure Functions Core Tools (`func
start`) como processo real, testado via `HttpClient` comum.

## Infraestrutura

Toda a infra deste servico (Function App + Service Plan Consumption, banco
`SegurancaDb`, chave RS256 no Key Vault, RBAC, bloco `segurancaserver` na
APIM) vive em `OficinaMecanica.Infra` (repositorio irmao) — este repositorio
nunca tem pasta `infra/` propria. Deploy via zip (`Azure/functions-action`
ou `func azure functionapp publish`), autenticado no Azure via OIDC (App
Registration/Federated Credential proprios, federados pra este repo).

## CI/CD

`.github/workflows/ci.yml` — 2 jobs, mesmo espirito do CI do `OficinaMecanica`
(OIDC, sem secret de longa duracao) mas adaptado pra deploy de Function App
em vez de imagem+AKS:

1. **build-and-test**: restore/build da solution inteira +
   `Domain.Tests`/`Application.Tests` (rapidos, sem dependencia externa).
   `Integration.Tests` fica de fora do CI de proposito - sobe Azure
   Functions Core Tools (`func start`) + Azurite como processos reais e
   precisa de um SQL Server de verdade pra migration (LocalDB local, ver
   secao "Testes" acima) - portar isso pro runner ubuntu do GitHub Actions
   exigiria trocar LocalDB por um container SQL Server + instalar o Core
   Tools no runner, fora de escopo por ora. Roda local antes de dar push.
2. **deploy** (so em push/dispatch na `main` OU `release` - homologacao e
   producao publicam na mesma Function App, ver secao "Ambiente de
   Homologacao" no README): `dotnet publish` do projeto Functions, aplica
   as migrations pendentes (`dotnet ef database update`, connection string
   buscada do Key Vault via `az keyvault secret show` - unico mecanismo que
   aplica schema novo em producao, ja que `Database.Migrate()` em
   `Program.cs` so roda com `AZURE_FUNCTIONS_ENVIRONMENT=Development`) e
   entao `Azure/functions-action` **sem** `publish-profile` - o login OIDC
   (`azure/login`) já deixa o contexto autenticado. O Service Principal do
   GitHub Actions tem `Contributor` na propria Function App e `Key Vault
   Secrets User` no Key Vault (`OficinaMecanica.Infra/github_oidc_seguranca/main.tf`),
   suficiente pra deploy via zip + ler a connection string sem precisar de
   mais nada.

**Ja feito** (nao e mais pendente): o repositorio remoto
`willianrmattos/OficinaMecanica.Seguranca` foi criado no GitHub (a
Federated Identity Credential ja esperava esse nome exato,
`OficinaMecanica.Infra/github_oidc_seguranca/main.tf`) e recebeu o push
inicial - este repositorio ja tem commits reais. As 3 `variables` do
repositorio no GitHub (`AZURE_CLIENT_ID` - valor de `terraform output -raw
seguranca_github_actions_client_id`, diferente do `AZURE_CLIENT_ID` usado
no repo `OficinaMecanica`; `AZURE_TENANT_ID`/`AZURE_SUBSCRIPTION_ID` -
mesma assinatura Azure ja usada no `OficinaMecanica`) ja estao configuradas
- confirmado rodando o CI de ponta a ponta com sucesso (build+test + login
OIDC + deploy real na Function App).

## Agente Validador de Padroes

`.claude/agents/pattern-validator.md` — subagente read-only (`Read, Grep,
Glob`) que revisa este repositorio contra as convencoes estabelecidas no
`OficinaMecanica` (camadas, idioma do dominio, CQRS/ValidationBehavior,
estilo de comentario, estrutura de testes). Rodar apos qualquer mudanca
significativa de codigo, antes de considerar uma fase concluida.
