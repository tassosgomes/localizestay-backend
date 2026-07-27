# Especificação Técnica — F03: Controlar Allotment, Disponibilidade, Bloqueios e Retenções

> **Modo de operação:** API-First
> **PRD de origem:** `tasks/prd-controlar-allotment-disponibilidade-bloqueios-e-retencoes/prd.md`
> **API Contract:** `tasks/prd-controlar-allotment-disponibilidade-bloqueios-e-retencoes/api-contract.yaml`
> **Data:** 2026-07-26
> **Status:** Aprovado

---

## Resumo Executivo

A F03 será implementada como terceira capacidade vertical do módulo `Inventory`, reaproveitando o CQRS nativo, as Minimal APIs, o `InventoryDbContext`, a outbox transacional e a auditoria de negócio já consolidados por F01 e F02. O centro da implementação é `inventory.daily_inventory`, uma tabela de saldo materializada por `(accommodation_id, date)` que traduz RN-03 em uma linha por noite vendável. Allotment materializa as datas do período; bloqueio, retenção e comprometimento fazem `SELECT ... FOR UPDATE` das noites envolvidas antes de validar e escrever, tornando a disputa pela última unidade uma serialização por noite, e não por acomodação.

A vendabilidade de RN-07 vive em `inventory.property_sellability`, projeção local alimentada por eventos de curadoria e por recálculo transacional dos gates que pertencem ao próprio módulo, de modo que `GET /availability` — público e no caminho quente de D01 — resolva os cinco gates com uma leitura indexada, sem chamada síncrona entre módulos. A expiração de retenções combina uma varredura em background, única fonte de transição de estado e publicação de evento, com uma guarda na leitura do saldo que desconsidera retenções vencidas, mantendo a capacidade vendável no instante exato de `expiresAt`.

O trade-off primário é persistir saldo derivado. `allotted_units`, `committed_units`, `held_units` e `blocked_units` são valores calculáveis a partir de allotments, bloqueios, retenções e comprometimentos, e sua persistência cria uma superfície de divergência que exige teste de reconstrução e disciplina de escrita por um único serviço de domínio. Em troca, as duas operações mais críticas da feature — a checagem "todas as noites têm saldo" e o corte de vendas em até um minuto — passam a ser transações curtas sobre um conjunto pequeno e determinístico de linhas, sem agregação em tempo de requisição e sem infraestrutura nova, preservando ADR-0002 e a possibilidade de extração futura do módulo.

---

## Skills de Referência

| Skill | Caminho | Decisões Influenciadas |
|---|---|---|
| `dotnet-architecture` | `~/.claude/skills/dotnet-architecture/SKILL.md` | CQRS nativo, limites de componentes, exceções de domínio, estrutura de pastas |
| `dotnet-dependency-config` | `~/.claude/skills/dotnet-dependency-config/SKILL.md` | EF Core, PostgreSQL, FluentValidation, migrations, outbox, options com `ValidateOnStart` |
| `dotnet-code-quality` | `~/.claude/skills/dotnet-code-quality/SKILL.md` | Naming, DI, `async`/`await`, `CancellationToken`, records internos |
| `dotnet-testing` | `~/.claude/skills/dotnet-testing/SKILL.md` | xUnit, AwesomeAssertions, `WebApplicationFactory`, Testcontainers PostgreSQL |
| `dotnet-observability` | `~/.claude/skills/dotnet-observability/SKILL.md` | Logs estruturados, métricas OTel de baixa cardinalidade, spans, health checks |
| `dotnet-performance` | `~/.claude/skills/dotnet-performance/SKILL.md` | `AsNoTracking`, projeções EF, índices, paginação, ausência de cache |
| `restful-api` | `~/.claude/skills/restful-api/SKILL.md` | OpenAPI 3.1, versionamento, paginação, RFC 9457 |
| `common-roles-naming` | `~/.claude/skills/common-roles-naming/SKILL.md` | Nomes das cinco permissões `inventory:*` e policies |

---

## Arquitetura do Sistema

### Visão Geral dos Componentes

- **`DailyInventory`** é a entidade central: uma linha por `(accommodation_id, date)` com total cedido, comprometido, retido e bloqueado. Saldo disponível é derivado, nunca persistido.
- **`InventoryLedger`** é o serviço de domínio que concentra **toda** mutação de saldo. Nenhum handler altera contadores diretamente. Ele carrega as noites com bloqueio pessimista em ordem determinística, valida as invariantes de RN-03 e aplica o delta.
- **`Allotment`** é a entidade contratual de RN-02: quantidade uniforme cedida a uma acomodação em um período contínuo, com `revision` como concurrency token e períodos não sobrepostos por acomodação.
- **`InventoryBlock`** representa a redução operacional, com `type` (`planned` ou `emergency`), `origin` (`partnerRequest`, `internalOperation`, `curationSuspension`), motivo obrigatório e preservação do histórico após remoção.
- **`InventoryHold`** é a separação temporária durante o checkout, com prazo global de quinze minutos e cinco estados terminais distintos (`held`, `expired`, `released`, `committed`, `invalidated`).
- **`InventoryCommitment`** registra a conversão de retenção em capacidade comprometida, vinculando `reservationId` sem absorver dado algum do viajante.
- **`InventoryRequest`** é a fila de solicitações recebidas por WhatsApp ou e-mail, com prioridade, SLA derivado no servidor e vínculo opcional com a alteração que originou.
- **`PropertySellability`** é a projeção dos cinco gates de RN-07, lida por `GET /availability` e `GET /sellability`.
- **`IInventoryServiceWindow`** deriva `receivedOutsideWindow`, `slaStartsAt` e `slaDueAt` a partir da janela seg–sáb 08h–20h, sem tocar o `IBusinessCalendar` da F01/F02.
- **`InventoryHoldExpirationService`** é o `IHostedService` que expira retenções vencidas em lotes e publica os eventos correspondentes.
- **Consumidores de curadoria** traduzem `propriedade-aprovada`, `propriedade-suspensa` e `conteudo-aprovado` em mudança de gate e, no caso da suspensão, em bloqueio de origem `curationSuspension`.

### Diagrama de Componentes

```text
                 público                        JWT LogTo + policies inventory:*
                    |                                        |
        GET /availability                    Minimal APIs /api/v1 (22 operações)
                    |                                        |
                    +--------------+-------------------------+
                                   v
                        Commands / Queries (CQRS nativo)
                                   |
                    +--------------+---------------------------+
                    v                                          v
            InventoryLedger  ------------------------  IInventoryServiceWindow
       (única mutação de saldo, FOR UPDATE)             (janela seg-sáb 08-20h)
                    |
                    v
        InventoryDbContext (schema inventory)
          ├─ daily_inventory          (ADR-001)
          ├─ allotments
          ├─ inventory_blocks
          ├─ inventory_holds
          ├─ inventory_commitments
          ├─ inventory_requests
          ├─ property_sellability     (ADR-002)
          ├─ inventory_idempotency_keys
          ├─ business_audit_entries   (existente)
          └─ outbox_messages          (existente)
                    |
                    v
        6 eventos de domínio -> D01, D03, D05, D07, D09

  InventoryHoldExpirationService (ADR-004)  -->  InventoryLedger + outbox
  Curation event handlers (ADR-002)         -->  PropertySellability + InventoryBlock
```

---

## Design de Implementação

### Interfaces Principais

```csharp
internal interface IInventoryLedger
{
    Task<IReadOnlyList<DailyInventory>> LoadForUpdateAsync(
        Guid accommodationId, DateOnly from, DateOnly toInclusive, CancellationToken cancellationToken);

    Task MaterializeAllotmentAsync(Allotment allotment, CancellationToken cancellationToken);

    Task ApplyBlockAsync(InventoryBlock block, CancellationToken cancellationToken);

    Task<HoldOutcome> TryHoldAsync(HoldRequest request, CancellationToken cancellationToken);

    Task ReleaseAsync(InventoryHold hold, HoldReleaseReason reason, CancellationToken cancellationToken);

    Task<CommitOutcome> CommitAsync(InventoryHold hold, Guid reservationId, CancellationToken cancellationToken);
}
```

```csharp
internal interface IInventoryServiceWindow
{
    bool IsOutsideWindow(DateTimeOffset instantUtc);

    DateTimeOffset NextWindowStart(DateTimeOffset instantUtc);

    DateTimeOffset AddBusinessHours(DateTimeOffset startUtc, int hours);
}
```

Os consumidores de curadoria implementam a interface transversal já existente, seguindo `CurationOfferReturnedHandler`:

```csharp
internal sealed class CurationPropertySuspendedHandler
    : IIntegrationEventHandler<CurationPropertySuspendedV1>
{
    public Task HandleAsync(
        CurationPropertySuspendedV1 integrationEvent,
        CancellationToken cancellationToken);
}
```

### Modelos de Dados

Mapeamento entidade do Domain Doc → modelo técnico:

