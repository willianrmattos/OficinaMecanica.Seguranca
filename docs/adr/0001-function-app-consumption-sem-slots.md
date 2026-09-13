# ADR 0001: Function App Consumption sem deployment slots

## Status
Aceito

## Contexto
Ao desenhar o deploy automatico de homologacao (`release`) e producao
(`main`), a primeira ideia foi usar **deployment slots** do Azure
Functions - um mecanismo nativo pra manter duas versoes da mesma Function
App (ex: "staging" e "production") e trocar entre elas com um swap, sem
downtime e sem duplicar a infraestrutura em si (Key Vault, banco, etc.
continuariam compartilhados).

Essa ideia esbarrou numa limitacao da plataforma: o Service Plan usado e
**Consumption (Y1)**, o tier serverless do Azure Functions (cobra por
execucao, com scale-to-zero) - e o tier Consumption **nao suporta
deployment slots**. Isso e uma limitacao da plataforma, nao uma escolha de
configuracao: nao ha flag ou parametro que habilite slots num Consumption
plan, seria necessario migrar pra um tier Premium ou Dedicated (App
Service Plan), que tem custo fixo mensal mesmo sem trafego algum.

## Decisao
Manter o Service Plan Consumption (alinhado com a restricao geral de custo
do projeto - ver RFC 0001 do `OficinaMecanica.Infra`) e, em vez de usar
slots, fazer `main` e `release` publicarem na **mesma Function App**
(`funcsegurancafiap`), sem nenhum isolamento fisico entre os dois
ambientes - mesma decisao de fundo do ADR 0003 de `OficinaMecanica.Infra`
(mesmo cluster/aplicacao pra homologacao e producao), aqui aplicada a
Function App em vez de ao AKS.

## Consequencias
### Positivas
- Custo zero adicional - nenhum Service Plan Premium/Dedicated
  provisionado so pra ganhar suporte a slots.
- Simplicidade: um unico app pra monitorar, sem logica de swap entre
  slots.

### Negativas
- Sem isolamento real entre homologacao e producao - um deploy disparado
  por `release` sobrescreve a mesma Function App que atende producao.
- Sem a possibilidade de "esquentar" uma nova versao no slot de staging e
  so trocar o trafego depois de validar (o que os slots dariam de graca).

## Alternativas consideradas
- **Migrar pra um Service Plan Premium/Dedicated com slots**: resolveria o
  problema de forma nativa, mas introduz custo fixo mensal - rejeitado
  pela mesma restricao de orcamento que motivou o tier Consumption em
  primeiro lugar.
- **Uma segunda Function App inteira so pra homologacao**: isolamento
  real, mas cada Function App em Consumption ja e gratuita ate um certo
  volume de execucoes - o obstaculo real nao era essa segunda Function App
  em si, e sim precisar de um segundo banco de dados e Key Vault
  associados a ela pra fazer sentido como ambiente isolado de verdade, o
  que reintroduziria o mesmo problema de custo resolvido em ADR 0003 do
  `OficinaMecanica.Infra`.
