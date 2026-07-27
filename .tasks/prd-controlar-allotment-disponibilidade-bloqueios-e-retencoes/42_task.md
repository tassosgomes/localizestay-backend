---
status: pending
parallelizable: false
blocked_by: ["1.0", "10.0", "39.0", "40.0", "41.0"]
---

<task_context>
<domain>inventory/application/inventory-holds</domain>
<type>implementation</type>
<scope>core_feature</scope>
<complexity>high</complexity>
<dependencies>database</dependencies>
<unblocks>"43.0, 45.0, 46.0"</unblocks>
<vertical_slice>Reter, liberar e comprometer inventário funcionam de ponta a ponta na camada de aplicação, com idempotência, outbox e os eventos da Onda B.</vertical_slice>
</task_context>

# Tarefa 42.0: Reter, liberar, comprometer e consultar retenção

> ⚠️ **`complexity: high` — exige revisão humana do plano antes de implementar.** Os três commands compartilham idempotência, transição de estado e outbox, e o comprometimento de retenção expirada exige revalidação de saldo no mesmo caminho.

## Relacionada às User Stories

- [US-04] Acomodação separada durante o checkout (cobertura direta)

## Visão Geral

Quatro operações: `createInventoryHold`, `releaseInventoryHold`, `commitInventoryHold` e `getInventoryHold`. É a fatia que traduz RF-06, RF-07 e RF-08 em capacidade de aplicação.

`createInventoryHold` é o ponto de maior concorrência do sistema. Duas intenções concorrentes pela última unidade da data: exatamente uma retenção é criada, a outra recebe `422 INSUFFICIENT_AVAILABILITY` — e **nenhuma capacidade é separada** na perdedora.

## Requisitos

- `Idempotency-Key` obrigatório em `createInventoryHold` (escopo `inventoryHoldCreation`) e `commitInventoryHold` (escopo `inventoryHoldCommitment`).
- `expiresAt` derivado do parâmetro global de quinze minutos, **no handler**, nunca do cliente.
- Recusa por saldo insuficiente produz `422 INSUFFICIENT_AVAILABILITY` com `metadata.unavailableDates`, sem separar capacidade alguma.
- `releaseInventoryHold` é **idempotente por desenho**: retenção já expirada, liberada ou invalidada responde `204` sem devolver capacidade duas vezes. Retenção `committed` produz `409 HOLD_ALREADY_COMMITTED`.
- `commitInventoryHold` com retenção vigente migra a capacidade sem alterar o total disponível; com retenção expirada, revalida e devolve `revalidated: true`, ou recusa com `422 COMMITMENT_WITHOUT_AVAILABILITY`.
- Eventos na outbox, na mesma transação: `inventario-retido` na criação, `inventario-liberado` na liberação efetiva, `inventario-comprometido` no comprometimento.
- Trilha de auditoria em cada mutação.
- Instrumentar `inventory.hold.created`, `.rejected`, `.released`, `.committed` e `.commit_revalidated`; span `inventory.hold.acquire`.
- DTOs, mapeamentos e validators da Onda B acrescentados aos arquivos compartilhados criados em 15.0 e 16.0.

## Arquivos Envolvidos

- **Criar:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/InventoryHolds/InventoryHoldCommands.cs`
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/InventoryHolds/InventoryHoldQueries.cs`
  - `../localizestay-backend/tests/LocalizeStay.UnitTests/Inventory/InventoryHoldCommandHandlerTests.cs`
- **Modificar:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/Inventory/InventoryDtos.cs` (DTOs da Onda B)
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/Inventory/InventoryMapper.cs` (mapeamentos da Onda B)
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/Inventory/InventoryValidators.cs` (regra de estadia da retenção)
- **Referência:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Domain/DailyInventories/InventoryLedger.cs` (estendido em 39.0)
  - `.tasks/prd-controlar-allotment-disponibilidade-bloqueios-e-retencoes/api-contract.md` (exemplos das quatro operações de retenção)
  - `../localizestay-backend/src/BuildingBlocks/LocalizeStay.SharedKernel/Outbox/OutboxMessageFactory.cs`
- **Skills para consultar durante implementação:**
  - `dotnet-architecture` — CQRS, outbox na mesma transação, idempotência
  - `dotnet-observability` — contadores e span da retenção
  - `dotnet-code-quality` — `CancellationToken`, métodos curtos
  - `dotnet-testing` — AAA cobrindo a matriz de estados

## Subtarefas