| Entidade do Domain Doc | Modelo Técnico | Persistência |
|---|---|---|
| Allotment | `Allotment` | `inventory.allotments` |
| Inventário Diário | `DailyInventory` | `inventory.daily_inventory` |
| Bloqueio | `InventoryBlock` | `inventory.inventory_blocks` |
| Retenção de Inventário | `InventoryHold` | `inventory.inventory_holds` |
| *(derivado de RF-08)* | `InventoryCommitment` | `inventory.inventory_commitments` |
| *(derivado de RF-04)* | `InventoryRequest` | `inventory.inventory_requests` |
| *(derivado de RN-07)* | `PropertySellability` | `inventory.property_sellability` |
| Acomodação | `Accommodation` *(existente, F02)* | `inventory.accommodations` |
| Propriedade | `IncorporatedProperty` *(existente, F02)* | `inventory.incorporated_properties` |

Decisões de persistência:

- `daily_inventory` tem chave primária composta `(accommodation_id, date)`. `available_units` **não** é coluna: é derivado como `GREATEST(allotted − committed − held − blocked, 0)`, satisfazendo o piso zero de RN-03 por construção.
- Data sem allotment é lida como zero. A ausência da linha e a linha com `allotted_units = 0` produzem a mesma resposta; o saldo nunca é indefinido.
- `Allotment.Revision` é concurrency token do EF Core, sustentando `expectedRevision` e `409 REVISION_MISMATCH`.
- Não sobreposição de allotment por acomodação é garantida por índice de exclusão PostgreSQL sobre `daterange(start_date, end_date, '[]')` filtrado por `status = 'active'`, e não apenas por checagem no handler. A corrida entre dois `POST` concorrentes vira violação de constraint, traduzida em `409 ALLOTMENT_PERIOD_OVERLAP`.
- `InventoryBlock` é preservado após remoção: `status = 'removed'` com `removed_at`, `removal_reason` e `removed_by`. Não há hard delete.
- `InventoryHold.Status` cobre os cinco valores do contrato. `invalidated_by_block_id` é preenchido apenas na invalidação por bloqueio emergencial, distinguindo-a de expiração.
- Períodos de allotment e bloqueio usam `DateOnly` inclusivo em ambas as pontas; estadias usam `checkIn` inclusivo e `checkOut` exclusivo, e a conversão para noites acontece na borda, nunca no domínio.
- Instantes de auditoria, SLA e expiração usam `DateTimeOffset` UTC.
- Idempotência de bloqueio, retenção e comprometimento usa `inventory_idempotency_keys`, com escopo, chave e fingerprint do payload, replicando o padrão de `CommercialOfferIdempotencyKey` da F02.
- Índices:
  - `daily_inventory(accommodation_id, date)` — chave primária, cobre estadia, calendário e consulta pública;
  - `daily_inventory(date) WHERE allotted_units > 0` — métrica de cobertura;
  - `allotments(accommodation_id, start_date, end_date)` e o índice de exclusão de sobreposição;
  - `inventory_blocks(accommodation_id, start_date, end_date, status)`;
  - `inventory_holds(status, expires_at) WHERE status = 'held'` — varredura de expiração;
  - `inventory_holds(reservation_intent_id)` — deduplicação de intenção;
  - `inventory_requests(status, priority, received_at)` — ordenação da fila e cálculo de `overdue`;
  - `property_sellability(property_id)` e índice parcial em `sellable = true`.
- Todas as tabelas pertencem ao schema `inventory`. Nenhuma FK ou join atravessa módulos.

DTOs HTTP serão records internos junto aos endpoints; Commands e Queries usarão records próprios da camada de aplicação; o mapeamento é manual e explícito, seguindo o padrão vigente da F02.

### Endpoints de API

> Os endpoints, schemas, autenticação, paginação e formato de erros são definidos no [API Contract](api-contract.yaml). Esta TechSpec não duplica essas definições.

| operationId | Onda | Caminho de Implementação |
|---|:--:|---|
| `getAvailability` | A | `AvailabilityEndpoints.GetAsync` → `GetAvailabilityQueryHandler` → `PropertySellability` + projeção de `DailyInventory` |
| `getPropertySellability` | A | `AvailabilityEndpoints.GetSellabilityAsync` → `GetPropertySellabilityQueryHandler` → `PropertySellability` |
| `getInventoryCalendar` | A | `InventoryCalendarEndpoints.GetAsync` → `GetInventoryCalendarQueryHandler` → projeção de `DailyInventory` + bloqueios ativos |
| `getDailyInventoryDetail` | A | `InventoryCalendarEndpoints.GetDayAsync` → `GetDailyInventoryDetailQueryHandler` → `DailyInventory` + `InventoryBlock` + `InventoryCommitment` + `InventoryHold` |
| `listAllotments` | A | `AllotmentEndpoints.ListAsync` → `ListAllotmentsQueryHandler` → projeção EF paginada |
| `createAllotment` | A | `AllotmentEndpoints.CreateAsync` → `CreateAllotmentCommandHandler` → `Allotment.Create` → `InventoryLedger.MaterializeAllotmentAsync` |
| `getAllotment` | A | `AllotmentEndpoints.GetAsync` → `GetAllotmentQueryHandler` → projeção EF |
| `updateAllotment` | A | `AllotmentEndpoints.UpdateAsync` → `UpdateAllotmentCommandHandler` → `Allotment.ChangeUnits` → recálculo do ledger |
| `cancelAllotment` | A | `AllotmentEndpoints.CancelAsync` → `CancelAllotmentCommandHandler` → `Allotment.Cancel` → zera `allotted_units` derivado |
| `listInventoryBlocks` | A | `InventoryBlockEndpoints.ListAsync` → `ListInventoryBlocksQueryHandler` → projeção EF paginada |
| `createInventoryBlock` | A | `InventoryBlockEndpoints.CreateAsync` → `CreateInventoryBlockCommandHandler` → `InventoryLedger.ApplyBlockAsync` → outbox |
| `previewInventoryBlockImpact` | A | `InventoryBlockEndpoints.PreviewAsync` → `PreviewInventoryBlockImpactQueryHandler` → leitura sem escrita e sem evento |
| `getInventoryBlock` | A | `InventoryBlockEndpoints.GetAsync` → `GetInventoryBlockQueryHandler` → projeção EF |
| `removeInventoryBlock` | A | `InventoryBlockEndpoints.RemoveAsync` → `RemoveInventoryBlockCommandHandler` → `InventoryBlock.Remove` → recálculo + `inventario-liberado` |
| `listInventoryRequests` | A | `InventoryRequestEndpoints.ListAsync` → `ListInventoryRequestsQueryHandler` → ordenação `priorityThenReceivedAt`, `overdue` no servidor |
| `createInventoryRequest` | A | `InventoryRequestEndpoints.CreateAsync` → `CreateInventoryRequestCommandHandler` → `IInventoryServiceWindow` deriva SLA e prioridade |
| `getInventoryRequest` | A | `InventoryRequestEndpoints.GetAsync` → `GetInventoryRequestQueryHandler` → projeção EF |
| `updateInventoryRequest` | A | `InventoryRequestEndpoints.UpdateAsync` → `UpdateInventoryRequestCommandHandler` → `InventoryRequest.Transition` |
| `createInventoryHold` | B | `InventoryHoldEndpoints.CreateAsync` → `CreateInventoryHoldCommandHandler` → `InventoryLedger.TryHoldAsync` → outbox |
| `getInventoryHold` | B | `InventoryHoldEndpoints.GetAsync` → `GetInventoryHoldQueryHandler` → projeção EF |
| `releaseInventoryHold` | B | `InventoryHoldEndpoints.ReleaseAsync` → `ReleaseInventoryHoldCommandHandler` → `InventoryLedger.ReleaseAsync`, idempotente |
| `commitInventoryHold` | B | `InventoryHoldEndpoints.CommitAsync` → `CommitInventoryHoldCommandHandler` → `InventoryLedger.CommitAsync` → outbox |
| `getInventoryMetrics` | A | `InventoryMetricsEndpoints.GetAsync` → `GetInventoryMetricsQueryHandler` → agregação `AsNoTracking` |

Eventos consumidos, sem endpoint correspondente:

| Evento | Onda | Caminho de Implementação |
|---|:--:|---|
| `curadoria-qualidade.propriedade-aprovada` | A | `CurationPropertyApprovedHandler` → gate `propertyApproved` + encerra bloqueio `curationSuspension` |
| `curadoria-qualidade.propriedade-suspensa` | A | `CurationPropertySuspendedHandler` → gate + bloqueio `curationSuspension` por acomodação |
| `curadoria-qualidade.conteudo-aprovado` | A | `CurationContentApprovedHandler` → gate `contentApproved` |
| `reserva.intencao-iniciada` | B | `ReservationIntentStartedHandler` → mesmo caminho de `createInventoryHold` |
| `reserva.confirmada` | B | `ReservationConfirmedHandler` → mesmo caminho de `commitInventoryHold` |
| `reserva.nao-concluida` | B | `ReservationNotCompletedHandler` → mesmo caminho de `releaseInventoryHold`, idempotente |

