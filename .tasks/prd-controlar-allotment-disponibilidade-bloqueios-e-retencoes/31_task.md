---
status: pending
parallelizable: true
blocked_by: ["2.0", "14.0", "17.0", "22.0"]
---

<task_context>
<domain>inventory/application/metrics</domain>
<type>implementation</type>
<scope>core_feature</scope>
<complexity>medium</complexity>
<dependencies>database</dependencies>
<unblocks>"33.0, 34.0"</unblocks>
<vertical_slice>Os sete indicadores do PRD são apurados por agregação direta e expostos em uma operação HTTP.</vertical_slice>
</task_context>

# Tarefa 31.0: Apurar e expor as métricas de inventário

## Relacionada às User Stories

- [US-06] Medir vendas sem lastro e prazo de processamento para decidir sobre a exposição do piloto (cobertura direta)

## Visão Geral

`getInventoryMetrics` consolida os sete indicadores da tabela de métricas do PRD em uma única resposta, apurados por **agregação direta** sobre as tabelas do módulo.

Isso contraria deliberadamente o `x-backend-notes` da operação, que sugere projeção assíncrona. ADR-0002 proíbe introduzir infraestrutura por expectativa não medida, e a escala do piloto é de oito propriedades em noventa dias. O gatilho formal de reavaliação está fixado em número.

## Requisitos

- Sete indicadores: venda sem lastro, oferta sem allotment, conformidade de SLA, cobertura de inventário, latência do bloqueio emergencial, exposição fora da janela e expiração de retenção.
- `holdExpiration` devolve `null` enquanto a Onda B não estiver em produção.
- `from` e `to` obrigatórios; `propertyId` opcional.
- Agregações com `AsNoTracking` e projeção direta, usando os índices declarados nas tarefas 12.0 e 13.0.
- Permissão `inventory:metrics`, distinta de `inventory:read`.
- Instrumentar `inventory.metrics.coverage_duration` — a agregação de cobertura cruza `daily_inventory` inteiro e é a mais cara das sete. É o gatilho formal de reavaliação da decisão.
- Endpoint na mesma tarefa: é uma operação só, e query e endpoint formam uma fatia vertical única.

## Arquivos Envolvidos

- **Criar:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/Metrics/InventoryMetricsQueries.cs`
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Endpoints/InventoryMetricsEndpoints.cs`
  - `../localizestay-backend/tests/LocalizeStay.UnitTests/Inventory/InventoryMetricsQueryHandlerTests.cs`
- **Modificar:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Endpoints/InventoryEndpoints.cs` (uma linha de registro)
- **Referência:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Endpoints/CommercialOfferMetricsEndpoints.cs` (padrão de métricas do módulo)
  - `.tasks/prd-controlar-allotment-disponibilidade-bloqueios-e-retencoes/api-contract.md` (exemplo de resposta de `getInventoryMetrics`)
  - `.tasks/prd-controlar-allotment-disponibilidade-bloqueios-e-retencoes/prd.md` (tabela de Métricas de Sucesso)
- **Skills para consultar durante implementação:**
  - `dotnet-performance` — agregação com `AsNoTracking`, índices que evitam varredura completa
  - `dotnet-observability` — histograma de duração como gatilho de reavaliação
  - `restful-api` — parâmetros obrigatórios e formato da resposta

## Subtarefas

- [ ] 31.1 Implementar as agregações dos sete indicadores, com os denominadores corretos e `holdExpiration = null` na Onda A.
- [ ] 31.2 Instrumentar `inventory.metrics.coverage_duration` sobre a agregação de cobertura.
- [ ] 31.3 Criar `InventoryMetricsEndpoints` com `inventory:metrics` e registrar o grupo em `InventoryEndpoints.cs`.
- [ ] 31.4 Testar: cada indicador com dados controlados, período vazio, denominador zero e ausência de divisão por zero.

## Sequenciamento

- Bloqueado por: 2.0, 14.0, 17.0, 22.0
- Desbloqueia: 33.0, 34.0
- Paralelizável: Sim; arquivos exclusivos, com apenas a linha de registro compartilhada.

