---
status: pending
parallelizable: true
blocked_by: ["1.0", "10.0", "11.0", "14.0", "15.0", "16.0"]
---

<task_context>
<domain>inventory/application/inventory-blocks</domain>
<type>implementation</type>
<scope>core_feature</scope>
<complexity>high</complexity>
<dependencies>database</dependencies>
<unblocks>"20.0, 29.0, 32.0"</unblocks>
<vertical_slice>Aplicar um bloqueio corta a venda das datas alvo — planejado só sobre saldo livre, emergencial sempre aceito com invalidação de retenções e evento para D05.</vertical_slice>
</task_context>

# Tarefa 19.0: Aplicar bloqueio planejado e emergencial

> ⚠️ **`complexity: high` — exige revisão humana do plano antes de implementar.** O bloqueio emergencial precisa cortar vendas, invalidar retenções e gravar dois eventos na mesma transação, com o carimbo `salesStoppedAt` que sustenta a métrica de um minuto do PRD.

## Relacionada às User Stories

- [US-03] Bloquear datas imediatamente ao receber um aviso de indisponibilidade (cobertura direta)

## Visão Geral

`createInventoryBlock` é a operação que cumpre a promessa mais dura do PRD: interromper novas vendas em até **um minuto** após a confirmação da ação. O prazo mede o sistema, não a disponibilidade humana — por isso `salesStoppedAt` é carimbado no commit e vira a base da métrica.

A distinção entre os dois tipos é a regra central de RN-15 e RN-16.

## Requisitos

- Header `Idempotency-Key` obrigatório, com escopo `inventoryBlockCreation`; mesma chave com corpo diferente produz `409 IDEMPOTENCY_KEY_REUSED`; réplica **não** reaplica o efeito no saldo.
- `planned`: consome apenas saldo livre; acima disso produz `422 INSUFFICIENT_FREE_BALANCE` com `metadata.freeBalanceByDate`. Nunca alcança retenção vigente nem reserva confirmada.
- `emergency`: **sempre aceito**; exige `confirmEmergencyImpact: true`, sob pena de `422 EMERGENCY_BLOCK_CONFIRMATION_REQUIRED`.
- `emergency` invalida retenções vigentes das datas alvo, gravando `invalidatedByBlockId` e produzindo `inventario-liberado` para que D03 encerre o checkout.
- `emergency` que alcança datas com reserva confirmada produz `bloqueio-afeta-reserva` para D05. **Nenhuma reserva é cancelada ou alterada.**
- `inventario-bloqueado` é produzido em ambos os tipos, via outbox transacional, na mesma transação do saldo e da auditoria.
- `salesStoppedAt` é gravado no momento em que o corte passa a valer.
- Trilha de auditoria com autor, horário e motivo; vínculo opcional com `requestId`.

## Arquivos Envolvidos