### Validações Adicionais

| Endpoint/operação | Validação | Local |
|---|---|---|
| Todas as mutações | Propriedade incorporada e acomodação pertencente à propriedade informada | handler |
| `createAllotment` | Período contínuo válido e ausência de sobreposição para a acomodação | domínio + índice de exclusão |
| `createAllotment` | `units < 2` é aceito e marca `belowCommercialFloor`, sem bloquear a operação | domínio |
| `updateAllotment` | Nova quantidade não pode ficar abaixo do comprometido em nenhuma data do período | `InventoryLedger` |
| `cancelAllotment` | Nenhuma data do período pode ter capacidade comprometida ou retida | `InventoryLedger` |
| `createInventoryBlock` (`planned`) | Quantidade não pode exceder o saldo livre de nenhuma data; nunca alcança retenção vigente ou reserva confirmada | `InventoryLedger` |
| `createInventoryBlock` (`emergency`) | Sempre aceito; exige `confirmEmergencyImpact: true`; invalida retenções e produz `bloqueio-afeta-reserva` sem cancelar reserva | domínio + `InventoryLedger` |
| `createInventoryBlock` | `reason: other` exige `reasonNote` | validator |
| `removeInventoryBlock` | Bloqueio de origem `curationSuspension` não é removível manualmente | domínio |
| `removeInventoryBlock` | Bloqueio já removido não é removido de novo | domínio |
| `createInventoryRequest` | `receivedAt` não pode ser futuro; SLA e prioridade são derivados no servidor e ignorados se enviados | validator + handler |
| `updateInventoryRequest` | Solicitação processada ou cancelada não retorna a pendente | domínio |
| `createInventoryHold` | Todas as noites da estadia precisam de saldo suficiente; recusa não separa capacidade alguma | `InventoryLedger` |
| `createInventoryHold` | `expiresAt` derivado do parâmetro global de quinze minutos, nunca do cliente | handler |
| `releaseInventoryHold` | Retenção já comprometida não é liberada | domínio |
| `commitInventoryHold` | Retenção expirada ou invalidada exige revalidação de saldo antes de comprometer | `InventoryLedger` |
| `getInventoryCalendar` | Intervalo máximo de 92 dias | validator |
| `getAvailability` | Estadia máxima de 30 noites; `checkOut` posterior a `checkIn` | validator |

### Mapeamento de Exceções para Problem Details

O `GlobalExceptionHandler` existente já traduz as exceções abaixo para RFC 9457 com `code`, `traceId`, `errors` e `metadata`. Nenhuma alteração no handler é necessária; apenas os códigos estáveis do contrato precisam ser usados.

| Condição | Exceção | HTTP | `code` |
|---|---|---:|---|
| Propriedade inexistente | `NotFoundException` | 404 | `PROPERTY_NOT_FOUND` |
| Acomodação inexistente ou de outra propriedade | `NotFoundException` | 404 | `ACCOMMODATION_NOT_FOUND` |
| Allotment, bloqueio, solicitação ou retenção inexistente | `NotFoundException` | 404 | `ALLOTMENT_NOT_FOUND` · `BLOCK_NOT_FOUND` · `REQUEST_NOT_FOUND` · `HOLD_NOT_FOUND` |
| Período de allotment sobreposto | `ConflictException` | 409 | `ALLOTMENT_PERIOD_OVERLAP` |
| `expectedRevision` obsoleto | `ConflictException` | 409 | `REVISION_MISMATCH` |
| Bloqueio já removido | `ConflictException` | 409 | `BLOCK_ALREADY_REMOVED` |
| Solicitação já encerrada | `ConflictException` | 409 | `REQUEST_ALREADY_CLOSED` |
| Retenção já comprometida | `ConflictException` | 409 | `HOLD_ALREADY_COMMITTED` |
| Chave idempotente com outro payload | `ConflictException` | 409 | `IDEMPOTENCY_KEY_REUSED` |
| Intervalo de datas incoerente | `BusinessRuleViolationException` | 422 | `INVALID_DATE_RANGE` |
| Calendário acima de 92 dias ou estadia acima de 30 noites | `BusinessRuleViolationException` | 422 | `DATE_RANGE_TOO_LARGE` |
| Redução abaixo do comprometido | `BusinessRuleViolationException` | 422 | `ALLOTMENT_BELOW_COMMITTED` |
| Bloqueio planejado acima do saldo livre | `BusinessRuleViolationException` | 422 | `INSUFFICIENT_FREE_BALANCE` |
| Emergencial sem confirmação explícita | `BusinessRuleViolationException` | 422 | `EMERGENCY_BLOCK_CONFIRMATION_REQUIRED` |
| `reason: other` sem `reasonNote` | `BusinessRuleViolationException` | 422 | `REASON_NOTE_REQUIRED` |
| Bloqueio de curadoria não removível | `BusinessRuleViolationException` | 422 | `CURATION_BLOCK_NOT_REMOVABLE` |
| Saldo insuficiente em alguma noite | `BusinessRuleViolationException` | 422 | `INSUFFICIENT_AVAILABILITY` |
| Retenção expirada e sem saldo para comprometer | `BusinessRuleViolationException` | 422 | `COMMITMENT_WITHOUT_AVAILABILITY` |
| Validação sintática do request | `ValidationException` | 400 | `BAD_REQUEST` |
| Falha inesperada | — | 500 | `INTERNAL_ERROR` |

`ALLOTMENT_BELOW_COMMITTED`, `INSUFFICIENT_FREE_BALANCE` e `INSUFFICIENT_AVAILABILITY` precisam preencher `metadata` com `conflictingDates`, `freeBalanceByDate` e `unavailableDates`. O `BuildMetadata` atual do `GlobalExceptionHandler` só popula `conflictingResourceId` a partir de `ConflictException`, e precisa ser estendido para carregar metadados arbitrários vindos de `BusinessRuleViolationException`.

---

## Inventário de Artefatos

### Arquivos a Criar

