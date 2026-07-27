---
status: pending
parallelizable: false
blocked_by: ["19.0"]
---

<task_context>
<domain>inventory/application/inventory-blocks</domain>
<type>implementation</type>
<scope>core_feature</scope>
<complexity>medium</complexity>
<dependencies>database</dependencies>
<unblocks>"29.0"</unblocks>
<vertical_slice>Remover um bloqueio devolve a capacidade ao saldo vendável imediatamente e preserva o registro do bloqueio removido.</vertical_slice>
</task_context>

# Tarefa 20.0: Remover bloqueio e devolver a capacidade

## Relacionada às User Stories

- [US-03] Bloquear datas ao receber aviso de indisponibilidade (cobertura direta — a remoção fecha o ciclo quando o parceiro confirma o reparo)

## Visão Geral

`removeInventoryBlock` devolve a capacidade bloqueada ao saldo vendável e preserva o histórico com `status: removed`, autor e motivo. **Não há hard delete.**

Duas recusas específicas protegem a operação: bloqueio já removido e bloqueio de origem `curationSuspension`, que só termina com a retomada da aprovação por D06.

## Requisitos

- A capacidade volta a ser vendável **imediatamente** após o commit; a devolução passa pelo `InventoryLedger`.
- `inventario-liberado` é produzido na mesma transação, para que D01 saiba que voltou a haver saldo.
- Bloqueio já removido produz `409 BLOCK_ALREADY_REMOVED`.
- Bloqueio de origem `curationSuspension` produz `422 CURATION_BLOCK_NOT_REMOVABLE`.
- O registro é preservado com `removedAt`, `removalReason` e `removedBy`; nenhum campo original é apagado.
- Trilha de auditoria com autor, horário e motivo.
- O gate `activeAllotment` não muda com a remoção de bloqueio — bloqueio não é allotment.

## Arquivos Envolvidos

- **Criar:**
  - `../localizestay-backend/tests/LocalizeStay.UnitTests/Inventory/InventoryBlockRemovalHandlerTests.cs`
- **Modificar:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/InventoryBlocks/InventoryBlockCommands.cs` (acrescentar `RemoveInventoryBlockCommand` e handler)
- **Referência:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Domain/InventoryBlocks/InventoryBlock.cs` (criado em 7.0 — método `Remove` e as duas recusas)
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Domain/DailyInventories/InventoryLedger.cs`
  - `.tasks/prd-controlar-allotment-disponibilidade-bloqueios-e-retencoes/api-contract.md` (`PATCH /inventory-blocks/{blockId}`)
- **Skills para consultar durante implementação:**
  - `dotnet-architecture` — Command/Handler, outbox transacional
  - `dotnet-code-quality` — exceções específicas com filtro `when`
  - `dotnet-testing` — AAA com as duas recusas

## Subtarefas

- [ ] 20.1 Implementar `RemoveInventoryBlockCommandHandler`, devolvendo a capacidade pelo ledger e preservando o registro.
- [ ] 20.2 Gravar `inventario-liberado` na outbox e a trilha de auditoria na mesma transação.
- [ ] 20.3 Testar: devolução de capacidade, preservação do histórico, `BLOCK_ALREADY_REMOVED` e `CURATION_BLOCK_NOT_REMOVABLE`.

## Sequenciamento

- Bloqueado por: 19.0
- Desbloqueia: 29.0
- Paralelizável: Não; modifica `InventoryBlockCommands.cs`, criado pela tarefa 19.0. É a única colisão real de arquivo entre fatias de bloqueio, e por isso é sequenciada em vez de fundida — fundir estouraria o orçamento de subtarefas de 19.0.

## Rastreabilidade

- Esta tarefa cobre: o quinto critério de aceite de RF-02 ("bloqueio removido devolve a capacidade e preserva o histórico").
- Evidência esperada: `InventoryBlockRemovalHandlerTests` prova a devolução e as duas recusas; 29.0 expõe por HTTP; 35.0 prova a atomicidade.

## Detalhes de Implementação

Corpo da requisição, conforme o contrato:

```json
{ "status": "removed", "removalReason": "Parceiro confirmou que o reparo foi concluído." }
```

Recusas:

| HTTP | `code` | Quando |
|---:|---|---|
| 409 | `BLOCK_ALREADY_REMOVED` | Bloqueio já está em `status: removed` |
| 422 | `CURATION_BLOCK_NOT_REMOVABLE` | `origin: curationSuspension` — só D06 encerra, pelo evento `propriedade-aprovada` (tarefa 26.0) |

> A remoção de bloqueio de curadoria não é uma limitação técnica: é a regra. Deixar a Operação remover manualmente um bloqueio de suspensão permitiria vender uma propriedade que D06 tirou da vitrine — que é o cenário que RF-05 existe para impedir.

**Convenções da stack (das skills consultadas):**

- A recusa vive no domínio (`InventoryBlock.Remove`), não no handler; o handler apenas propaga (`dotnet-architecture`).
- Devolução de capacidade passa pelo `InventoryLedger`, nunca por `UPDATE` direto.
- Evento gravado na outbox na mesma `SaveChangesAsync` (ADR-0002).
- Testes AAA cobrindo os dois caminhos de recusa (`dotnet-testing`).

## Critérios de Sucesso (Verificáveis)

- [ ] Testes passam: `dotnet test ../localizestay-backend/LocalizeStay.sln --filter "FullyQualifiedName~InventoryBlockRemovalHandlerTests"`
- [ ] Build compila sem erros: `dotnet build ../localizestay-backend/LocalizeStay.sln --no-restore`
- [ ] Formatação válida: `dotnet format ../localizestay-backend/LocalizeStay.sln --verify-no-changes --no-restore`
- [ ] Após a remoção, `blockedUnits` das datas afetadas volta ao valor anterior e `availableUnits` aumenta correspondentemente.
- [ ] O registro do bloqueio permanece consultável com `status: removed`, `removedAt`, `removalReason` e `removedBy`.
- [ ] Remover duas vezes produz `BLOCK_ALREADY_REMOVED` na segunda tentativa.
- [ ] Remover bloqueio `curationSuspension` produz `CURATION_BLOCK_NOT_REMOVABLE`.
- [ ] `inventario-liberado` aparece na outbox na mesma transação.
