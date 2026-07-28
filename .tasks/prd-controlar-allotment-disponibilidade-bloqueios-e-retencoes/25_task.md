---
status: pending
parallelizable: true
blocked_by: ["4.0", "7.0", "11.0", "17.0"]
---

<task_context>
<domain>inventory/application/sellability</domain>
<type>integration</type>
<scope>core_feature</scope>
<complexity>medium</complexity>
<dependencies>database</dependencies>
<unblocks>"26.0, 35.0"</unblocks>
<vertical_slice>Ao receber a suspensão de curadoria, todas as acomodações da propriedade deixam de ser vendáveis, com efeito equivalente ao bloqueio emergencial e sem cancelar reserva alguma.</vertical_slice>
</task_context>

# Tarefa 25.0: Interromper a venda da propriedade por suspensão de curadoria

## Relacionada às User Stories

- [US-03] Não vender o que não pode ser honrado (cobertura direta — aqui a decisão vem de D06, não do parceiro)

## Visão Geral

RF-05 exige cessar a venda de **toda a propriedade** quando D06 comunica suspensão. O efeito é equivalente a um bloqueio emergencial de origem `curationSuspension`: novas vendas cessam, retenções vigentes são invalidadas e **nenhuma reserva confirmada é cancelada ou alterada**.

Se o período alcançar reservas confirmadas, `bloqueio-afeta-reserva` é produzido para D05 tratar como caso crítico.

## Requisitos

- Consumo **idempotente por `eventId`**: reprocessar o mesmo evento não duplica bloqueio nem evento.
- A suspensão é processada **por acomodação, cada uma em sua própria transação** — nunca a propriedade inteira em uma transação única. Propriedade grande tocaria muitas linhas de `daily_inventory`.
- Cada acomodação recebe um `InventoryBlock` de `type: emergency` e `origin: curationSuspension`, sem exigir `confirmEmergencyImpact` — a confirmação explícita é regra do painel humano, não do consumidor de evento.
- O gate `propertyApproved` é marcado `blocked` com origem `event`.
- `inventario-bloqueado` é produzido por acomodação; `inventario-liberado` quando há retenções invalidadas; `bloqueio-afeta-reserva` quando há reserva confirmada alcançada.
- O bloqueio criado **não é removível manualmente** (`CURATION_BLOCK_NOT_REMOVABLE`); só a retomada da aprovação o encerra (tarefa 26.0).
- Nenhuma reserva confirmada é cancelada ou alterada, em nenhuma circunstância.

## Arquivos Envolvidos

- **Criar:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/Sellability/CurationPropertySuspendedHandler.cs`
  - `../localizestay-backend/tests/LocalizeStay.UnitTests/Inventory/CurationPropertySuspendedHandlerTests.cs`
- **Referência:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/CommercialOffers/CurationOfferReturnedHandler.cs` (padrão de consumidor idempotente do módulo)
  - `../localizestay-backend/src/Modules/Curation/LocalizeStay.Modules.Curation.Contracts/CurationSellabilityEvents.cs` (criado em 4.0)
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Domain/DailyInventories/InventoryLedger.cs`
  - `.tasks/prd-controlar-allotment-disponibilidade-bloqueios-e-retencoes/adrs/adr-002.md`
- **Skills para consultar durante implementação:**
  - `dotnet-architecture` — `IIntegrationEventHandler<T>`, consumo idempotente, escopo de DI por evento
  - `dotnet-observability` — log estruturado com `eventId` e `propertyId`
  - `dotnet-testing` — reprocessamento do mesmo evento sem efeito duplicado

## Subtarefas

- [ ] 25.1 Implementar `CurationPropertySuspendedHandler` com deduplicação por `eventId`.
- [ ] 25.2 Iterar as acomodações da propriedade, criando um bloqueio `emergency`/`curationSuspension` por acomodação, **cada uma em sua transação**, via `InventoryLedger`.
- [ ] 25.3 Marcar o gate `propertyApproved` como `blocked` com origem `event` e gravar os eventos correspondentes na outbox.
- [ ] 25.4 Testar: suspensão bloqueia todas as acomodações; retenções invalidadas; reservas confirmadas intactas com `bloqueio-afeta-reserva` produzido; reprocessamento sem efeito duplicado.

## Sequenciamento

- Bloqueado por: 4.0, 7.0, 11.0, 17.0
- Desbloqueia: 26.0, 35.0
- Paralelizável: Sim; cria arquivos exclusivos.

## Rastreabilidade

- Esta tarefa cobre: RF-05 integralmente, RN-07, RN-15 e RN-16.
- Evidência esperada: `CurationPropertySuspendedHandlerTests` prova os dois critérios de aceite de RF-05; 35.0 prova a atomicidade por acomodação.

## Detalhes de Implementação

Critérios de aceite de RF-05 mapeados:

| Critério | Verificação |
|---|---|
| Todas as acomodações deixam de ser vendáveis, com efeito equivalente a bloqueio emergencial de origem D06, sem cancelar reservas | Um bloqueio por acomodação; reservas intactas |
| Propriedade suspensa com reservas confirmadas no período produz `bloqueio-afeta-reserva` para D05 | Evento na outbox |

Por que uma transação por acomodação, e não uma para a propriedade inteira:

> Uma propriedade com oito acomodações e noventa dias de allotment tocaria 720 linhas de `daily_inventory` em uma transação única, segurando locks por tempo suficiente para bloquear checkouts concorrentes em acomodações que a suspensão nem precisaria alcançar naquele instante. ADR-001 registra isso como risco explícito e a mitigação é exatamente esta.

Escopo temporal do bloqueio: da data corrente em diante, cobrindo a janela de allotment vigente. Datas passadas não são bloqueadas.

**Convenções da stack (das skills consultadas):**

- Consumidor implementa `IIntegrationEventHandler<T>` seguindo `CurationOfferReturnedHandler` (`dotnet-architecture`).
- Deduplicação por `eventId`; entrega at-least-once já garantida pelo `InProcessEventBus`.
- Toda mutação de saldo passa pelo `InventoryLedger`.
- Log estruturado com `eventId`, `propertyId`, `accommodationId`, `result` (`dotnet-observability`).
- Testes reprocessam o mesmo evento e verificam ausência de efeito duplicado (`dotnet-testing`).

## Critérios de Sucesso (Verificáveis)

- [ ] Testes passam: `dotnet test ../localizestay-backend/LocalizeStay.sln --filter "FullyQualifiedName~CurationPropertySuspendedHandlerTests"`
- [ ] Build compila sem erros: `dotnet build ../localizestay-backend/LocalizeStay.sln --no-restore`
- [ ] Formatação válida: `dotnet format ../localizestay-backend/LocalizeStay.sln --verify-no-changes --no-restore`
- [ ] Após a suspensão, todas as acomodações da propriedade têm `availableUnits: 0` nas datas alcançadas.
- [ ] O gate `propertyApproved` fica `blocked` com origem `event`, e `sellable` vira `false`.
- [ ] Reservas confirmadas permanecem inalteradas; `bloqueio-afeta-reserva` é produzido para cada uma alcançada.
- [ ] Retenções vigentes são invalidadas com `invalidatedByBlockId` preenchido.
- [ ] Reprocessar o mesmo `eventId` não cria bloqueio adicional nem evento duplicado.
- [ ] Cada acomodação é processada em transação própria — verificável por teste que falha o processamento da segunda e confirma que a primeira permaneceu aplicada.
