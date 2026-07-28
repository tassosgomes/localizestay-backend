---
status: pending
parallelizable: true
blocked_by: []
---

<task_context>
<domain>inventory/domain/inventory-blocks</domain>
<type>implementation</type>
<scope>core_feature</scope>
<complexity>medium</complexity>
<dependencies>database</dependencies>
<unblocks>"11.0, 12.0, 19.0, 20.0, 25.0"</unblocks>
<vertical_slice>Um bloqueio conhece seu tipo, origem, motivo obrigatório e período, e preserva o histórico quando removido.</vertical_slice>
</task_context>

# Tarefa 7.0: Modelar `InventoryBlock` com tipo, origem e remoção com histórico

## Relacionada às User Stories

- [US-03] Bloquear datas imediatamente ao receber um aviso de indisponibilidade (direta)

## Visão Geral

`InventoryBlock` representa a redução operacional da capacidade vendável. A distinção entre `planned` e `emergency` é o coração de RN-15 e RN-16: o planejado só alcança saldo livre; o emergencial é sempre aceito, invalida retenções e **nunca** cancela ou altera reserva confirmada.

Bloqueio removido é preservado com `status = 'removed'`, autor e motivo. **Não há hard delete.**

## Requisitos

- `Type` com `planned` e `emergency`; `Origin` com `partnerRequest`, `internalOperation` e `curationSuspension`.
- `Reason` obrigatório em todos os casos; `ReasonNote` obrigatório quando `Reason` é `other`.
- `Units` inteiro; `BlocksEntireAccommodation = true` quando o bloqueio zera a capacidade.
- Período `StartDate`/`EndDate` inclusivo em ambas as pontas.
- `Status` com `active` e `removed`; remoção grava `RemovedAt`, `RemovalReason` e `RemovedBy`, preservando o registro.
- Bloqueio de origem `curationSuspension` **não é removível manualmente** — lança erro com `code = CURATION_BLOCK_NOT_REMOVABLE`. Só a retomada da aprovação por D06 o encerra.
- Bloqueio já removido recusa nova remoção com `code = BLOCK_ALREADY_REMOVED`.
- `AffectedReservationCount` e `InvalidatedHoldCount` são sempre `0` em `planned`.
- `SalesStoppedAt` é o carimbo da métrica de latência de um minuto; só é preenchido em bloqueios que efetivamente cortam venda.
- A entidade **não** decide se há saldo livre suficiente — isso pertence ao `InventoryLedger` (11.0).

## Arquivos Envolvidos

- **Criar:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Domain/InventoryBlocks/InventoryBlock.cs`
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Domain/InventoryBlocks/InventoryBlockValues.cs`
  - `../localizestay-backend/tests/LocalizeStay.UnitTests/Inventory/InventoryBlockTests.cs`
- **Referência:**
  - `.tasks/prd-controlar-allotment-disponibilidade-bloqueios-e-retencoes/api-contract.md` (schema `InventoryBlock` e tabela "Comportamento por tipo")
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Domain/CommercialOffers/CommercialOffer.cs` (padrão de agregado)
- **Skills para consultar durante implementação:**
  - `dotnet-architecture` — aggregate root, enums de domínio, invariantes
  - `dotnet-code-quality` — sem flag params (não usar `bool isEmergency`; usar o enum `BlockType`)
  - `dotnet-testing` — `[Theory]` para a matriz tipo × alcance

## Subtarefas

- [ ] 7.1 Declarar `BlockType`, `BlockOrigin`, `BlockReason` e `BlockStatus` em `InventoryBlockValues.cs`.
- [ ] 7.2 Modelar o agregado com factory `Create`, exigindo `reasonNote` quando `reason = other` e recusando período invertido.
- [ ] 7.3 Implementar `Remove`, com as duas recusas (`BLOCK_ALREADY_REMOVED`, `CURATION_BLOCK_NOT_REMOVABLE`) e preservação do histórico; e `MarkSalesStopped` para o carimbo de latência.
- [ ] 7.4 Testar a matriz completa de tipo × origem × remoção, incluindo os dois códigos de recusa e a garantia de que `planned` mantém contadores de impacto em zero.

## Sequenciamento

- Bloqueado por: Nenhum
- Desbloqueia: 11.0, 12.0, 19.0, 20.0, 25.0
- Paralelizável: Sim; domínio puro, arquivos exclusivos desta tarefa.

## Rastreabilidade

- Esta tarefa cobre: RF-02 no domínio, RN-03, RN-15 e RN-16.
- Evidência esperada: `InventoryBlockTests` prova que bloqueio de curadoria não é removível e que remoção preserva o registro.

## Detalhes de Implementação

Comportamento por tipo, conforme o contrato:

| Tipo | Alcança saldo livre | Alcança retenção vigente | Alcança reserva confirmada | Confirmação explícita |
|---|:--:|:--:|:--:|:--:|
| `planned` | sim | **não** | **não** | não exigida |
| `emergency` | sim | invalida | não cancela; produz `bloqueio-afeta-reserva` | `confirmEmergencyImpact: true` |

> A exigência de `confirmEmergencyImpact` é validada no handler (19.0), não aqui — é uma regra do contrato HTTP, não do domínio.

Erros que a entidade produz:

| Condição | `code` | HTTP |
|---|---|---:|
| `reason: other` sem `reasonNote` | `REASON_NOTE_REQUIRED` | 422 |
| Remover bloqueio já removido | `BLOCK_ALREADY_REMOVED` | 409 |
| Remover bloqueio de curadoria | `CURATION_BLOCK_NOT_REMOVABLE` | 422 |
| Período invertido | `INVALID_DATE_RANGE` | 422 |

**Convenções da stack (das skills consultadas):**

- Enums de domínio em arquivo próprio, seguindo `CommercialOfferValues.cs` (`dotnet-architecture`).
- Nunca usar flag param booleano para chavear comportamento; o tipo do bloqueio é enum (`dotnet-code-quality`).
- Soft delete com preservação de autor e motivo, como `CommercialPolicy` da F02.
- Testes AAA parametrizados cobrindo a matriz completa (`dotnet-testing`).

## Critérios de Sucesso (Verificáveis)

- [ ] Testes passam: `dotnet test ../localizestay-backend/LocalizeStay.sln --filter "FullyQualifiedName~InventoryBlockTests"`
- [ ] Build compila sem erros: `dotnet build ../localizestay-backend/LocalizeStay.sln --no-restore`
- [ ] Formatação válida: `dotnet format ../localizestay-backend/LocalizeStay.sln --verify-no-changes --no-restore`
- [ ] `reason: other` sem `reasonNote` lança erro com `code = REASON_NOTE_REQUIRED`.
- [ ] Remover bloqueio de origem `curationSuspension` lança `CURATION_BLOCK_NOT_REMOVABLE`.
- [ ] Remover bloqueio já removido lança `BLOCK_ALREADY_REMOVED`.
- [ ] Bloqueio removido preserva `Id`, período, motivo original e ganha `RemovedAt`, `RemovalReason`, `RemovedBy`.
- [ ] Bloqueio `planned` mantém `AffectedReservationCount = 0` e `InvalidatedHoldCount = 0`.
