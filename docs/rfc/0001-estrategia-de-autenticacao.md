# RFC 0001: Estrategia de autenticacao (JWT RS256/JWKS via servico dedicado)

## Status
Aceito

## Resumo
A autenticacao/autorizacao do ecossistema saiu do monolito
(`OficinaMecanica`) e virou um servico proprio,
`OficinaMecanica.Seguranca` (Azure Function, .NET isolated worker), que
emite tokens JWT assinados com **RS256** e expoe as chaves publicas via
`/.well-known/jwks.json`. O monolito deixou de emitir qualquer token -
so **valida** o que recebe, buscando as chaves publicas nesse endpoint.

## Motivacao
Antes desta mudanca, o proprio monolito emitia e validava JWT (assinatura
simetrica, HS256, com um segredo compartilhado). Isso criava um
acoplamento: qualquer outro servico do ecossistema que precisasse validar
o mesmo token precisaria conhecer o mesmo segredo simetrico - distribuir
um segredo compartilhado entre servicos independentes e um risco de
seguranca (qualquer um dos servicos que o possui pode tanto emitir quanto
validar tokens, nao ha separacao de responsabilidade).

## Proposta
Extrair a emissao de token pra um servico dedicado e trocar de assinatura
simetrica (HS256) pra assinatura assimetrica (**RS256**): o servico de
autenticacao guarda a chave **privada** (no Key Vault em producao,
fallback de chave RSA local em dev) e assina os tokens com ela; qualquer
outro servico do ecossistema (hoje, so o `OficinaMecanica`) valida a
assinatura usando apenas a chave **publica**, obtida dinamicamente via o
endpoint padrao `/.well-known/jwks.json` (JWKS - JSON Web Key Set). Nenhum
segredo e compartilhado entre os servicos - quem so precisa validar
tokens nunca tem acesso a nada que permitiria forjar um token novo.

## Alternativas consideradas
- **Manter HS256 com segredo compartilhado**: mais simples (uma chave so,
  sem infraestrutura de chave publica/privada), mas qualquer servico que
  valida tokens tambem seria capaz de emitir tokens validos - inaceitavel
  assim que mais de um servico precisa validar.
- **Emissao de token dentro do proprio monolito, so trocando pra RS256**:
  resolveria o problema do segredo compartilhado sem separar em outro
  servico, mas manteria a responsabilidade de autenticacao acoplada ao
  ciclo de vida/deploy do monolito - rejeitado em favor de um servico
  independente, alinhado com a ideia de que autenticacao e uma
  responsabilidade transversal, nao especifica do dominio de oficina
  mecanica.
- **Um provedor de identidade externo gerenciado (ex: Azure AD B2C,
  Auth0)**: elimina a necessidade de manter um servico de autenticacao
  proprio, mas foge do objetivo do projeto de implementar e entender o
  fluxo de autenticacao/autorizacao na pratica.

## Decisao
Servico de autenticacao dedicado (`OficinaMecanica.Seguranca`), RS256,
chaves publicas expostas via JWKS - qualquer API do ecossistema pode
validar tokens sem jamais precisar de um segredo compartilhado.
