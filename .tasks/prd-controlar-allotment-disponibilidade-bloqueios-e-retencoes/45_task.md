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
<vertical_slice>A intenção de reserva de D03 cria a retenção, e o encerramento sem confirmação a libera — ambos idempotentes.</vertical_slice>
</task_context>

# Tarefa 45.0: Consumir intenção iniciada e reserva não concluída

## Relacionada às User Stories

- [US-04] Acomodação separada durante o checkout (cobertura direta)

## Visão Geral

Dois consumidores de evento que convergem para o mesmo caminho de aplicação dos endpoints HTTP correspondentes: `reserva.intencao-iniciada` equivale a `POST /inventory-holds`, e `reserva.nao-concluida` equivale a `DELETE /inventory-holds/{holdId}`.

A liberação é o caso mais delicado: uma retenção que **já expirou** pode receber `reserva.nao-concluida`. Nesse caso, nenhuma capacidade é devolvida uma segunda vez.

## Requisitos

- Consumo **idempotente por `eventId`**; reprocessar não cria segunda retenção nem devolve capacidade duas vezes.
- `ReservationIntentStartedHandler` reutiliza o mesmo `CreateInventoryHoldCommand` do endpoint — nenhum caminho paralelo de escrita.
- Deduplicação adicional por `reservationIntentId`: a mesma intenção não gera duas retenções, mesmo com `eventId` diferente.
- Recusa por saldo insuficiente **não** lança exceção que trave o barramento: registra o resultado e informa D03 pelo canal previsto.
- `ReservationNotCompletedHandler` reutiliza `ReleaseInventoryHoldCommand`, com a idempotência já garantida em 42.0.
- Retenção já expirada recebendo `reserva.nao-concluida` **não devolve capacidade** e não produz `inventario-liberado` uma segunda vez.
- Retenção `committed` recebendo `reserva.nao-concluida` é registrada como anomalia e não altera nada — liberar inventário de reserva confirmada pertence a D03.
- Log estruturado com `eventId`, `reservationIntentId`, `holdId` e `result`. **Nenhum dado do viajante.**

## Arquivos Envolvidos

- **Criar:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/Reservations/ReservationIntentStartedHandler.cs`
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/Reservations/ReservationNotCompletedHandler.cs`
  - `../localizestay-backend/tests/LocalizeStay.UnitTests/Inventory/ReservationHoldHandlerTests.cs`
- **Referência:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/InventoryHolds/InventoryHoldCommands.cs` (criado em 42.0)
  - `../localizestay-backend/src/Modules/Booking/LocalizeStay.Modules.Booking.Contracts/BookingIntegrationEvents.cs` (criado em 44.0)
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/CommercialOffers/CurationOfferReturnedHandler.cs` (padrão de consumidor idempotente)
- **Skills para consultar durante implementação:**
  - `dotnet-architecture` — `IIntegrationEventHandler<T>`, reutilização do Command do endpoint
  - `dotnet-observability` — log com `eventId` e resultado
  - `dotnet-testing` — reprocessamento e reordenação de eventos

## Subtarefas

- [ ] 45.1 Implementar `ReservationIntentStartedHandler` reutilizando `CreateInventoryHoldCommand`, com deduplicação por `eventId` e por `reservationIntentId`.
- [ ] 45.2 Tratar a recusa por saldo sem travar o barramento, registrando o resultado com log estruturado.
- [ ] 45.3 Implementar `ReservationNotCompletedHandler` reutilizando `ReleaseInventoryHoldCommand`, idempotente.
- [ ] 45.4 Testar: reprocessamento, intenção duplicada, retenção já expirada recebendo `nao-concluida` e retenção comprometida recebendo `nao-concluida`.

## Sequenciamento

- Bloqueado por: 42.0, 44.0
- Desbloqueia: 50.0
- Paralelizável: Sim; arquivos exclusivos, disjuntos de 46.0.

## Rastreabilidade

- Esta tarefa cobre: RF-06 e RF-07 pela via de evento; o terceiro critério de aceite de RF-07 ("retenção já expirada recebendo `reserva.nao-concluida` não devolve capacidade duas vezes").
- Evidência esperada: `ReservationHoldHandlerTests` prova as quatro situações da matriz.

## Detalhes de Implementação

Matriz de estado × evento `reserva.nao-concluida`:

| Estado da retenção | Efeito | Evento produzido |
|---|---|---|
| `held` e vigente | Libera; capacidade devolvida | `inventario-liberado` |
| `expired` | **Nada** — já devolvida pela varredura | **nenhum** |
| `released` | Nada | nenhum |
| `invalidated` | Nada — já devolvida pelo bloqueio | nenhum |
| `committed` | Nada; registra anomalia | nenhum |

> A linha `expired` é o critério de aceite literal de RF-07. Se o handler devolvesse capacidade que a varredura já devolveu, o saldo cresceria além do allotment — e o sistema passaria a vender unidades inexistentes. É a mesma classe de falha que a tarefa 36.0 detecta por reconciliação.

Por que reutilizar os Commands em vez de escrever um caminho próprio: **duplicar o caminho de escrita duplica a chance de esquecer a guarda de retenção vencida, a idempotência ou o evento.** O consumidor é uma casca fina sobre o mesmo Command do endpoint.

Recusa por saldo não é falha de processamento: é resposta legítima. Lançar exceção faria o barramento reentregar o evento indefinidamente para uma condição que não vai mudar sozinha.

**Convenções da stack (das skills consultadas):**

- Consumidores idempotentes por `eventId`, seguindo `CurationOfferReturnedHandler` (`dotnet-architecture`).
- Nenhum caminho de escrita paralelo ao dos endpoints.
- Log estruturado com `eventId`, `reservationIntentId`, `holdId`, `result` (`dotnet-observability`).
- Nenhum dado do viajante em log algum (`dotnet-production-readiness`).

## Critérios de Sucesso (Verificáveis)

- [ ] Testes passam: `dotnet test ../localizestay-backend/LocalizeStay.sln --filter "FullyQualifiedName~ReservationHoldHandlerTests"`
- [ ] Build compila sem erros: `dotnet build ../localizestay-backend/LocalizeStay.sln --no-restore`
- [ ] Formatação válida: `dotnet format ../localizestay-backend/LocalizeStay.sln --verify-no-changes --no-restore`
- [ ] Reprocessar o mesmo `eventId` de intenção não cria segunda retenção.
- [ ] Duas intenções com o mesmo `reservationIntentId` produzem uma única retenção.
- [ ] Recusa por saldo não lança exceção que force reentrega do evento.
- [ ] Retenção já expirada recebendo `nao-concluida` **não** devolve capacidade e **não** produz `inventario-liberado`.
- [ ] Retenção `committed` recebendo `nao-concluida` não altera nada.
- [ ] Nenhum log contém nome, documento, e-mail ou telefone.
