---
status: pending
parallelizable: true
blocked_by: ["18.0", "19.0", "23.0"]
---

<task_context>
<domain>inventory/application/observability</domain>
<type>implementation</type>
<scope>performance</scope>
<complexity>medium</complexity>
<dependencies>http_server</dependencies>
<unblocks>"51.0"</unblocks>
<vertical_slice>As operações de inventário emitem métricas, spans e logs estruturados que sustentam os alertas do PRD — sobretudo a latência de um minuto do bloqueio emergencial.</vertical_slice>
</task_context>

# Tarefa 32.0: Instrumentar a telemetria da Onda A

## Relacionada às User Stories

- [US-06] Medir vendas sem lastro e prazo de processamento (cobertura direta)
- [US-03] Bloquear datas imediatamente (suporte — a latência de um minuto só é verificável se for medida)

## Visão Geral

O PRD exige que o bloqueio emergencial corte novas vendas em **até um minuto** após a confirmação no painel, e que esse prazo meça o sistema. Sem instrumentação, a meta é uma afirmação sem evidência.

Esta tarefa acrescenta os instrumentos, spans e tags da F03 ao `InventoryTelemetry` existente, e liga os handlers já implementados a eles.

## Requisitos

- Métricas com **tags de baixa cardinalidade**; identificadores viajam apenas em spans e escopos de log, nunca como tag de métrica.
- `inventory.block.emergency_latency` é histograma do commit ao efeito na consulta de disponibilidade — a base da meta de sessenta segundos.
- Spans customizados: `inventory.ledger.load`, `inventory.allotment.materialize`, `inventory.block.apply`, `inventory.availability.query`.
- Logs estruturados com `propertyId`, `accommodationId`, `allotmentId`, `blockId`, `requestId`, `operation`, `result`, `eventId` e `correlationId`.
- **Nenhum dado do viajante em log algum** — pertence a D03.
- Auditoria de negócio é distinta dos logs de diagnóstico e não é substituída por eles.
- Os instrumentos da Onda B (`inventory.hold.*`) entram nas tarefas 41.0 e 42.0, no mesmo arquivo.

## Arquivos Envolvidos

- **Criar:**
  - `../localizestay-backend/tests/LocalizeStay.IntegrationTests/Inventory/InventoryControlObservabilityTests.cs`
- **Modificar:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/Observability/InventoryTelemetry.cs` (instrumentos, spans e tags da F03)
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/InventoryBlocks/InventoryBlockCommands.cs` (latência, contadores e span)
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/Allotments/AllotmentCommands.cs` (contadores e span)
- **Referência:**
  - `../localizestay-backend/tests/LocalizeStay.IntegrationTests/Inventory/CommercialOfferObservabilityTests.cs` (padrão de teste de telemetria da F02)
  - `../localizestay-backend/src/BuildingBlocks/LocalizeStay.SharedKernel/Observability/OpenTelemetryExtensions.cs`
- **Skills para consultar durante implementação:**
  - `dotnet-observability` — métricas, spans, scopes de log para correlação
  - `dotnet-production-readiness` — templates estruturados obrigatórios, níveis de log, sanitização
  - `dotnet-testing` — teste de telemetria com `MeterListener` / `ActivityListener`

## Subtarefas

- [ ] 32.1 Declarar os instrumentos da Onda A em `InventoryTelemetry`: `allotment.granted`, `allotment.changed`, `block.applied`, `block.emergency_latency`, `block.affects_reservation`, `request.sla`, `request.received_outside_window`, `sellability.gate_changed`, `availability.query_duration`.
- [ ] 32.2 Instrumentar os handlers de allotment e bloqueio com contadores, spans e escopos de log estruturado.
- [ ] 32.3 Medir `emergency_latency` do commit ao instante em que a consulta de disponibilidade reflete o corte, gravando `salesStoppedAt`.
- [ ] 32.4 Testar: os instrumentos são emitidos; nenhuma tag carrega identificador; nenhum log contém dado de viajante.

## Sequenciamento

- Bloqueado por: 18.0, 19.0, 23.0
- Desbloqueia: 51.0
- Paralelizável: Sim; roda em paralelo às demais tarefas de certificação da Fase 6.

## Rastreabilidade

- Esta tarefa cobre: a seção de Monitoramento e Observabilidade da TechSpec e a métrica de latência do PRD.
- Evidência esperada: `InventoryControlObservabilityTests` prova a emissão e a ausência de alta cardinalidade; 51.0 documenta os alertas no runbook.

## Detalhes de Implementação

Instrumentos da Onda A:

| Instrumento | Tipo | Tags |
|---|---|---|
| `inventory.allotment.granted` / `.changed` | Contador | `result` |
| `inventory.block.applied` | Contador | `type`, `origin` |
| `inventory.block.emergency_latency` | Histograma | — |
| `inventory.block.affects_reservation` | Contador | — |
| `inventory.request.sla` | Contador | `result` |
| `inventory.request.received_outside_window` | Contador | — |
| `inventory.sellability.gate_changed` | Contador | `gate`, `result` |
| `inventory.availability.query_duration` | Histograma | — |
| `inventory.metrics.coverage_duration` | Histograma | — (tarefa 31.0) |
| `inventory.outbox.failures` | Contador | — (existente, reutilizado) |

Alertas que estes instrumentos habilitam, documentados em 51.0:

- **Qualquer amostra** de `inventory.block.emergency_latency` acima de sessenta segundos — viola a meta do PRD.
- Outbox sem processamento após o limite de retentativas.
- `inventory.metrics.coverage_duration` com p95 acima de 2s sustentado por 7 dias — abre o ADR de projeção assíncrona.
- Taxa anormal de `429` no router `lstay-api`, observada na borda — indica limite mal calibrado ou abuso real.

> Tags de métrica **não** podem carregar `propertyId`, `accommodationId` ou `blockId`. Uma tag por propriedade parece útil no piloto de oito propriedades e vira explosão de cardinalidade na primeira expansão. Identificador viaja em span e em escopo de log, onde o custo é por amostra e não por série temporal.

**Convenções da stack (das skills consultadas):**

- Métricas de baixa cardinalidade; spans customizados por operação (`dotnet-observability`).
- Templates de log estruturados obrigatórios: `_logger.LogInformation("...{Field}...", value)` (`dotnet-production-readiness`).
- Nunca logar em loop — agregar e emitir uma linha por operação.
- `Information` para evento de negócio, `Warning` para situação inesperada não fatal, `Error` para erro tratável.

## Critérios de Sucesso (Verificáveis)

- [ ] Testes passam: `dotnet test ../localizestay-backend/tests/LocalizeStay.IntegrationTests --filter "FullyQualifiedName~InventoryControlObservabilityTests"`
- [ ] Build compila sem erros: `dotnet build ../localizestay-backend/LocalizeStay.sln --no-restore`
- [ ] Formatação válida: `dotnet format ../localizestay-backend/LocalizeStay.sln --verify-no-changes --no-restore`
- [ ] Aplicar bloqueio emergencial emite `inventory.block.emergency_latency` com amostra menor que 60s no ambiente de teste.
- [ ] Nenhuma tag de métrica contém identificador de propriedade, acomodação, bloqueio ou reserva.
- [ ] Os quatro spans customizados aparecem no `ActivityListener` durante as operações correspondentes.
- [ ] Nenhum log emitido pela F03 contém nome, documento, e-mail ou telefone.
- [ ] A telemetria da F01/F02 segue intacta: `dotnet test ../localizestay-backend/tests/LocalizeStay.IntegrationTests --filter "FullyQualifiedName~CommercialOfferObservabilityTests"`