| Caminho | Tipo | Skills Aplicáveis | Descrição |
|---|---|---|---|
| `../localizestay-backend/.tasks/prd-controlar-allotment-disponibilidade-bloqueios-e-retencoes/api-contract.yaml` | Contrato | `restful-api` | Cópia versionada consumida pelos testes de contrato |
| `.../Inventory/Domain/DailyInventories/DailyInventory.cs` | Entidade | `dotnet-architecture`, `dotnet-code-quality` | Saldo por acomodação e data; invariantes de RN-03 |
| `.../Inventory/Domain/DailyInventories/InventoryLedger.cs` | Serviço de domínio | `dotnet-architecture` | Única fonte de mutação de saldo, com bloqueio pessimista ordenado |
| `.../Inventory/Domain/DailyInventories/InventoryLedgerResults.cs` | Value Objects | `dotnet-code-quality` | `HoldOutcome`, `CommitOutcome`, `BlockImpact`, motivos de recusa |
| `.../Inventory/Domain/Allotments/Allotment.cs` | Aggregate Root | `dotnet-architecture` | Quantidade cedida, período, revisão, piso comercial |
| `.../Inventory/Domain/Allotments/AllotmentValues.cs` | Enums | `dotnet-code-quality` | `AllotmentStatus` e códigos de cancelamento |
| `.../Inventory/Domain/InventoryBlocks/InventoryBlock.cs` | Aggregate Root | `dotnet-architecture` | Tipo, origem, motivo, período, remoção com histórico |
| `.../Inventory/Domain/InventoryBlocks/InventoryBlockValues.cs` | Enums | `dotnet-code-quality` | `BlockType`, `BlockOrigin`, `BlockReason`, `BlockStatus` |
| `.../Inventory/Domain/InventoryHolds/InventoryHold.cs` | Aggregate Root | `dotnet-architecture` | Retenção, prazo, transições e desfechos |
| `.../Inventory/Domain/InventoryHolds/InventoryCommitment.cs` | Entidade | `dotnet-architecture` | Conversão de retenção em capacidade comprometida |
| `.../Inventory/Domain/InventoryHolds/InventoryHoldValues.cs` | Enums | `dotnet-code-quality` | `HoldStatus`, `HoldReleaseReason` |
| `.../Inventory/Domain/InventoryRequests/InventoryRequest.cs` | Aggregate Root | `dotnet-architecture` | Fila, prioridade, SLA derivado, vínculo com a alteração |
| `.../Inventory/Domain/InventoryRequests/InventoryRequestValues.cs` | Enums | `dotnet-code-quality` | `RequestChannel`, `RequestType`, `RequestPriority`, `RequestStatus` |
| `.../Inventory/Domain/Sellability/PropertySellability.cs` | Entidade | `dotnet-architecture` | Projeção dos cinco gates de RN-07 |
| `.../Inventory/Domain/Sellability/SellabilityGate.cs` | Value Object | `dotnet-code-quality` | Código, status, detalhe e `ownerDomain` do gate |
| `.../Inventory/Domain/InventoryIdempotencyKey.cs` | Entidade | `dotnet-architecture` | Idempotência de bloqueio, retenção e comprometimento |
| `.../Inventory/Application/Timing/IInventoryServiceWindow.cs` | Porta | `dotnet-architecture` | Janela seg–sáb 08h–20h e derivação do SLA |
| `.../Inventory/Application/Availability/AvailabilityQueries.cs` | CQRS | `dotnet-performance` | `getAvailability` e `getPropertySellability` |
| `.../Inventory/Application/Availability/InventoryCalendarQueries.cs` | CQRS | `dotnet-performance` | Calendário e detalhe da data |
| `.../Inventory/Application/Allotments/AllotmentCommands.cs` | CQRS | `dotnet-architecture` | Ceder, alterar e cancelar allotment |
| `.../Inventory/Application/Allotments/AllotmentQueries.cs` | CQRS | `dotnet-performance` | Listagem paginada e detalhe |
| `.../Inventory/Application/InventoryBlocks/InventoryBlockCommands.cs` | CQRS | `dotnet-architecture` | Aplicar e remover bloqueio, com outbox |
| `.../Inventory/Application/InventoryBlocks/InventoryBlockQueries.cs` | CQRS | `dotnet-performance` | Listagem, detalhe e simulação de impacto |
| `.../Inventory/Application/InventoryRequests/InventoryRequestCommands.cs` | CQRS | `dotnet-architecture` | Registrar e atualizar situação da solicitação |
| `.../Inventory/Application/InventoryRequests/InventoryRequestQueries.cs` | CQRS | `dotnet-performance` | Fila ordenada e cálculo de `overdue` |
| `.../Inventory/Application/InventoryHolds/InventoryHoldCommands.cs` | CQRS | `dotnet-architecture` | Reter, liberar e comprometer |
| `.../Inventory/Application/InventoryHolds/InventoryHoldQueries.cs` | CQRS | `dotnet-performance` | Consulta de retenção |
| `.../Inventory/Application/InventoryHolds/InventoryHoldExpirationService.cs` | Hosted Service | `dotnet-architecture`, `dotnet-observability` | Varredura de expiração com `SKIP LOCKED` e eventos |
| `.../Inventory/Application/Metrics/InventoryMetricsQueries.cs` | CQRS | `dotnet-performance` | Sete indicadores do PRD por agregação direta |
| `.../Inventory/Application/Sellability/CurationPropertyApprovedHandler.cs` | Consumer | `dotnet-architecture` | Gate `propertyApproved` e encerramento do bloqueio de suspensão |
| `.../Inventory/Application/Sellability/CurationPropertySuspendedHandler.cs` | Consumer | `dotnet-architecture` | Suspensão por propriedade e bloqueio `curationSuspension` |
| `.../Inventory/Application/Sellability/CurationContentApprovedHandler.cs` | Consumer | `dotnet-architecture` | Gate `contentApproved` |
| `.../Inventory/Application/Sellability/SellabilityRecalculator.cs` | Serviço de aplicação | `dotnet-architecture` | Recálculo dos gates de D02 a cada mutação relevante |
| `.../Inventory/Application/Reservations/ReservationIntentStartedHandler.cs` | Consumer | `dotnet-architecture` | `reserva.intencao-iniciada` → retenção |
| `.../Inventory/Application/Reservations/ReservationConfirmedHandler.cs` | Consumer | `dotnet-architecture` | `reserva.confirmada` → comprometimento |
| `.../Inventory/Application/Reservations/ReservationNotCompletedHandler.cs` | Consumer | `dotnet-architecture` | `reserva.nao-concluida` → liberação idempotente |
| `.../Inventory/Application/Inventory/InventoryDtos.cs` | DTO | `dotnet-code-quality` | Respostas internas correspondentes aos schemas do contrato |
| `.../Inventory/Application/Inventory/InventoryMapper.cs` | Mapper | `dotnet-code-quality` | Mapeamento manual domínio → DTO |
| `.../Inventory/Application/Inventory/InventoryValidators.cs` | Validador | `dotnet-dependency-config` | FluentValidation de Commands e Queries da F03 |
| `.../Inventory/Infrastructure/Timing/ConfiguredInventoryServiceWindow.cs` | Adaptador | `dotnet-dependency-config` | Janela configurável com feriados e timezone IANA |
| `.../Inventory/Infrastructure/Configurations/DailyInventoryConfiguration.cs` | EF Mapping | `dotnet-dependency-config`, `dotnet-performance` | Chave composta e índices de saldo |
| `.../Inventory/Infrastructure/Configurations/AllotmentConfiguration.cs` | EF Mapping | `dotnet-dependency-config` | Revisão, período e índice de exclusão de sobreposição |
| `.../Inventory/Infrastructure/Configurations/InventoryBlockConfiguration.cs` | EF Mapping | `dotnet-dependency-config` | Tipo, origem, motivo e histórico de remoção |
| `.../Inventory/Infrastructure/Configurations/InventoryHoldConfiguration.cs` | EF Mapping | `dotnet-dependency-config`, `dotnet-performance` | Estados e índice parcial da varredura |
| `.../Inventory/Infrastructure/Configurations/InventoryCommitmentConfiguration.cs` | EF Mapping | `dotnet-dependency-config` | Vínculo com `reservationId` |
| `.../Inventory/Infrastructure/Configurations/InventoryRequestConfiguration.cs` | EF Mapping | `dotnet-dependency-config` | Fila, prioridade e índice de ordenação |
| `.../Inventory/Infrastructure/Configurations/PropertySellabilityConfiguration.cs` | EF Mapping | `dotnet-dependency-config` | Gates e índice parcial de vendáveis |
| `.../Inventory/Infrastructure/Configurations/InventoryIdempotencyKeyConfiguration.cs` | EF Mapping | `dotnet-dependency-config` | Unicidade por escopo e fingerprint |
| `.../Inventory/Infrastructure/Migrations/[timestamp]_AddInventoryControl.cs` | Migration | `dotnet-dependency-config` | Sete tabelas, índices e índice de exclusão |
| `.../Inventory/Infrastructure/Migrations/[timestamp]_AddInventoryControl.Designer.cs` | Migration Metadata | `dotnet-dependency-config` | Modelo EF gerado |
| `.../Inventory/Endpoints/AvailabilityEndpoints.cs` | Minimal API | `dotnet-architecture`, `restful-api` | `/availability` público, com `.AllowAnonymous()` e `.DisableRateLimiting()`, e `/sellability` |
| `.../Inventory/Endpoints/InventoryCalendarEndpoints.cs` | Minimal API | `dotnet-architecture`, `restful-api` | Calendário e detalhe da data |
| `.../Inventory/Endpoints/AllotmentEndpoints.cs` | Minimal API | `dotnet-architecture`, `restful-api` | Cinco operações de allotment |
| `.../Inventory/Endpoints/InventoryBlockEndpoints.cs` | Minimal API | `dotnet-architecture`, `restful-api` | Cinco operações de bloqueio, incluindo simulação |
| `.../Inventory/Endpoints/InventoryRequestEndpoints.cs` | Minimal API | `dotnet-architecture`, `restful-api` | Quatro operações da fila |
| `.../Inventory/Endpoints/InventoryHoldEndpoints.cs` | Minimal API | `dotnet-architecture`, `restful-api` | Quatro operações de retenção |
| `.../Inventory/Endpoints/InventoryMetricsEndpoints.cs` | Minimal API | `dotnet-architecture`, `restful-api` | Indicadores consolidados |
| `.../Curation/LocalizeStay.Modules.Curation.Contracts/CurationSellabilityEvents.cs` | Contrato de evento | `dotnet-architecture` | `CurationPropertyApprovedV1`, `CurationPropertySuspendedV1`, `CurationContentApprovedV1` |
| `.../Booking/LocalizeStay.Modules.Booking.Contracts/BookingIntegrationEvents.cs` | Contrato de evento | `dotnet-architecture` | `ReservationIntentStartedV1`, `ReservationConfirmedV1`, `ReservationNotCompletedV1` |
| `tests/LocalizeStay.UnitTests/Inventory/DailyInventoryTests.cs` | Teste | `dotnet-testing` | RN-03, piso zero e data sem allotment |
| `tests/LocalizeStay.UnitTests/Inventory/InventoryLedgerTests.cs` | Teste | `dotnet-testing` | Deltas, invariantes e recusas do ledger |
| `tests/LocalizeStay.UnitTests/Inventory/AllotmentTests.cs` | Teste | `dotnet-testing` | RN-01, RN-02, RN-07, piso comercial e revisão |
| `tests/LocalizeStay.UnitTests/Inventory/InventoryBlockTests.cs` | Teste | `dotnet-testing` | RN-03, RN-15, RN-16, planejado versus emergencial |
| `tests/LocalizeStay.UnitTests/Inventory/InventoryHoldTests.cs` | Teste | `dotnet-testing` | RN-04, RN-05, RN-06 e transições de estado |
| `tests/LocalizeStay.UnitTests/Inventory/InventoryRequestTests.cs` | Teste | `dotnet-testing` | RN-14, prioridade e derivação do SLA |
| `tests/LocalizeStay.UnitTests/Inventory/InventoryServiceWindowTests.cs` | Teste | `dotnet-testing` | Janela, feriados, horário de verão e exemplo do contrato |
| `tests/LocalizeStay.UnitTests/Inventory/PropertySellabilityTests.cs` | Teste | `dotnet-testing` | RN-07 e os cinco gates |
| `tests/LocalizeStay.UnitTests/Inventory/InventoryMetricsQueryHandlerTests.cs` | Teste | `dotnet-testing` | Sete indicadores e denominadores |
| `tests/LocalizeStay.UnitTests/Inventory/CurationSellabilityHandlerTests.cs` | Teste | `dotnet-testing` | Deduplicação e reordenação dos eventos de curadoria |
| `tests/LocalizeStay.IntegrationTests/Inventory/InventoryContractTests.cs` | Teste | `dotnet-testing`, `restful-api` | Conformidade das 23 operações com o contrato |
| `tests/LocalizeStay.IntegrationTests/Inventory/InventoryPersistenceTests.cs` | Teste | `dotnet-testing` | Migration, índices e índice de exclusão de sobreposição |
| `tests/LocalizeStay.IntegrationTests/Inventory/InventoryConcurrencyTests.cs` | Teste | `dotnet-testing` | Duas retenções simultâneas pela última unidade; ausência de deadlock |
| `tests/LocalizeStay.IntegrationTests/Inventory/InventoryLedgerReconciliationTests.cs` | Teste | `dotnet-testing` | Reconstrução dos contadores versus materializado |
| `tests/LocalizeStay.IntegrationTests/Inventory/AvailabilityEndpointsTests.cs` | Teste | `dotnet-testing` | Consulta pública, RN-07 e ausência de composição interna |
| `tests/LocalizeStay.IntegrationTests/Inventory/InventoryBlockEndpointsTests.cs` | Teste | `dotnet-testing` | Planejado, emergencial, simulação e remoção |
| `tests/LocalizeStay.IntegrationTests/Inventory/InventoryHoldLifecycleTests.cs` | Teste | `dotnet-testing` | Criar, expirar, liberar, comprometer e revalidar |
| `tests/LocalizeStay.IntegrationTests/Inventory/InventoryOutboxAndAuditTests.cs` | Teste | `dotnet-testing` | Atomicidade de saldo, auditoria e os seis eventos |
| `tests/LocalizeStay.IntegrationTests/Inventory/InventorySecurityTests.cs` | Teste | `dotnet-testing` | Cinco permissões, 401/403, `/availability` acessível sem token e isento do limiter, e ausência de vazamento da composição interna do saldo |
| `tests/LocalizeStay.IntegrationTests/Inventory/InventoryEndToEndTests.cs` | Teste | `dotnet-testing` | Allotment → bloqueio → retenção → comprometimento |
| `../localizestay-backend/docs/runbooks/inventory-control.md` | Runbook | `dotnet-observability` | Varredura de expiração, replay, gates de curadoria e diagnóstico |