## Rastreabilidade

- Esta tarefa cobre: as sete métricas de sucesso do PRD e RF-04 na parte de medição.
- Evidência esperada: `InventoryMetricsQueryHandlerTests` prova cada indicador com dados controlados.

## Detalhes de Implementação

Mapa métrica do PRD → campo da resposta → fonte:

| Métrica do PRD | Campo | Fonte | Meta |
|---|---|---|---:|
| Venda sem lastro | `unbackedSales` | Comprometimentos sem saldo na data | 0 |
| Oferta sem allotment | `offersWithoutAllotment` | Gate `activeAllotment` `blocked` com oferta exposta | 0 |
| SLA de processamento | `slaCompliance` | `inventory_requests.processed_within_sla` | 100% |
| Cobertura de inventário | `inventoryCoverage` | `daily_inventory` + piso comercial | ≥ 8 |
| Latência do bloqueio emergencial | `emergencyBlockLatency` | `inventory_blocks.sales_stopped_at` | ≤ 60s |
| Exposição fora da janela | `outOfWindowExposure` | Solicitações emergenciais pendentes × reservas no intervalo | Monitorada |
| Expiração de retenção | `holdExpiration` | `inventory_holds` (Onda B) | Monitorada |

Resposta-alvo:

```json
{
  "period": { "startDate": "2026-09-01", "endDate": "2026-09-30" },
  "unbackedSales": { "count": 0, "target": 0 },
  "offersWithoutAllotment": { "count": 0, "target": 0 },
  "slaCompliance": { "processedWithinSla": 23, "totalProcessed": 23, "percentage": 100 },
  "inventoryCoverage": { "propertiesWithActiveAllotment": 8, "propertiesMeetingCommercialFloor": 7, "target": 8 },
  "emergencyBlockLatency": { "sampleSize": 4, "p95Seconds": 3.8, "maxSeconds": 6.1, "targetSeconds": 60 },
  "outOfWindowExposure": { "confirmedReservations": 0, "pendingEmergencyRequests": 1 },
  "holdExpiration": null
}
```

> **Desvio consciente do contrato, registrado na TechSpec.** O `x-backend-notes` pede projeção assíncrona; adotamos agregação direta por força de ADR-0002. Gatilho formal de reavaliação: **p95 de `inventory.metrics.coverage_duration` acima de 2s sustentado por 7 dias com dados reais do piloto abre o ADR de projeção.** Nenhum campo, status ou schema do contrato é alterado.

Atenção ao denominador: `slaCompliance.percentage` com `totalProcessed = 0` deve devolver `null` ou `100`, nunca lançar divisão por zero — decidir e testar explicitamente.

**Convenções da stack (das skills consultadas):**

- Agregação com `AsNoTracking` e projeção direta; nenhuma entidade materializada (`dotnet-performance`).
- Índice `(date) WHERE allotted_units > 0` sustenta a cobertura sem varrer a tabela.
- Histograma OpenTelemetry com tags de baixa cardinalidade (`dotnet-observability`).
- `inventory:metrics` é permissão própria, sem hierarquia com `inventory:read`.

## Critérios de Sucesso (Verificáveis)

- [ ] Testes passam: `dotnet test ../localizestay-backend/LocalizeStay.sln --filter "FullyQualifiedName~InventoryMetricsQueryHandlerTests"`
- [ ] Build compila sem erros: `dotnet build ../localizestay-backend/LocalizeStay.sln --no-restore`
- [ ] Formatação válida: `dotnet format ../localizestay-backend/LocalizeStay.sln --verify-no-changes --no-restore`
- [ ] Os sete campos aparecem na resposta, com `holdExpiration: null` na Onda A.
- [ ] Período sem dados não lança divisão por zero.
- [ ] `emergencyBlockLatency.p95Seconds` é apurado a partir de `salesStoppedAt`.
- [ ] `inventoryCoverage` distingue propriedades com allotment vigente das que atingem o piso comercial.
- [ ] Token com `inventory:read` mas sem `inventory:metrics` recebe 403.
- [ ] `inventory.metrics.coverage_duration` é emitida a cada consulta.
