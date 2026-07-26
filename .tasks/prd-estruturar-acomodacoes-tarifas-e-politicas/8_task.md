---
status: pending
parallelizable: true
blocked_by: ["7.0"]
---

<task_context>
<domain>inventory/application/commercial-offer-queries</domain>
<type>implementation</type>
<scope>core_feature</scope>
<complexity>high</complexity>
<dependencies>database</dependencies>
<unblocks>"10.0, 12.0"</unblocks>
</task_context>

# Tarefa 8.0: Implementar consultas, DTOs, histórico e métricas

## Relacionada às User Stories

- [US-01] Visualizar rascunhos e pendências (direta)
- [US-03] Conferir resumo comercial (direta)
- [US-04] Reutilizar registros dos canais atuais no SLA (suporte)
- [US-05] Medir prazo, completude e retrabalho (direta)

## Visão Geral

Implementar read models e handlers para fila, detalhe, políticas, acomodações, tarifas, histórico e métricas. Consultas devem projetar diretamente do EF Core, sem tracking, cache ou projeção assíncrona, e respeitar paginação/filtros do contrato.

## Requisitos

- O primeiro detalhe após incorporação cria `CommercialOffer` draft idempotentemente.
- Listas retornam `data: []` e paginação quando declarada.
- Fila suporta filtros, ordenação e campos-resumo indexados do contrato.
- Histórico projeta `business_audit_entries`; não criar tabela de timeline.
- Métricas expõem numerador, denominador e período, incluindo SLA, completude, dupla validação, primeira aceitação e retrabalho.
- O SLA de duas jornadas úteis usa calendário oficial; o indicador de quatro horas reutiliza comunicações humanas F01.
- DTOs HTTP/aplicação não expõem snapshots, payloads técnicos nem PII.

## Arquivos Envolvidos

- **Criar:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/CommercialOffers/CommercialOfferQueries.cs`
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/CommercialOffers/CommercialOfferDtos.cs`
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/CommercialOffers/CommercialOfferMapper.cs`
  - `../localizestay-backend/tests/LocalizeStay.UnitTests/Inventory/CommercialOfferMetricsQueryHandlerTests.cs`
- **Modificar:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/Timing/IBusinessCalendar.cs`
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Infrastructure/Timing/ConfiguredBusinessCalendar.cs`
- **Referência:**
  - `tasks/prd-estruturar-acomodacoes-tarifas-e-politicas/api-contract.yaml`
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/PropertyOnboardings/PropertyOnboardingQueries.cs`
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Infrastructure/InventoryDbContext.cs`
- **Skills para consultar durante implementação:**
  - `dotnet-performance` — `AsNoTracking`, `Select` e paginação
  - `dotnet-architecture` — Queries/Handlers CQRS e mapper manual
  - `dotnet-code-quality` — records, async e `CancellationToken`
  - `dotnet-testing` — testes de handlers e calendário
  - `restful-api` — paginação, filtros e resposta JSON

## Subtarefas

- [ ] 8.1 Definir DTOs internos que correspondem aos schemas do OpenAPI sem acoplar domínio ao HTTP.
- [ ] 8.2 Implementar mapper manual para o detalhe do agregado e recursos.
- [ ] 8.3 Implementar `ListCommercialOffersQueryHandler` e `GetCommercialOfferQueryHandler`.
- [ ] 8.4 Implementar queries de políticas, acomodações e tarifas com filtros/paginação.
- [ ] 8.5 Implementar `ListCommercialOfferHistoryQueryHandler` sobre auditoria.
- [ ] 8.6 Estender `IBusinessCalendar` para o prazo de dois dias úteis.
- [ ] 8.7 Implementar `GetCommercialOfferMetricsQueryHandler` com denominadores explícitos.
- [ ] 8.8 Testar limites de período, calendário, listas vazias, filtros, ordenação e denominadores zero.

## Sequenciamento

- Bloqueado por: 7.0
- Desbloqueia: 10.0 e 12.0
- Paralelizável: Sim; pode avançar junto com 9.0.

## Rastreabilidade

- Esta tarefa cobre: US-01, US-03 e US-05 diretamente; US-04 como suporte; RF-04, RF-05 e RF-06.
- Evidência esperada: handlers retornam os schemas e métricas do contrato sem carregar agregados completos para listas.

## Detalhes de Implementação

Queries previstas: `ListCommercialOffersQuery`, `GetCommercialOfferQuery`, `ListCommercialPoliciesQuery`, `ListAccommodationsQuery`, `GetAccommodationQuery`, `ListCommercialRatesQuery`, `ListCommercialOfferHistoryQuery` e `GetCommercialOfferMetricsQuery`.

Todas as listas devem iniciar por `AsNoTracking()`, aplicar filtros antes de `CountAsync` e projetar apenas campos necessários via `Select`. A fila ordena deterministicamente com desempate por ID. O detalhe pode usar múltiplas projeções ou `AsSplitQuery` quando necessário, evitando explosão cartesiana.

**Convenções da stack (das skills consultadas):**

- Queries não causam mutação, exceto o comportamento explícito e idempotente do primeiro GET.
- Mapper manual em classe pequena; não introduzir AutoMapper/Mapster.
- Páginas validam limites do contrato e retornam metadados consistentes.
- Propagar `CancellationToken` em `CountAsync`, `ToListAsync` e demais I/O.
- Testes usam xUnit/AwesomeAssertions e cobrem denominador zero sem NaN/divisão por zero.

## Critérios de Sucesso (Verificáveis)

- [ ] Testes focados passam: `dotnet test ../localizestay-backend/LocalizeStay.sln --filter "FullyQualifiedName~CommercialOfferMetricsQueryHandlerTests|FullyQualifiedName~BusinessCalendarTests"`
- [ ] Build compila sem erros: `dotnet build ../localizestay-backend/LocalizeStay.sln --no-restore`
- [ ] Formatação válida: `dotnet format ../localizestay-backend/LocalizeStay.sln --verify-no-changes --no-restore`
- [ ] Todas as consultas somente leitura usam `AsNoTracking` e projeção.
- [ ] Listas vazias retornam `data: []`; paginação respeita página 1 e máximo 100.
- [ ] Métricas retornam numerador, denominador e período; denominador zero produz resultado definido.
- [ ] O primeiro GET concorrente cria apenas um draft e ambos os requests retornam a mesma revisão.