- [ ] 42.1 Implementar `CreateInventoryHoldCommandHandler` com idempotência, prazo derivado no servidor, `INSUFFICIENT_AVAILABILITY` com `metadata.unavailableDates` e `inventario-retido` na outbox.
- [ ] 42.2 Implementar `ReleaseInventoryHoldCommandHandler` idempotente, com `HOLD_ALREADY_COMMITTED` e `inventario-liberado` apenas quando a transição efetivamente ocorre.
- [ ] 42.3 Implementar `CommitInventoryHoldCommandHandler` nos dois caminhos, com `revalidated` e `COMMITMENT_WITHOUT_AVAILABILITY`, e `inventario-comprometido` na outbox.
- [ ] 42.4 Implementar `GetInventoryHoldQueryHandler`; acrescentar DTOs, mapeamentos e validators da Onda B; instrumentar os cinco contadores e o span; testar a matriz completa.

## Sequenciamento

- Bloqueado por: 1.0, 10.0, 39.0, 40.0, 41.0
- Desbloqueia: 43.0, 45.0, 46.0
- Paralelizável: Não; modifica os três arquivos de contrato interno compartilhados criados em 15.0 e 16.0.

## Rastreabilidade

- Esta tarefa cobre: RF-06, RF-07 e RF-08 na camada de aplicação; RN-04, RN-05 e RN-06.
- Evidência esperada: `InventoryHoldCommandHandlerTests` prova a matriz; 47.0 prova o ciclo fim a fim; 48.0 prova a concorrência real.

## Detalhes de Implementação

Critérios de aceite mapeados:

| Critério | RF | Verificação |
|---|:--:|---|
| Intenção com saldo cria retenção com prazo e produz `inventario-retido` | 06 | `201` + evento |
| Intenção sem saldo em alguma noite não separa capacidade e recebe a data indisponível | 06 | `422` + `metadata.unavailableDates` |
| Duas intenções concorrentes pela última unidade: só uma vence | 06 | Ver tarefa 48.0 |
| Bloqueio emergencial invalida retenções e produz `inventario-liberado` | 06 | Tarefa 19.0 |
| Prazo terminado devolve capacidade e produz os dois eventos | 07 | Tarefa 41.0 |
| `reserva.nao-concluida` libera antes do prazo | 07 | Tarefa 45.0 |
| Retenção já expirada não devolve capacidade duas vezes | 07 | Liberação idempotente |
| Retenção vigente migra para comprometida sem alterar o total | 08 | `revalidated: false` |
| Retenção expirada só compromete se houver saldo | 08 | `revalidated: true` ou `422` |

Recusa por saldo insuficiente, conforme o contrato:

```json
{
  "status": 422,
  "detail": "Não há saldo disponível em ao menos uma noite da estadia.",
  "code": "INSUFFICIENT_AVAILABILITY",
  "metadata": { "unavailableDates": ["2026-09-15"] }
}
```

> É **a mesma resposta** que a intenção perdedora recebe quando duas jornadas concorrem pela última unidade. Do ponto de vista de D03, perder a corrida e não ter saldo são o mesmo evento — e é assim que deve ser: não há informação útil a acrescentar, e distinguir os dois vazaria a concorrência de checkout.

**Convenções da stack (das skills consultadas):**

- Toda mutação de saldo passa pelo `InventoryLedger` (ADR-001).
- Outbox na mesma `SaveChangesAsync` do saldo e da auditoria (ADR-0002).
- Evento de liberação produzido **apenas** quando a transição ocorre — nunca em replay idempotente.
- Duração da retenção é parâmetro global, nunca do cliente (PRD e contrato).
- Nenhum dado do viajante em DTO ou log (`dotnet-production-readiness`).

## Critérios de Sucesso (Verificáveis)

- [ ] Testes passam: `dotnet test ../localizestay-backend/LocalizeStay.sln --filter "FullyQualifiedName~InventoryHoldCommandHandlerTests"`
- [ ] Build compila sem erros: `dotnet build ../localizestay-backend/LocalizeStay.sln --no-restore`
- [ ] Formatação válida: `dotnet format ../localizestay-backend/LocalizeStay.sln --verify-no-changes --no-restore`
- [ ] `expiresAt` devolvido é sempre `heldAt + 15 min`, mesmo se o cliente enviar outro valor.
- [ ] Recusa por saldo não altera nenhum contador e traz `metadata.unavailableDates`.
- [ ] Liberar retenção já expirada responde `204` e não devolve capacidade uma segunda vez.
- [ ] Liberar retenção `committed` responde `409 HOLD_ALREADY_COMMITTED`.
- [ ] Comprometer retenção vigente mantém `availableUnits` inalterado e devolve `revalidated: false`.
- [ ] Comprometer retenção expirada com saldo devolve `revalidated: true`; sem saldo, `422 COMMITMENT_WITHOUT_AVAILABILITY`.
- [ ] Os três eventos da Onda B aparecem na outbox na mesma transação de suas mutações.