> Caminhos abreviados com `.../Inventory/` expandem para `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/`; `.../Curation/` e `.../Booking/` seguem a mesma convenção sob `src/Modules/`.

### Arquivos a Modificar

| Caminho | Skills Aplicáveis | Alteração |
|---|---|---|
| `.../Inventory/InventoryModule.cs` | `dotnet-architecture`, `dotnet-dependency-config` | Registrar `IInventoryServiceWindow`, `IInventoryLedger`, opções de retenção e o hosted service de expiração |
| `.../Inventory/Infrastructure/InventoryDbContext.cs` | `dotnet-dependency-config` | Adicionar os sete novos `DbSet` |
| `.../Inventory/Infrastructure/Migrations/InventoryDbContextModelSnapshot.cs` | `dotnet-dependency-config` | Atualizar snapshot EF |
| `.../Inventory/Endpoints/InventoryEndpoints.cs` | `dotnet-architecture` | Mapear os sete novos grupos de endpoints |
| `.../Inventory/Application/Observability/InventoryTelemetry.cs` | `dotnet-observability` | Instrumentos, spans e tags da F03 |
| `.../Inventory/Application/CommercialOffers/CommercialRateCommands.cs` | `dotnet-architecture` | Disparar recálculo do gate `validRate` ao mutar tarifa |
| `.../Inventory/LocalizeStay.Modules.Inventory.csproj` | `dotnet-architecture` | Referenciar `Curation.Contracts` e `Booking.Contracts` |
| `../localizestay-backend/src/BuildingBlocks/LocalizeStay.SharedKernel/Security/PermissionRequirement.cs` | `restful-api`, `common-roles-naming` | Adicionar catálogo `InventoryControlPermissions` com as cinco permissões |
| `../localizestay-backend/src/BuildingBlocks/LocalizeStay.SharedKernel/Security/SecurityServiceCollectionExtensions.cs` | `restful-api` | Registrar as cinco policies `inventory:*` |
| `../localizestay-backend/src/BuildingBlocks/LocalizeStay.SharedKernel/ErrorHandling/BusinessRuleViolationException.cs` | `dotnet-architecture` | Carregar metadados estruturados do erro |
| `../localizestay-backend/src/BuildingBlocks/LocalizeStay.SharedKernel/ErrorHandling/GlobalExceptionHandler.cs` | `restful-api` | Propagar metadados de `BusinessRuleViolationException` para `metadata` |
| `../localizestay-deploy/envs/*/localizestay.stack.yml` | — | Middleware `ratelimit` no router `lstay-api`, com `sourceCriterion.ipstrategy.depth` correto para a cadeia Cloudflare → Traefik → app. Entrega de infraestrutura, não de backend |
| `../localizestay-backend/src/LocalizeStay.Api/appsettings.json` | `dotnet-dependency-config` | Seções `Inventory:InventoryServiceWindow` (seg–sáb 08–20h, `America/Fortaleza`, feriados nacionais), `Inventory:HoldExpiration` e allowlist de gates de curadoria |
| `../localizestay-backend/README.md` | `dotnet-code-quality` | Documentar contrato, permissões e certificação da F03 |

### Arquivos de Referência

| Caminho | Motivo da Consulta |
|---|---|
| `tasks/prd-controlar-allotment-disponibilidade-bloqueios-e-retencoes/api-contract.yaml` | Fonte soberana dos endpoints, schemas e códigos de erro |
| `domains/oferta-inventario/domain.md` | Entidades, RN-01 a RN-16 e eventos |
| `context/architecture-baseline.md` | Ownership, consistência, eventos e guardrails |
| `.../Inventory/Domain/CommercialOffers/CommercialOffer.cs` | Padrão de agregado, revisão e invariantes |
| `.../Inventory/Application/CommercialOffers/CurationOfferReturnedHandler.cs` | Padrão de consumidor idempotente de evento |
| `.../Inventory/Infrastructure/Timing/ConfiguredBusinessCalendar.cs` | Algoritmo de janela e feriados a espelhar em ADR-003 |
| `.../SharedKernel/Outbox/OutboxMessageFactory.cs` | Publicação confiável na mesma transação |
| `.../SharedKernel/Auditing/BusinessAuditWriter.cs` | Trilha de auditoria por módulo |
| `tests/LocalizeStay.IntegrationTests/Infrastructure/OpenApiContractDocument.cs` | Parser de contrato reutilizável criado pela F02 |
| `tests/LocalizeStay.IntegrationTests/Infrastructure/LocalizeStayWebApplicationFactory.cs` | JWT de teste e PostgreSQL via Testcontainers |

---

## Pontos de Integração

