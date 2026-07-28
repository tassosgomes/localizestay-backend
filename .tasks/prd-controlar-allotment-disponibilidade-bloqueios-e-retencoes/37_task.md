---
status: pending
parallelizable: true
blocked_by: ["5.0"]
---

<task_context>
<domain>inventory/domain/inventory-holds</domain>
<type>implementation</type>
<scope>core_feature</scope>
<complexity>medium</complexity>
<dependencies>temporal</dependencies>
<unblocks>"38.0, 39.0"</unblocks>
<vertical_slice>Uma retenção nasce com prazo de quinze minutos derivado no servidor e transita para exatamente um de cinco estados terminais distintos.</vertical_slice>
</task_context>

# Tarefa 37.0: Modelar `InventoryHold` com prazo e cinco estados terminais

## Relacionada às User Stories

- [US-04] Como viajante, quero que a acomodação escolhida fique separada enquanto concluo o checkout (cobertura direta)

## Visão Geral

`InventoryHold` é a separação temporária de capacidade durante o checkout. É o mecanismo que impede que duas jornadas concorrentes vendam a mesma unidade.

O ponto mais fácil de errar é confundir **expirada** com **invalidada**. São estados distintos com causas distintas: a expiração é a passagem do tempo; a invalidação é um bloqueio emergencial. `invalidatedByBlockId` só é preenchido no segundo caso.

## Requisitos

- Cinco estados: `held`, `expired`, `released`, `committed`, `invalidated`. Todos terminais exceto `held`.
- `ExpiresAt` derivado do **parâmetro global de quinze minutos**, calculado no servidor. Nunca recebido do cliente.
- `CheckIn` inclusivo e `CheckOut` **exclusivo**; `Nights` derivado.
- `ReservationIntentId` vindo de D03; índice de deduplicação de intenção (tarefa 38.0).
- `InvalidatedByBlockId` preenchido **apenas** na invalidação por bloqueio emergencial.
- `ReleaseReason` com `reservationNotCompleted`, `checkoutAbandoned` e `operationalCorrection`.
- Transições ilegais são recusadas: retenção `committed` não é liberada (`HOLD_ALREADY_COMMITTED`); retenção já encerrada não muda de estado uma segunda vez.
- Uma retenção só conta como retida quando `Status = held` **e** `ExpiresAt > now()` — a guarda de ADR-004 é expressa no próprio agregado.

## Arquivos Envolvidos

- **Criar:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Domain/InventoryHolds/InventoryHold.cs`
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Domain/InventoryHolds/InventoryHoldValues.cs`
  - `../localizestay-backend/tests/LocalizeStay.UnitTests/Inventory/InventoryHoldTests.cs`
- **Referência:**
  - `.tasks/prd-controlar-allotment-disponibilidade-bloqueios-e-retencoes/api-contract.md` (schema `InventoryHold`)
  - `.tasks/prd-controlar-allotment-disponibilidade-bloqueios-e-retencoes/adrs/adr-004.md`
  - `../localizestay-backend/src/BuildingBlocks/LocalizeStay.SharedKernel/Time/IClock.cs`
- **Skills para consultar durante implementação:**
  - `dotnet-architecture` — aggregate root com máquina de estados explícita
  - `dotnet-code-quality` — enums, propriedades derivadas, sem setters públicos
  - `dotnet-testing` — mockar `IClock`; `[Theory]` para a matriz de transições

## Subtarefas

- [ ] 37.1 Declarar `HoldStatus` e `HoldReleaseReason` em `InventoryHoldValues.cs`.
- [ ] 37.2 Modelar o agregado com factory `Create`, prazo derivado de quinze minutos e `Nights` calculado de `checkIn`/`checkOut` exclusivo.
- [ ] 37.3 Implementar as quatro transições — `Expire`, `Release`, `Commit`, `InvalidateBy` — com recusa de transição ilegal.
- [ ] 37.4 Testar a matriz completa de transições, a distinção expirada × invalidada e a propriedade `IsActive` no instante exato de `ExpiresAt`.

## Sequenciamento

- Bloqueado por: 5.0
- Desbloqueia: 38.0, 39.0
- Paralelizável: Sim; domínio puro. Pode começar em paralelo à Fase 6 da Onda A.

## Rastreabilidade

- Esta tarefa cobre: RN-04, RN-05 e RN-06 no domínio, e o schema `InventoryHold` do contrato.
- Evidência esperada: `InventoryHoldTests` prova a matriz de transições e a distinção entre os cinco estados.

## Detalhes de Implementação

Máquina de estados:

```
                ┌──> expired      (prazo terminou — varredura, tarefa 41.0)
                ├──> released     (liberação explícita, com motivo)
held ───────────┼──> committed    (reserva confirmada)
                └──> invalidated  (bloqueio emergencial; invalidatedByBlockId preenchido)

committed ──> Release()  ==>  409 HOLD_ALREADY_COMMITTED
qualquer terminal ──> transição  ==>  sem efeito (idempotente) ou recusa explícita
```

Semântica de período — a diferença que causa bug:

| Campo | Semântica | Exemplo |
|---|---|---|
| `checkIn` | inclusivo | 2026-09-14 |
| `checkOut` | **exclusivo** | 2026-09-17 |
| `nights` | derivado | 3 (14, 15, 16) |

> A noite de 17/09 **não** é retida. Reter a data de checkout consumiria uma unidade que deveria estar disponível para o próximo hóspede — um erro que só aparece quando a ocupação está cheia, ou seja, exatamente quando importa.

Guarda de ADR-004 expressa no agregado:

```csharp
public bool IsActive(DateTimeOffset now) => Status == HoldStatus.Held && ExpiresAt > now;
```

**Convenções da stack (das skills consultadas):**

- Prazo derivado de configuração global, injetada, nunca do cliente (`dotnet-architecture`).
- `IClock` injetado; nenhum `DateTimeOffset.UtcNow` inline (`dotnet-testing`).
- Enums em arquivo próprio, seguindo o padrão do módulo (`dotnet-code-quality`).
- Nenhum dado do viajante no agregado — apenas `reservationIntentId` e `reservationId`.

## Critérios de Sucesso (Verificáveis)

- [ ] Testes passam: `dotnet test ../localizestay-backend/LocalizeStay.sln --filter "FullyQualifiedName~InventoryHoldTests"`
- [ ] Build compila sem erros: `dotnet build ../localizestay-backend/LocalizeStay.sln --no-restore`
- [ ] Formatação válida: `dotnet format ../localizestay-backend/LocalizeStay.sln --verify-no-changes --no-restore`
- [ ] `ExpiresAt` é sempre `heldAt + 15 minutos`, independentemente de qualquer valor enviado.
- [ ] Estadia de 14/09 a 17/09 produz `nights = 3`.
- [ ] `IsActive` é `false` no instante exato de `ExpiresAt`.
- [ ] Retenção `committed` recusa `Release` com `HOLD_ALREADY_COMMITTED`.
- [ ] `invalidatedByBlockId` é preenchido apenas em `invalidated`, nunca em `expired`.
- [ ] Transição sobre estado terminal não altera o agregado uma segunda vez.