- **Criar:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/InventoryBlocks/InventoryBlockCommands.cs`
  - `../localizestay-backend/tests/LocalizeStay.UnitTests/Inventory/InventoryBlockCommandHandlerTests.cs`
- **Referência:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Domain/DailyInventories/InventoryLedger.cs` (criado em 11.0)
  - `../localizestay-backend/src/BuildingBlocks/LocalizeStay.SharedKernel/Outbox/OutboxMessageFactory.cs`
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Domain/InventoryIdempotencyKey.cs` (criado em 10.0)
  - `.tasks/prd-controlar-allotment-disponibilidade-bloqueios-e-retencoes/api-contract.md` (exemplo de `POST /inventory-blocks` e tabela de comportamento por tipo)
- **Skills para consultar durante implementação:**
  - `dotnet-architecture` — CQRS, outbox na mesma transação, exceções de domínio
  - `dotnet-observability` — histograma `inventory.block.emergency_latency`, span `inventory.block.apply`
  - `dotnet-code-quality` — sem flag param; o tipo do bloqueio é enum
  - `dotnet-testing` — matriz tipo × saldo × impacto

## Subtarefas

- [ ] 19.1 Implementar a guarda de idempotência com escopo `inventoryBlockCreation`, garantindo que a réplica não reaplique efeito no saldo.
- [ ] 19.2 Implementar `CreateInventoryBlockCommandHandler` para `planned`, com a recusa `INSUFFICIENT_FREE_BALANCE` e `metadata.freeBalanceByDate`.
- [ ] 19.3 Implementar o caminho `emergency`: exigir `confirmEmergencyImpact`, invalidar retenções, carimbar `salesStoppedAt` e gravar `inventario-bloqueado`, `inventario-liberado` e `bloqueio-afeta-reserva` na outbox, na mesma transação.
- [ ] 19.4 Testar a matriz completa: planejado dentro e acima do saldo livre, emergencial com e sem confirmação, com e sem reserva alcançada, e replay idempotente.

## Sequenciamento

- Bloqueado por: 1.0, 10.0, 11.0, 14.0, 15.0, 16.0
- Desbloqueia: 20.0, 29.0, 32.0
- Paralelizável: Sim; cria arquivos exclusivos. A remoção de bloqueio (20.0) modifica o mesmo arquivo e por isso é sequenciada depois.

## Rastreabilidade

- Esta tarefa cobre: RF-02 (aplicação), RN-03, RN-15 e RN-16, e a métrica de latência do bloqueio emergencial.
- Evidência esperada: `InventoryBlockCommandHandlerTests` prova a matriz; 35.0 prova a atomicidade saldo + auditoria + outbox; 32.0 instrumenta a latência.

## Detalhes de Implementação

Comportamento por tipo, conforme o contrato:

| Tipo | Alcança saldo livre | Alcança retenção vigente | Alcança reserva confirmada | Confirmação explícita |
|---|:--:|:--:|:--:|:--:|
| `planned` | sim | **não** | **não** | não exigida |
| `emergency` | sim | invalida | não cancela; produz `bloqueio-afeta-reserva` | `confirmEmergencyImpact: true` |

Erros da operação:

| HTTP | `code` | Quando |
|---:|---|---|
| 422 | `INSUFFICIENT_FREE_BALANCE` | Planejado maior que o saldo livre; `metadata.freeBalanceByDate` |
| 422 | `EMERGENCY_BLOCK_CONFIRMATION_REQUIRED` | `emergency` sem `confirmEmergencyImpact: true` |
| 422 | `REASON_NOTE_REQUIRED` | `reason: other` sem `reasonNote` |
| 409 | `IDEMPOTENCY_KEY_REUSED` | Mesma chave com corpo diferente |

Eventos produzidos, todos na mesma transação da mutação de saldo:

| Evento | Quando | Consumidores |
|---|---|---|
| `oferta-inventario.inventario-bloqueado` | Sempre | D01, D07, D09 |
| `oferta-inventario.inventario-liberado` | Quando retenções são invalidadas | D01, D03, D09 |
| `oferta-inventario.bloqueio-afeta-reserva` | Quando alcança reserva confirmada | D05 |

> `salesStoppedAt` é a base da métrica de latência do PRD: do commit ao corte de novas vendas, **no máximo um minuto**. Qualquer amostra acima de sessenta segundos dispara alerta (tarefa 32.0).

**Convenções da stack (das skills consultadas):**

- Outbox transacional in-process; mensagem gravada na mesma `SaveChangesAsync` do saldo (ADR-0002).
- Auditoria de negócio distinta dos logs de diagnóstico (`dotnet-observability`).
- Nenhum handler altera `daily_inventory` diretamente; toda mutação passa pelo `InventoryLedger`.
- Logs com `blockId`, `accommodationId`, `type`, `origin`, `result` — nunca dado do viajante (`dotnet-production-readiness`).
- Testes parametrizados cobrindo a matriz completa (`dotnet-testing`).

## Critérios de Sucesso (Verificáveis)

- [ ] Testes passam: `dotnet test ../localizestay-backend/LocalizeStay.sln --filter "FullyQualifiedName~InventoryBlockCommandHandlerTests"`
- [ ] Build compila sem erros: `dotnet build ../localizestay-backend/LocalizeStay.sln --no-restore`
- [ ] Formatação válida: `dotnet format ../localizestay-backend/LocalizeStay.sln --verify-no-changes --no-restore`
- [ ] Bloqueio planejado acima do saldo livre produz `INSUFFICIENT_FREE_BALANCE` com `freeBalanceByDate` por data.
- [ ] Bloqueio planejado **não** reduz `heldUnits` nem `committedUnits` em nenhuma data.
- [ ] Bloqueio emergencial sem `confirmEmergencyImpact` produz `EMERGENCY_BLOCK_CONFIRMATION_REQUIRED`.
- [ ] Bloqueio emergencial é aceito mesmo sem saldo livre e carimba `salesStoppedAt`.
- [ ] Bloqueio emergencial sobre data com reserva confirmada produz `bloqueio-afeta-reserva` e **não** altera a reserva.
- [ ] Replay com a mesma `Idempotency-Key` e mesmo corpo devolve o bloqueio original sem alterar o saldo uma segunda vez.