- **F02 / acomodações e tarifas:** `daily_inventory.accommodation_id` referencia `inventory.accommodations`, do mesmo schema e do mesmo módulo. O gate `validRate` é recalculado a partir de `commercial_rates`.
- **F01 / canal operacional:** o gate `testedChannel` deriva do canal registrado na incorporação, também do próprio schema.
- **D06 — Curadoria:** três eventos consumidos por handlers idempotentes. Não existe publicador ainda; os contratos são declarados em `Curation.Contracts` e os gates partem de configuração validada no startup até que D06 publique.
- **D03 — Reserva:** três eventos consumidos, cada um convergindo para o mesmo caminho de aplicação do endpoint HTTP correspondente. Os nomes `reservationIntentId` e `reservationId` ainda precisam de ratificação com D03.
- **D05 — Atendimento:** recebe `bloqueio-afeta-reserva` quando um bloqueio emergencial ou uma suspensão alcança datas com reserva confirmada. Nenhuma reserva é cancelada ou alterada pela F03.
- **D01 — Descoberta:** consome `GET /availability` público e os eventos `inventario-bloqueado` e `inventario-liberado`.
- **LogTo:** JWT fornece `sub`, nome, escopo `staff` e as cinco permissões. Atores nunca vêm no corpo da requisição.
- **WhatsApp e e-mail:** permanecem canais humanos. Registrar uma solicitação não altera inventário; o vínculo com a alteração só acontece quando o operador envia `requestId` no POST de allotment ou de bloqueio.

---

## Análise de Impacto

| Componente Afetado | Impacto | Descrição e Risco | Ação |
|---|---|---|---|
| `Inventory` | Alto | Sete tabelas novas, 23 operações e três consumidores de evento | Implementar por incrementos verticais, onda A antes da B |
| Schema `inventory` | Alto | Migration com índice de exclusão e chave composta | Testar em PostgreSQL real com Testcontainers |
| `SharedKernel.ErrorHandling` | Médio | `BusinessRuleViolationException` passa a carregar metadados | Manter compatibilidade com F01 e F02, que não usam o campo |
| `SharedKernel.Security` | Médio | Cinco permissões e policies novas | Testar 401, 403 e menor privilégio por operação |
| Endpoint público | Médio | Primeira superfície anônima da API. O `GlobalLimiter` atual recolhe todo tráfego anônimo em uma partição literal `"anonymous"`, que viraria gargalo global se a rota fosse submetida a ele | Isentar `getAvailability` do limiter com `.DisableRateLimiting()`; o controle por cliente é da borda. Resposta que nunca expõe composição interna do saldo. Ver ADR-005 |
| Borda HTTP (`localizestay-deploy`) | Médio | Rate limiting do endpoint público passa a ser entrega de infraestrutura, em Cloudflare e no middleware do router `lstay-api` | Configurar `sourceCriterion.ipstrategy.depth` corretamente para a cadeia Cloudflare → Traefik → app; validar com origens distintas |
| `Curation.Contracts` | Médio | Três eventos novos sem publicador | Aprovar payload e versionamento antes da Onda A |
| `Booking.Contracts` | Médio | Três eventos novos sem publicador | Aprovar com D03 antes da Onda B |
| F02 / tarifas | Baixo | Mutação de tarifa passa a recalcular gate | Cobrir com teste de regressão da F02 |
| `IBusinessCalendar` | Nenhum | Preservado por ADR-003 | Garantir por teste que o SLA da F01/F02 não muda |
| Outbox | Médio | Seis eventos novos e volume da varredura | Medir atraso e falhas de processamento |
| Frontend | Referência | Consumirá o contrato existente | TechSpec própria, fora deste documento |
| Deploy | Baixo | Nenhuma infraestrutura nova; um hosted service a mais | Aplicar migration antes de ativar o serviço de expiração |

O contrato HTTP não será modificado por esta TechSpec.

---

## Abordagem de Testes

### Testes Unitários

xUnit, AwesomeAssertions e padrão AAA. Mockar apenas relógio, janela de atendimento e outras portas externas — nunca o próprio domínio.

Cada regra de negócio do Domain Doc tem caso correspondente:

- **RN-01:** somente ator interno autenticado alcança os Commands.
- **RN-02:** inventário provém exclusivamente de allotment; sobreposição de período é recusada.
- **RN-03:** saldo é `allotted − committed − held − blocked`, com piso zero; data sem allotment vale zero.
- **RN-04:** a retenção nasce com prazo de quinze minutos derivado no servidor.
- **RN-05:** a retenção é liberada por expiração ou por término sem confirmação, uma única vez.
- **RN-06:** o comprometimento migra a capacidade de retida para comprometida sem alterar o total disponível.
- **RN-07:** os cinco gates são avaliados antes do saldo; falha de qualquer um torna a oferta não vendável.
- **RN-14:** SLA de quatro horas úteis derivado da janela seg–sáb 08h–20h, incluindo recebimento fora dela.
- **RN-15:** bloqueio emergencial é sempre aceito, invalida retenções e produz `bloqueio-afeta-reserva`.
- **RN-16:** nenhum bloqueio cancela ou altera reserva confirmada.

Casos de borda adicionais: redução de allotment abaixo do comprometido; bloqueio planejado maior que o saldo livre; bloqueio de curadoria não removível; retenção expirada recebendo `reserva.nao-concluida`; comprometimento de retenção expirada com e sem saldo; solicitação encerrada tentando voltar a pendente; horário de verão na conversão de timezone.

### Testes de Integração

`WebApplicationFactory` com PostgreSQL via Testcontainers:

- aplicar a migration e validar constraints, índices e o índice de exclusão de sobreposição;
- **teste de concorrência:** duas retenções simultâneas para a última unidade — exatamente uma vence, a outra recebe `422 INSUFFICIENT_AVAILABILITY` e nenhuma capacidade é separada;
- **teste de deadlock:** operações concorrentes sobre conjuntos de datas parcialmente sobrepostos concluem sem deadlock;
- **teste de reconciliação:** reconstruir os contadores de `daily_inventory` a partir de allotments, bloqueios, retenções e comprometimentos e comparar com o materializado;
- verificar transação única para saldo, auditoria e outbox em cada mutação;
- exercitar o ciclo completo de retenção: criar, expirar por varredura, liberar, comprometer e revalidar;
- verificar que retenção vencida não reduz saldo antes de a varredura passar;
- testar as cinco permissões, `/availability` anônimo e os status 400, 401, 403, 404, 409, 422, 429 e 500;
- testar paginação, filtros, ordenação `priorityThenReceivedAt` e listas vazias como `[]`;
- executar o fluxo F02 → allotment → bloqueio → retenção → comprometimento ponta a ponta.

### Testes de Contrato

Reutilizar `OpenApiContractDocument`, criado pela F02, para validar contra `api-contract.yaml`:

- exatamente 23 `operationId`, com métodos e paths correspondentes;
- todos os status declarados por operação, com **uma exceção registrada explicitamente**: o `429` de `getAvailability` é produzido pela borda, não pela aplicação, e não é exercitável em teste. O parser deve tratá-lo como exceção conhecida em vez de falhar (ver ADR-005);
- header `Location` nas respostas 201 e ausência de corpo nos 204;
- `application/problem+json` em todos os erros, com `code` e `traceId`;
- `metadata` presente em `ALLOTMENT_BELOW_COMMITTED`, `INSUFFICIENT_FREE_BALANCE` e `INSUFFICIENT_AVAILABILITY`;
- `getAvailability` acessível sem token; as demais 22 protegidas pela permissão declarada em `x-required-permissions`;
- `Idempotency-Key` obrigatório em `createInventoryBlock`, `createInventoryHold` e `commitInventoryHold`;
- campos obrigatórios dos schemas e os exemplos críticos de erro do contrato.

---

## Sequenciamento de Desenvolvimento

### Build Order

**Onda A — Inventário (RF-01 a RF-05)**

1. Criar `DailyInventory`, `Allotment` e `InventoryLedger` — sem dependências.
2. Criar `InventoryBlock` e as regras de planejado versus emergencial — depende de 1.
3. Criar `PropertySellability`, `SellabilityGate` e `SellabilityRecalculator` — depende de 1.
4. Criar `IInventoryServiceWindow`, `ConfiguredInventoryServiceWindow` e `InventoryRequest` — sem dependências.
5. Criar mappings EF, `DbSet`, migration e índices — depende de 1, 2, 3 e 4.
6. Estender `BusinessRuleViolationException` e `GlobalExceptionHandler` com metadados — sem dependências.
7. Registrar as cinco permissões e policies — sem dependências.
8. Implementar Commands, Queries, validators e mappers da Onda A — depende de 1 a 7.
9. Declarar `Curation.Contracts` e implementar os três consumidores de curadoria — depende de 3, 5 e 8.
10. Mapear as 19 operações Minimal API da Onda A — depende de 8 e 9.
11. Implementar `getInventoryMetrics` por agregação direta — depende de 5 e 8.
12. Criar testes unitários, de persistência, de concorrência e de contrato da Onda A — depende de 1 a 11.

**Onda B — Retenção (RF-06 a RF-08)**

13. Criar `InventoryHold`, `InventoryCommitment` e as operações de retenção no `InventoryLedger` — depende de 1 e 5.
14. Criar migration incremental das tabelas de retenção e seus índices — depende de 13.
15. Implementar `InventoryHoldExpirationService` e a guarda de retenção vencida na leitura — depende de 13 e 14.
16. Implementar os Commands e Queries de retenção e mapear as quatro operações restantes — depende de 13, 14 e 15.
17. Declarar `Booking.Contracts` e implementar os três consumidores de reserva — depende de 16.
18. Criar testes de ciclo de vida, concorrência de retenção, reconciliação e fluxo ponta a ponta — depende de 13 a 17.
19. Atualizar README e runbook — depende de 12 e 18.

