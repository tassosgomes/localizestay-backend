---
status: pending
parallelizable: true
blocked_by: ["3.0"]
---

<task_context>
<domain>inventory/domain/inventory-requests</domain>
<type>implementation</type>
<scope>core_feature</scope>
<complexity>medium</complexity>
<dependencies>temporal</dependencies>
<unblocks>"13.0, 22.0"</unblocks>
<vertical_slice>Uma solicitação recebida por WhatsApp ou e-mail nasce com prioridade e prazo de SLA derivados da janela de atendimento.</vertical_slice>
</task_context>

# Tarefa 9.0: Modelar `InventoryRequest` com prioridade e SLA derivado

## Relacionada às User Stories

- [US-05] Parceiro solicita allotment e bloqueios pelos canais que já usa (direta)
- [US-06] Gestor mede o prazo de processamento (direta)

## Visão Geral

RF-04 exige registrar origem, canal, responsável e horário de recebimento de cada solicitação, para apuração do SLA de quatro horas úteis. RN-14 define a janela de atendimento e a regra de que, fora dela, a contagem começa às 08h00 do próximo período útil.

Um aviso emergencial recebido fora da janela precisa aparecer **no topo da fila** na abertura, com o horário original de recebimento preservado.

**Registrar a solicitação não altera o inventário.** O vínculo com a alteração acontece quando o operador envia `requestId` no POST de allotment ou de bloqueio.

## Requisitos

- `Channel` com `whatsapp` e `email`; `RequestType` com `allotmentGrant`, `allotmentChange`, `block` e `blockRemoval`.
- `Priority` (`emergency`, `standard`) **derivada** do campo `emergency` do request — nunca recebida pronta do cliente.
- `Status` com `pending`, `inProgress`, `processed`, `cancelled`.
- `ReceivedAt` é o horário real da mensagem, não o do registro; não pode ser futuro.
- `ReceivedOutsideWindow`, `SlaStartsAt` e `SlaDueAt` são **derivados no servidor** via `IInventoryServiceWindow`; valores enviados pelo cliente são ignorados.
- `ProcessedWithinSla` é calculado no fechamento; `null` enquanto pendente.
- Solicitação `processed` ou `cancelled` **não volta** a `pending` — lança `REQUEST_ALREADY_CLOSED`.
- `ResultingAllotmentId` e `ResultingBlockId` registram a alteração que a solicitação originou.

## Arquivos Envolvidos

- **Criar:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Domain/InventoryRequests/InventoryRequest.cs`
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Domain/InventoryRequests/InventoryRequestValues.cs`
  - `../localizestay-backend/tests/LocalizeStay.UnitTests/Inventory/InventoryRequestTests.cs`
- **Referência:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/Timing/IInventoryServiceWindow.cs` (criado em 3.0)
  - `.tasks/prd-controlar-allotment-disponibilidade-bloqueios-e-retencoes/api-contract.md` (schema `InventoryRequest` e exemplo do aviso de madrugada)
- **Skills para consultar durante implementação:**
  - `dotnet-architecture` — aggregate root com transição de estado explícita
  - `dotnet-code-quality` — enums, propriedades derivadas, sem setters públicos
  - `dotnet-testing` — mockar `IInventoryServiceWindow` com Moq; nunca mockar o próprio domínio

## Subtarefas

- [ ] 9.1 Declarar `RequestChannel`, `RequestType`, `RequestPriority` e `RequestStatus` em `InventoryRequestValues.cs`.
- [ ] 9.2 Modelar o agregado com factory `Register`, que recebe `IInventoryServiceWindow` e deriva prioridade, janela e prazos.
- [ ] 9.3 Implementar `Transition`, com a recusa `REQUEST_ALREADY_CLOSED`, o cálculo de `ProcessedWithinSla` no fechamento e o vínculo com allotment ou bloqueio resultante.
- [ ] 9.4 Testar: aviso de madrugada fora da janela, prioridade emergencial derivada, `receivedAt` futuro recusado, e reabertura de solicitação encerrada recusada.

## Sequenciamento

- Bloqueado por: 3.0
- Desbloqueia: 13.0, 22.0
- Paralelizável: Sim; após 3.0, não colide com nenhuma outra tarefa de domínio.

## Rastreabilidade

- Esta tarefa cobre: RF-04 no domínio e RN-14 integralmente.
- Evidência esperada: `InventoryRequestTests` reproduz o exemplo do contrato — recebido `2026-07-26T03:40:00Z`, `receivedOutsideWindow: true`, `slaStartsAt: 2026-07-26T11:00:00Z`, `slaDueAt: 2026-07-26T15:00:00Z`, `priority: emergency`.

## Detalhes de Implementação

Caso canônico do PRD e do contrato — aviso de indisponibilidade recebido às 00h40 de domingo:

```json
{
  "receivedAt": "2026-07-26T03:40:00Z",
  "emergency": true,
  "priority": "emergency",
  "receivedOutsideWindow": true,
  "slaStartsAt": "2026-07-26T11:00:00Z",
  "slaDueAt": "2026-07-26T15:00:00Z",
  "processedWithinSla": null
}
```

Ordenação da fila (`priorityThenReceivedAt` ascendente) é responsabilidade da query (22.0), mas depende dos campos derivados aqui. **É essa ordem que garante que o aviso de madrugada seja a primeira ação da abertura da janela.**

Transições permitidas:

```
pending ──> inProgress ──> processed
   │             │
   └─────────────┴──> cancelled

processed | cancelled ──> (qualquer)  ==>  409 REQUEST_ALREADY_CLOSED
```

**Convenções da stack (das skills consultadas):**

- A janela entra como porta injetada, não como cálculo inline no agregado (`dotnet-architecture`).
- Campos derivados são propriedades calculadas ou gravadas na factory, nunca setters (`dotnet-code-quality`).
- Testes mockam apenas `IInventoryServiceWindow`; o agregado é exercitado de verdade (`dotnet-testing`).
- Logs desta área usam `requestId`, `propertyId`, `channel`, `priority` — nunca conteúdo da mensagem do parceiro (`dotnet-production-readiness`).

## Critérios de Sucesso (Verificáveis)

- [ ] Testes passam: `dotnet test ../localizestay-backend/LocalizeStay.sln --filter "FullyQualifiedName~InventoryRequestTests"`
- [ ] Build compila sem erros: `dotnet build ../localizestay-backend/LocalizeStay.sln --no-restore`
- [ ] Formatação válida: `dotnet format ../localizestay-backend/LocalizeStay.sln --verify-no-changes --no-restore`
- [ ] O caso canônico do contrato produz exatamente os quatro campos derivados esperados.
- [ ] `emergency: true` produz `priority = emergency`; qualquer `priority` enviada pelo cliente é ignorada.
- [ ] `receivedAt` no futuro é recusado.
- [ ] Solicitação `processed` que tenta voltar a `pending` lança `REQUEST_ALREADY_CLOSED`.
- [ ] Fechamento dentro do prazo produz `ProcessedWithinSla = true`; fora, `false`.
