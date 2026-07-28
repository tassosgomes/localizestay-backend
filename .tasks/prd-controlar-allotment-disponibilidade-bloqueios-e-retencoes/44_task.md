---
status: pending
parallelizable: true
blocked_by: []
---

<task_context>
<domain>inventory/integration/booking-contracts</domain>
<type>integration</type>
<scope>configuration</scope>
<complexity>low</complexity>
<dependencies>external_apis</dependencies>
<unblocks>"45.0, 46.0"</unblocks>
<vertical_slice>Os três eventos de reserva existem como schemas versionados que o módulo Inventory pode consumir.</vertical_slice>
</task_context>

# Tarefa 44.0: Declarar os contratos de eventos de reserva

## Relacionada às User Stories

- [US-04] Acomodação separada durante o checkout (suporte — os eventos de D03 são o gatilho real do ciclo de retenção)

## Visão Geral

RF-06, RF-07 e RF-08 são disparados por três eventos de D03: `reserva.intencao-iniciada`, `reserva.nao-concluida` e `reserva.confirmada`. Cada um converge para o mesmo caminho de aplicação do endpoint HTTP correspondente.

D03 ainda não existe como módulo implementado e **não há publicador**. Os contratos existem para que os consumidores sejam escritos contra schemas versionados.

## Requisitos

- Declarar `ReservationIntentStartedV1`, `ReservationConfirmedV1` e `ReservationNotCompletedV1` em `LocalizeStay.Modules.Booking.Contracts` — **nunca** no módulo Inventory.
- Payload **mínimo**: apenas o que a F03 consome. Nenhum dado do viajante.
- Os nomes `reservationIntentId` e `reservationId` **devem ser travados com D03 antes da Onda B** — são a única ratificação que vale bloquear, e ambos já constam do contrato HTTP público da F03, portanto já estão comprometidos externamente.
- Cada evento herda de `IntegrationEvent` e carrega `EventId` para consumo idempotente.
- O projeto `LocalizeStay.Modules.Inventory.csproj` passa a referenciar `Booking.Contracts`.
- Testes de arquitetura continuam aprovando.

## Arquivos Envolvidos

- **Criar:**
  - `../localizestay-backend/src/Modules/Booking/LocalizeStay.Modules.Booking.Contracts/BookingIntegrationEvents.cs`
- **Modificar:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/LocalizeStay.Modules.Inventory.csproj` (referência a `Booking.Contracts`)
  - `../localizestay-backend/tests/LocalizeStay.ArchitectureTests/ContractsTests.cs` (cobrir os três novos contratos)
- **Referência:**
  - `../localizestay-backend/src/Modules/Curation/LocalizeStay.Modules.Curation.Contracts/CurationSellabilityEvents.cs` (criado em 4.0 — mesmo padrão)
  - `../localizestay-backend/src/BuildingBlocks/LocalizeStay.SharedKernel/Events/IntegrationEvent.cs`
  - `.tasks/prd-controlar-allotment-disponibilidade-bloqueios-e-retencoes/api-contract.md` (schemas `InventoryHold` e `commitment`)
- **Skills para consultar durante implementação:**
  - `dotnet-architecture` — contratos em assembly próprio, fronteira de módulo
  - `dotnet-code-quality` — records imutáveis, nomes em inglês

## Subtarefas

- [ ] 44.1 Declarar os três records `V1` com payload mínimo, herdando de `IntegrationEvent`, sem nenhum dado do viajante.
- [ ] 44.2 Referenciar `Booking.Contracts` no csproj do Inventory.
- [ ] 44.3 Estender `ContractsTests` para provar que os três tipos são públicos, imutáveis e versionados com sufixo `V1`.

## Sequenciamento

- Bloqueado por: Nenhum
- Desbloqueia: 45.0, 46.0
- Paralelizável: Sim; cria um arquivo novo em outro módulo e altera um csproj. Pode começar a qualquer momento.

## Rastreabilidade

- Esta tarefa cobre: os três eventos consumidos da Onda B declarados no contrato (`x-domain-events.consumes`).
- Evidência esperada: `ContractsTests` verde e os consumidores de 45.0/46.0 compilando contra os tipos.

## Detalhes de Implementação

Payload mínimo sugerido:

```csharp
public sealed record ReservationIntentStartedV1(
    Guid ReservationIntentId, Guid AccommodationId,
    DateOnly CheckIn, DateOnly CheckOut, int Units) : IntegrationEvent;

public sealed record ReservationConfirmedV1(
    Guid ReservationIntentId, Guid ReservationId) : IntegrationEvent;

public sealed record ReservationNotCompletedV1(
    Guid ReservationIntentId, string Reason) : IntegrationEvent;
```

Mapa evento → caminho de aplicação:

| Evento | Equivale a | Task |
|---|---|---|
| `reserva.intencao-iniciada` | `POST /inventory-holds` | 45.0 |
| `reserva.nao-concluida` | `DELETE /inventory-holds/{id}` (idempotente) | 45.0 |
| `reserva.confirmada` | `POST /inventory-holds/{id}/commitment` | 46.0 |

> **Payload mínimo maximiza a chance de que D03 apenas acrescente.** Acrescentar campo é compatível; renomear ou remover não é. A exceção que vale travar antes da Onda B são os dois nomes `reservationIntentId` e `reservationId` — e eles já estão comprometidos no contrato HTTP público da F03, o que reduz a ratificação a uma confirmação formal.

**Nenhum dado do viajante entra nesses eventos.** Nome, documento, e-mail e telefone pertencem a D03 e não atravessam a fronteira.

**Convenções da stack (das skills consultadas):**

- Contratos de integração em `*.Contracts`, públicos e imutáveis (`dotnet-architecture`).
- Records posicionais com sufixo de versão explícito (`dotnet-code-quality`).
- Nenhuma referência ao assembly de implementação de outro módulo.

## Critérios de Sucesso (Verificáveis)

- [ ] Testes passam: `dotnet test ../localizestay-backend/tests/LocalizeStay.ArchitectureTests`
- [ ] Build compila sem erros: `dotnet build ../localizestay-backend/LocalizeStay.sln --no-restore`
- [ ] Formatação válida: `dotnet format ../localizestay-backend/LocalizeStay.sln --verify-no-changes --no-restore`
- [ ] Os três tipos são públicos, `sealed record` e herdam de `IntegrationEvent`.
- [ ] Nenhum dos três carrega dado pessoal do viajante.
- [ ] Os nomes de campo `ReservationIntentId` e `ReservationId` correspondem exatamente aos do contrato HTTP da F03.
- [ ] O módulo Inventory referencia apenas `Booking.Contracts`, nunca `LocalizeStay.Modules.Booking`.