### Dependências Técnicas Bloqueantes

- **Middleware de rate limit no router `lstay-api` bloqueia a exposição pública de `getAvailability`** em ambiente acessível externamente. É entrega de infraestrutura em `localizestay-deploy`, não de backend, e pode correr em paralelo à implementação — só precisa estar pronta antes da ativação da rota. Ver ADR-005.
- Ratificação dos nomes `inventory:*` no catálogo de Identidade e Acesso — não bloqueia. O contrato já segue a convenção vigente; a ratificação corre em paralelo à implementação.
- Payload dos três eventos de curadoria — não bloqueia. Declarar `V1` com payload mínimo e seguir; campos adicionais são compatíveis quando D06 existir.
- Payload dos três eventos de reserva — não bloqueia. Apenas os nomes `reservationIntentId` e `reservationId` devem ser travados com D03 antes da Onda B, e ambos já constam do contrato HTTP público da F03.
- Confirmação da escala que sustenta a janela seg–sáb 08h–20h e dos feriados municipais — afeta a certificação do SLA, não a implementação. É configuração sem deploy.
- Nenhuma infraestrutura nova é necessária.

---

## Monitoramento e Observabilidade

Métricas OpenTelemetry, com tags de baixa cardinalidade — identificadores viajam apenas em spans e escopos de log:

- `inventory.allotment.granted` e `inventory.allotment.changed`, por resultado;
- `inventory.block.applied`, por `type` e `origin`;
- `inventory.block.emergency_latency` — histograma do commit ao efeito na consulta de disponibilidade, base da métrica de um minuto do PRD;
- `inventory.block.affects_reservation` — contador de casos críticos enviados a D05;
- `inventory.hold.created`, `inventory.hold.rejected`, `inventory.hold.expired`, `inventory.hold.released`, `inventory.hold.committed`;
- `inventory.hold.expiration_backlog` — profundidade da fila de retenções vencidas ainda não processadas;
- `inventory.hold.commit_revalidated` — comprometimentos que exigiram revalidação por retenção expirada;
- `inventory.request.sla` por resultado, e `inventory.request.received_outside_window`;
- `inventory.sellability.gate_changed`, por gate e resultado;
- `inventory.availability.query_duration` — histograma do endpoint público; sustenta, junto dos logs de `429` do Traefik, a calibração do limite na borda;
- `inventory.metrics.coverage_duration` — histograma da agregação de cobertura de inventário, a mais cara das sete e o gatilho formal de reavaliação da decisão de agregação direta;
- `inventory.outbox.failures` — instrumento existente, reutilizado.

Logs estruturados usam `propertyId`, `accommodationId`, `allotmentId`, `blockId`, `holdId`, `requestId`, `operation`, `result`, `eventId` e `correlationId`. Nunca registram dados do viajante, que pertencem a D03.

Spans customizados:

- `inventory.ledger.load`;
- `inventory.allotment.materialize`;
- `inventory.block.apply`;
- `inventory.hold.acquire`;
- `inventory.hold.expire`;
- `inventory.availability.query`.

Alertas:

- qualquer amostra de `inventory.block.emergency_latency` acima de sessenta segundos — viola a meta do PRD;
- `inventory.hold.expiration_backlog` saturando o lote em ciclos consecutivos;
- outbox sem processamento após o limite de retentativas;
- divergência detectada pelo teste de reconciliação em ambiente não produtivo;
- `inventory.metrics.coverage_duration` com p95 acima de 2s sustentado por 7 dias — abre o ADR de projeção assíncrona;
- taxa anormal de `429` no router `lstay-api`, observada na borda, que indica limite mal calibrado ou abuso real;
- limiares de SLA e de latência do endpoint público serão calibrados durante o piloto.

---

## Considerações Técnicas

### Decisões Principais

- **Saldo materializado por noite** — ADR-001.
  - Racional: a checagem "todas as noites têm saldo" e o corte de vendas em um minuto viram transações curtas sobre linhas determinísticas.
  - Trade-off: contadores derivados persistidos, com risco de divergência, mitigado por escrita única via `InventoryLedger` e teste de reconstrução.
  - Alternativas rejeitadas: saldo derivado por query com advisory lock; híbrido com `allotted` derivado.
- **Gates de RN-07 em projeção local** — ADR-002.
  - Racional: `GET /availability` é público e quente; D06 ainda não existe como módulo.
  - Trade-off: estado eventualmente consistente para dois dos cinco gates.
- **Janela de atendimento em calendário nomeado próprio** — ADR-003.
  - Racional: unificar mudaria silenciosamente o SLA já certificado da F01 e da F02.
  - Trade-off: dois calendários configuráveis coexistindo no módulo.
- **Expiração por varredura com guarda de leitura** — ADR-004.
  - Racional: a varredura garante o evento; a guarda garante a disponibilidade imediata.
  - Trade-off: um hosted service a mais e uma regra de filtro que precisa aparecer em toda leitura de saldo.
- **Métricas por agregação direta**, contrariando o `x-backend-notes` da operação.
  - Racional: ADR-0002 proíbe infraestrutura nova sem evidência medida, e a escala do piloto é de oito propriedades em noventa dias.
  - Trade-off: o endpoint pode ficar lento com volume; `inventory.metrics.query_duration` é o gatilho formal de reavaliação.
- **Índice de exclusão PostgreSQL para sobreposição de allotment**, em vez de checagem apenas no handler.
  - Racional: dois `POST` concorrentes precisam produzir `409`, não dois allotments sobrepostos.
- **Persistência via EF Core direto nos handlers e mapeamento manual**, seguindo o padrão vigente do módulo.
- **Rate limiting do endpoint público inteiramente na borda** — ADR-005.
  - Racional: a partição `"anonymous"` compartilhada transformaria a consulta pública em gargalo global, e particionar por IP dentro da aplicação exigiria alterar o pipeline do host inteiro por um requisito de uma rota. A borda já existe, conhece o IP real e barra o abuso antes de consumir thread ou conexão.
  - Trade-off: o `429` do endpoint público vem da borda e não segue o formato RFC 9457 do contrato; em desenvolvimento e teste não há limite algum.
  - Alternativas rejeitadas: particionar por IP na aplicação com `ForwardedHeaders`; elevar os limites mantendo a partição única.
- **Permissões sem hierarquia embutida:** `inventory:write` não concede `inventory:read`, ao contrário do caso especial existente para `commercial-offers`.
  - Racional: composição de acesso pertence à role no LogTo, onde é visível em revisão de segurança; hierarquia no handler cresce sem controle.
  - Trade-off: um operador precisa receber as permissões explicitamente.

### Riscos Conhecidos

- **Divergência dos contadores materializados:** mitigar com escrita exclusiva pelo `InventoryLedger` e teste de reconciliação em cada execução da suíte de integração.
- **Deadlock entre operações com datas parcialmente sobrepostas:** mitigar com ordem de aquisição `ORDER BY date` obrigatória em todos os caminhos, coberta por teste.
- **Suspensão de curadoria em propriedade grande tocando muitas linhas:** processar por acomodação, cada uma em sua transação.
- **Endpoint público como superfície de abuso:** rate limit por IP na borda e resposta que nunca expõe a composição interna do saldo. Ver ADR-005.
- **`ipstrategy.depth` incorreto no Traefik:** com a cadeia Cloudflare → Traefik → app, `X-Forwarded-For` chega com mais de um endereço; `depth` errado particiona pelo IP da Cloudflare e recria um gargalo global que *parece* configurado. Validar com requisições de origens distintas e conferir `forwardedHeaders.trustedIPs`.
- **Bypass da borda:** mitigado pela topologia — o serviço `api` não publica portas e só é alcançável pela overlay `traefik-public`. Publicar porta anularia a proteção inteira; a premissa precisa ser preservada.
- **Gates de curadoria partindo de configuração:** a origem precisa aparecer na resposta de `sellability` e no runbook, para que ninguém interprete configuração como decisão de D06. A allowlist com default `blocked` garante que o erro possível seja omitir uma propriedade aprovada — detectável em minutos pela Operação — e não o inverso, que produziria venda sem lastro.
- **Escrita amplificada em allotments longos:** noventa linhas por allotment é aceitável no piloto; monitorar antes de ampliar a janela.
- **Retenção invalidada confundida com expirada:** estados distintos e `invalidatedByBlockId` preenchido apenas na invalidação.
- **Divergência de timezone entre F01/F02 e F03:** registrada como questão em aberto para ratificação da Operação.

### Requisitos Especiais

