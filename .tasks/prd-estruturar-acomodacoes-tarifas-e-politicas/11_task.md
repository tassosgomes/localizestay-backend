---
status: done
parallelizable: true
blocked_by: ["7.0", "9.0"]
---

<task_context>
<domain>inventory/operations/commercial-offers</domain>
<type>documentation</type>
<scope>configuration</scope>
<complexity>medium</complexity>
<dependencies>database,http_server</dependencies>
<unblocks>"12.0"</unblocks>
</task_context>

# Tarefa 11.0: Instrumentar, documentar e preparar a operação

## Relacionada às User Stories

- [US-05] Medir prazo, completude e retrabalho (direta)
- [US-03] Diagnosticar validações e envios (suporte)

## Visão Geral

Completar telemetria OpenTelemetry, logging seguro, alertas operacionais, documentação de deploy/rollback e runbook da F02. A tarefa reutiliza health checks, OTLP e rate limiting globais; não adiciona infraestrutura nova.

## Requisitos

- Instrumentar contadores, histograma e spans definidos na TechSpec.
- Logs estruturados devem correlacionar propriedade, revisão, operação e IDs de workflow.
- Proibir preços completos, snapshots, comentários, textos jurídicos, tokens e PII nos logs.
- Confirmar que PostgreSQL continua no readiness e que liveness/readiness excluem dados de negócio.
- Documentar migration antes da ativação, rollback, replay de outbox e diagnóstico de conflitos.
- Definir alertas para outbox, persistência/concorrência e violação de submissão.

## Arquivos Envolvidos

- **Criar:**
  - `../localizestay-backend/docs/runbooks/commercial-offers.md`
  - `../localizestay-backend/tests/LocalizeStay.IntegrationTests/Inventory/CommercialOfferObservabilityTests.cs`
- **Modificar:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/Observability/InventoryTelemetry.cs`
  - `../localizestay-backend/README.md`
- **Referência:**
  - `../localizestay-backend/src/BuildingBlocks/LocalizeStay.SharedKernel/Observability/OpenTelemetryExtensions.cs`
  - `../localizestay-backend/src/BuildingBlocks/LocalizeStay.SharedKernel/HealthChecks/HealthCheckExtensions.cs`
  - `../localizestay-backend/src/BuildingBlocks/LocalizeStay.SharedKernel/Outbox/OutboxProcessor.cs`
  - `tasks/prd-estruturar-acomodacoes-tarifas-e-politicas/techspec.md`
- **Skills para consultar durante implementação:**
  - `dotnet-observability` — métricas, spans, logs e health checks
  - `dotnet-production-readiness` — sanitização, deploy e rollback
  - `dotnet-code-quality` — naming e APIs de telemetria
  - `dotnet-testing` — smoke checks e assertions de instrumentação

## Subtarefas

- [ ] 11.1 Adicionar métricas `created`, `mutation`, `validation`, `validation_invalidated`, `submission`, `returned`, `rate_overlap`, `submission_duration` e `outbox_failure`.
- [ ] 11.2 Adicionar spans `load`, `validate`, `submit`, `return` e `metrics`.
- [ ] 11.3 Padronizar scopes/logs com `propertyId`, `offerRevision`, `operation`, `result`, IDs e `correlationId`.
- [ ] 11.4 Auditar handlers para impedir conteúdo sensível nos logs.
- [ ] 11.5 Verificar health/readiness de PostgreSQL e exclusão dos probes do tracing.
- [ ] 11.6 Escrever runbook com migration, rollback, replay, alertas, SLI/SLO e troubleshooting.
- [ ] 11.7 Atualizar README com contrato, comandos de certificação e configuração jurídica.
- [ ] 11.8 Criar testes de registro das métricas/spans, health endpoints e ausência de conteúdo sensível.

## Sequenciamento

- Bloqueado por: 7.0 e 9.0
- Desbloqueia: 12.0
- Paralelizável: Sim; pode evoluir em paralelo com 10.0 após o workflow estabilizar.

## Rastreabilidade

- Esta tarefa cobre: US-05 diretamente e US-03 como suporte; objetivos/métricas do PRD e RF-05/RF-06 operacionalmente.
- Evidência esperada: instrumentos exportáveis, logs sanitizados, health checks preservados e runbook executável.

## Detalhes de Implementação

Usar o `ActivitySource` e `Meter` já expostos por `InventoryTelemetry`. Tags de métrica devem ter baixa cardinalidade (`operation`, `result`, `status`); IDs pertencem a spans/log scopes, não a labels de métrica.

O runbook deve cobrir:

- pré-deploy, migration e backfill;
- smoke checks de health/API;
- rollback de aplicação sem reverter migration destrutivamente;
- inspeção/replay seguro de outbox e eventos duplicados;
- diagnóstico de `REVISION_MISMATCH`, overlap e SLA;
- alertas, responsáveis e evidência de correlação.

**Convenções da stack (das skills consultadas):**

- OpenTelemetry/OTLP é o padrão; não adicionar Serilog/ECS.
- Logs usam templates estruturados, nunca interpolação.
- Health checks não executam regra cara nem expõem detalhes sensíveis.
- Exceções são registradas no span com status de erro e stack trace no logger.
- Documentar variáveis/configuração sem incluir secrets.

## Critérios de Sucesso (Verificáveis)

- [ ] Build compila sem erros: `dotnet build ../localizestay-backend/LocalizeStay.sln --no-restore`
- [ ] Testes de telemetria/health passam: `dotnet test ../localizestay-backend/LocalizeStay.sln --filter "FullyQualifiedName~CommercialOfferObservabilityTests"`
- [ ] Formatação válida: `dotnet format ../localizestay-backend/LocalizeStay.sln --verify-no-changes --no-restore`
- [ ] `GET /health/live` responde 200 sem consultar dependências; `GET /health/ready` inclui PostgreSQL.
- [ ] Busca automatizada não encontra snapshots, textos jurídicos, tokens ou PII em templates de log da F02.
- [ ] Runbook documenta deploy, rollback, replay, alertas e diagnóstico com comandos verificáveis.
- [ ] Todas as métricas e spans da TechSpec estão registrados uma única vez.
