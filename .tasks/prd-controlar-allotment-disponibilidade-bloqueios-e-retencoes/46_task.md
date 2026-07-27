---
status: pending
parallelizable: true
blocked_by: ["42.0", "44.0"]
---

<task_context>
<domain>inventory/application/reservations</domain>
<type>integration</type>
<scope>core_feature</scope>
<complexity>medium</complexity>
<dependencies>database</dependencies>
<unblocks>"50.0"</unblocks>
<vertical_slice>A confirmação de reserva de D03 converte a retenção em capacidade comprometida, ou comunica a divergência quando não há saldo.</vertical_slice>
</task_context>

# Tarefa 46.0: Consumir reserva confirmada

## Relacionada às User Stories

- [US-04] Acomodação separada durante o checkout (cobertura direta — o comprometimento é o desfecho bem-sucedido)

## Visão Geral

`reserva.confirmada` equivale a `POST /inventory-holds/{holdId}/commitment`. Com a retenção vigente, a capacidade migra de retida para comprometida **sem alterar o total disponível**.

O caso interessante é a retenção **já expirada**: o saldo é reavaliado. Havendo disponibilidade, o comprometimento acontece com `revalidated: true`. Não havendo, a divergência é comunicada a D03 e D07 **sem comprometer capacidade inexistente** — que é a definição de venda sem lastro, a métrica que o PRD exige manter em zero.

## Requisitos

- Consumo **idempotente por `eventId`** e por `reservationId`: a mesma confirmação não gera dois comprometimentos.
- `ReservationConfirmedHandler` reutiliza `CommitInventoryHoldCommand` — nenhum caminho paralelo de escrita.
- Retenção vigente: migração sem alterar o total disponível; produz `inventario-comprometido`.
- Retenção expirada ou invalidada com saldo: revalida, compromete e marca `revalidated: true`; instrumenta `inventory.hold.commit_revalidated`.
- Retenção expirada sem saldo: **não compromete nada**; registra a divergência com log em nível `Error` e a comunica a D03 e D07 pelo canal previsto.
- Divergência não trava o barramento — não é falha de processamento, é resultado de negócio.
- Log estruturado com `eventId`, `reservationIntentId`, `reservationId`, `holdId`, `revalidated`, `result`. Nenhum dado do viajante.

## Arquivos Envolvidos

- **Criar:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/Reservations/ReservationConfirmedHandler.cs`
  - `../localizestay-backend/tests/LocalizeStay.UnitTests/Inventory/ReservationConfirmedHandlerTests.cs`
- **Referência:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/InventoryHolds/InventoryHoldCommands.cs` (criado em 42.0)
  - `../localizestay-backend/src/Modules/Booking/LocalizeStay.Modules.Booking.Contracts/BookingIntegrationEvents.cs` (criado em 44.0)
  - `.tasks/prd-controlar-allotment-disponibilidade-bloqueios-e-retencoes/api-contract.md` (`POST /inventory-holds/{holdId}/commitment`)
- **Skills para consultar durante implementação:**
  - `dotnet-architecture` — `IIntegrationEventHandler<T>`, reutilização do Command
  - `dotnet-observability` — contador de revalidação, log de divergência
  - `dotnet-testing` — matriz de estado × saldo

## Subtarefas

- [ ] 46.1 Implementar `ReservationConfirmedHandler` reutilizando `CommitInventoryHoldCommand`, com deduplicação por `eventId` e por `reservationId`.
- [ ] 46.2 Tratar o caminho de retenção expirada com saldo, marcando `revalidated: true` e instrumentando `inventory.hold.commit_revalidated`.
- [ ] 46.3 Tratar a divergência sem saldo: não comprometer nada, registrar em nível `Error` e comunicar a D03 e D07 sem travar o barramento.
- [ ] 46.4 Testar: retenção vigente, expirada com saldo, expirada sem saldo, e reprocessamento do mesmo evento.

## Sequenciamento

- Bloqueado por: 42.0, 44.0
- Desbloqueia: 50.0
- Paralelizável: Sim; arquivo exclusivo, disjunto de 45.0.

## Rastreabilidade

- Esta tarefa cobre: RF-08 pela via de evento, com os dois critérios de aceite; RN-05 e RN-06.
- Evidência esperada: `ReservationConfirmedHandlerTests` prova a migração sem alterar o total e a recusa sem lastro.

## Detalhes de Implementação

Critérios de aceite de RF-08 mapeados:

| Critério | Verificação |
|---|---|
| Retenção vigente: capacidade migra de retida para comprometida sem alterar o total disponível, e `inventario-comprometido` é produzido | `availableUnits` inalterado antes e depois |
| Retenção já expirada: confirmação só é aceita se ainda houver saldo; caso contrário a divergência é comunicada a D03 e D07 sem comprometer capacidade inexistente | `revalidated: true` ou recusa registrada |

Migração sem alterar o total:

```
antes:  allotted=3  committed=0  held=1  blocked=0  available=2
depois: allotted=3  committed=1  held=0  blocked=0  available=2   ← inalterado
```

> **A recusa sem saldo é a última linha de defesa contra venda sem lastro.** Comprometer capacidade inexistente para "não perder a reserva" produziria exatamente a reserva não reconhecida pelo parceiro que a F03 existe para impedir — e a métrica de venda sem lastro do PRD, com meta zero, deixaria de ser confiável. A divergência é um problema de D05, não uma exceção a contornar aqui.

`inventory.hold.commit_revalidated` mede quantos comprometimentos exigiram revalidação por retenção expirada. É o sinal de que a duração de quinze minutos pode estar mal calibrada — insumo direto da recalibração prevista para a Phase 2.

**Convenções da stack (das skills consultadas):**

- Consumidor idempotente por `eventId`, seguindo o padrão do módulo (`dotnet-architecture`).
- Nenhum caminho de escrita paralelo ao do endpoint.
- Divergência de negócio é `Error` no log, não exceção que force reentrega (`dotnet-production-readiness`).
- Nenhum dado do viajante em log algum.

## Critérios de Sucesso (Verificáveis)

- [ ] Testes passam: `dotnet test ../localizestay-backend/LocalizeStay.sln --filter "FullyQualifiedName~ReservationConfirmedHandlerTests"`
- [ ] Build compila sem erros: `dotnet build ../localizestay-backend/LocalizeStay.sln --no-restore`
- [ ] Formatação válida: `dotnet format ../localizestay-backend/LocalizeStay.sln --verify-no-changes --no-restore`
- [ ] Comprometer retenção vigente mantém `availableUnits` inalterado e produz `inventario-comprometido`.
- [ ] Retenção expirada com saldo é comprometida com `revalidated: true`.
- [ ] Retenção expirada sem saldo **não** compromete nada e registra a divergência em nível `Error`.
- [ ] A divergência não força reentrega do evento pelo barramento.
- [ ] Reprocessar o mesmo `eventId` não cria segundo comprometimento.
- [ ] `inventory.hold.commit_revalidated` é incrementada apenas no caminho de revalidação.