- Latência do bloqueio emergencial: no máximo sessenta segundos do commit ao corte de novas vendas, medido por `salesStoppedAt`.
- Trilha de auditoria com autor, horário e motivo em toda alteração de capacidade, distinta dos logs de diagnóstico.
- Acesso restrito à equipe interna, com negação por padrão e permissões específicas por operação.
- `GET /availability` não expõe composição interna do saldo nem dado algum do viajante.
- Quantidades sempre em unidades inteiras de acomodação.
- Duração da retenção é parâmetro global fixo, nunca recebido do cliente.
- Código, classes e membros em inglês; termos de negócio preservados na documentação.

### Conformidade com Skills

- CQRS nativo, domínio e exceções seguem `dotnet-architecture`.
- Naming, DI e `CancellationToken` seguem `dotnet-code-quality`.
- PostgreSQL, EF Core, FluentValidation, options validadas e outbox seguem `dotnet-dependency-config`.
- Testes seguem `dotnet-testing`, com Testcontainers para toda verificação que dependa de comportamento real do PostgreSQL.
- Telemetria segue `dotnet-observability`, com métricas de baixa cardinalidade.
- Projeções, índices e ausência de cache seguem `dotnet-performance`.
- OpenAPI, versionamento e Problem Details seguem `restful-api`.
- Nomes de permissão seguem `common-roles-naming`.

Desvios deliberados:

| Desvio | Skill | Justificativa |
|---|---|---|
| Um projeto por módulo, em vez de projetos por camada | `dotnet-architecture` | Baseline e código existente usam encapsulamento modular por assembly e tipos `internal` |
| Handlers usam `InventoryDbContext` diretamente, sem repositório | `dotnet-architecture` | O contexto já é Unit of Work; repositório adicional não agregaria comportamento |
| Mapeamento manual em vez de Mapster | `dotnet-dependency-config` | Alternativa permitida e já adotada no módulo |
| SQL bruto para `SELECT ... FOR UPDATE` e `SKIP LOCKED` | `dotnet-performance` | O EF Core não expõe bloqueio pessimista; a invariável de concorrência exige o comando explícito |
| Sem cache, apesar do endpoint público | `dotnet-performance` | ADR-0002 e a escala do piloto não justificam Redis; cache jamais pode ser fonte de verdade de disponibilidade |
| Agregação direta em `/inventory-metrics` | `restful-api` | Contraria o `x-backend-notes`, que sugere projeção assíncrona; ADR-0002 prevalece, com gatilho de reavaliação documentado |

---

## Questões em Aberto

### Decidido nesta TechSpec

- **Timezone da janela de atendimento:** adotado `America/Fortaleza` nas duas seções de calendário, e não `America/Sao_Paulo`. Os dois fusos são UTC−3 fixo desde 2019, então nenhum instante calculado muda; Fortaleza alinha o piloto do Nordeste ao calendário já vigente em F01/F02 e permanece imune a um eventual retorno do horário de verão. `America/Sao_Paulo` aparecia uma única vez no contrato, em `info.description`, sem schema dependente; a correção foi registrada como errata no próprio `api-contract.yaml` e no `api-contract.md`. Ver ADR-003.
- **Feriados do SLA:** lista inicial com os feriados nacionais de 2026 e 2027; estaduais e municipais entram quando os destinos do piloto forem definidos. É configuração sem deploy e não bloqueia implementação. Atenção ao efeito específico da F03: como a janela inclui **sábado**, feriado que caia no sábado passa a importar, o que era irrelevante na janela seg–sex da F01/F02.
- **Gates iniciais de curadoria:** allowlist explícita de `propertyId` em configuração, com default `blocked`, seguindo o padrão de `UpstreamEligibilityOptions`. Ausência nunca significa aprovação. Ver ADR-002.
- **Rate limit do endpoint público:** delegado integralmente à borda — Cloudflare e middleware do Traefik no router `lstay-api`. A aplicação apenas isenta `getAvailability` do limiter com `.DisableRateLimiting()`; o ramo autenticado segue inalterado. O `429` produzido pela borda tem corpo vazio e não segue o formato RFC 9457 do contrato, o que é aceito explicitamente como condição de infraestrutura — e não é regressão, porque a Cloudflare sempre pôde emitir `429` antes da aplicação. Ver ADR-005.
- **Desvio consciente do contrato em `getInventoryMetrics`:** o `x-backend-notes` pede apuração por projeção assíncrona; esta TechSpec adota agregação direta por força do ADR-0002. Gatilho de reavaliação fixado em número: **p95 acima de 2s sustentado por 7 dias com dados reais do piloto abre o ADR de projeção**. A agregação a instrumentar é a de cobertura de inventário, que cruza `daily_inventory` inteiro e é a mais cara das sete. Nenhum campo, status ou schema do contrato é alterado.

### Pendente de ratificação — não bloqueia implementação

- [ ] **Nomes das cinco permissões `inventory:*`:** o contrato já segue a convenção `<recurso-kebab>:<ação>` estabelecida por `portfolio-onboarding:*` e `commercial-offers:*` em `LogToOptions.cs`. A ratificação é formalidade. Duas ressalvas a registrar no catálogo de Identidade e Acesso:
  - `portfolio-onboarding` e `commercial-offers` nomeiam capacidades, enquanto `inventory` nomeia o módulo, que também abriga F01 e F02. Documentar explicitamente que a permissão cobre **controle de inventário** — allotment, bloqueios, retenções — e não as capacidades da F01/F02.
  - **Não replicar** a hierarquia hard-coded existente em `PermissionRequirement.cs`, onde `commercial-offers:write` concede `commercial-offers:read`. Quem precisa de escrita e leitura recebe as duas permissões via role no LogTo, que é onde composição de acesso pertence. Hierarquia embutida no handler é invisível em revisão de segurança.
- [ ] **Payload dos três eventos de curadoria:** declarar `V1` com o payload mínimo que a F03 consome e seguir. Adicionar campo é compatível; renomear ou remover não é — payload mínimo maximiza a chance de que D06 apenas acrescente.
- [ ] **Payload dos três eventos de reserva:** mesma regra. A exceção que vale travar com D03 antes da Onda B são apenas os dois nomes `reservationIntentId` e `reservationId`, que já constam do contrato HTTP público da F03 e portanto já estão comprometidos externamente.
- [ ] Nenhum outro conflito com o API Contract foi identificado.

---

## Architecture Decision Records

- [ADR-001: Materializar o inventário diário como tabela de saldo com bloqueio pessimista por noite](adrs/adr-001.md) — traduz RN-03 em uma linha por noite e resolve a concorrência da última unidade com `FOR UPDATE` ordenado.
- [ADR-002: Espelhar os gates de vendabilidade (RN-07) em projeção local alimentada por eventos](adrs/adr-002.md) — permite avaliar RN-07 sem chamada síncrona entre módulos e viabiliza RF-05 antes de D06 existir.
- [ADR-003: Janela de atendimento do inventário como calendário nomeado próprio](adrs/adr-003.md) — atende a janela seg–sáb 08h–20h sem alterar o SLA já certificado da F01 e da F02.
- [ADR-004: Expirar retenções por varredura em background com guarda na leitura do saldo](adrs/adr-004.md) — garante a publicação dos eventos sem introduzir janela morta de disponibilidade.
- [ADR-005: Delegar o rate limiting do endpoint público à borda (Cloudflare + Traefik)](adrs/adr-005.md) — isenta `getAvailability` do limiter da aplicação e move o controle por cliente para onde a infraestrutura já resolve declarativamente.
- [ADR-0001 global: Backend .NET modular](../../docs/adr/ADR-0001-backend-dotnet-monolito-modular.md) — plataforma e fronteiras.
- [ADR-0002 global: PostgreSQL único e infraestrutura distribuída adiada](../../docs/adr/ADR-0002-postgresql-unico-adiamento-mongo-redis-broker.md) — persistência, outbox e ausência de cache e broker.
- [ADR-0006 global: LogTo](../../docs/adr/ADR-0006-logto-provedor-identidade.md) — autenticação e escopo `staff`.
- [ADR-0007 global: OpenTelemetry e Grafana](../../docs/adr/ADR-0007-observabilidade-otel-grafanacloud.md) — observabilidade.
- [ADR-0010 global: Autorização local](../../docs/adr/ADR-0010-autorizacao-local-ecad-authz-como-referencia.md) — enforcement de permissão no módulo.
- [ADR F02-002: Oferta comercial como agregado com resumos transacionais](../prd-estruturar-acomodacoes-tarifas-e-politicas/adrs/adr-002.md) — precedente de consistência forte sem projeção assíncrona.

---

## Próximos Passos

1. Usar `flow-task-creator` referenciando esta TechSpec para gerar as tarefas de implementação, separadas por onda.
2. Usar `flow-frontend-techspec-creator` para o calendário de inventário do backoffice.
3. Resolver as questões em aberto bloqueantes antes dos passos 7, 9 e 17 do Build Order.
