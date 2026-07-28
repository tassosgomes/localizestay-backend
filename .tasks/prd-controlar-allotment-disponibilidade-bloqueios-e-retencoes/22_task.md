---
status: pending
parallelizable: true
blocked_by: ["3.0", "9.0", "14.0", "15.0", "16.0"]
---

<task_context>
<domain>inventory/application/inventory-requests</domain>
<type>implementation</type>
<scope>core_feature</scope>
<complexity>medium</complexity>
<dependencies>temporal</dependencies>
<unblocks>"30.0, 31.0"</unblocks>
<vertical_slice>Uma solicitação recebida por WhatsApp ou e-mail entra na fila com SLA derivado, e a fila devolve o aviso emergencial no topo.</vertical_slice>
</task_context>

# Tarefa 22.0: Registrar, atualizar e ordenar a fila de solicitações

## Relacionada às User Stories

- [US-05] Parceiro solicita allotment e bloqueios pelos canais que já usa (cobertura direta)
- [US-06] Gestor mede o prazo de processamento (cobertura direta)

## Visão Geral

Quatro operações: `createInventoryRequest`, `updateInventoryRequest`, `listInventoryRequests` e `getInventoryRequest`.

A fila é o instrumento de apuração do SLA de quatro horas úteis e a garantia de que um aviso emergencial recebido de madrugada seja a **primeira ação da abertura da janela**. A ordenação padrão `priorityThenReceivedAt` ascendente é o que produz esse efeito.

**Registrar a solicitação não altera o inventário.** Nenhum canal humano muda capacidade automaticamente.

## Requisitos

- `createInventoryRequest` deriva janela, prazo e prioridade no **servidor**, via `IInventoryServiceWindow`; valores enviados pelo cliente são ignorados.
- `receivedAt` é o horário real da mensagem; futuro é recusado.
- Ordenação padrão `priorityThenReceivedAt` ascendente: emergenciais primeiro, depois por horário de recebimento.
- `overdue` é calculado **no servidor** comparando `slaDueAt` com o instante corrente, para solicitações ainda pendentes.
- `updateInventoryRequest` transiciona a situação; solicitação `processed` ou `cancelled` produz `409 REQUEST_ALREADY_CLOSED`.
- Fechamento calcula `processedWithinSla` e permite vincular `resultingAllotmentId` ou `resultingBlockId`.
- Filtros do contrato: `propertyId`, `status`, `requestType`, `priority`, `channel`, `overdue`, `sort`, `order`; paginação com teto de 100.
- Trilha de auditoria em cada transição, com autor e horário.

## Arquivos Envolvidos

- **Criar:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/InventoryRequests/InventoryRequestCommands.cs`
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/InventoryRequests/InventoryRequestQueries.cs`
  - `../localizestay-backend/tests/LocalizeStay.UnitTests/Inventory/InventoryRequestCommandHandlerTests.cs`
- **Referência:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Domain/InventoryRequests/InventoryRequest.cs` (criado em 9.0)
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/Timing/IInventoryServiceWindow.cs` (criado em 3.0)
  - `.tasks/prd-controlar-allotment-disponibilidade-bloqueios-e-retencoes/api-contract.md` (exemplo do aviso de madrugada)
- **Skills para consultar durante implementação:**
  - `dotnet-architecture` — CQRS, derivação no servidor
  - `dotnet-performance` — ordenação coberta pelo índice `(status, priority, received_at)`
  - `restful-api` — filtros, ordenação e paginação declarados no contrato
  - `dotnet-testing` — mockar apenas `IInventoryServiceWindow` e o relógio

## Subtarefas

- [ ] 22.1 Implementar `CreateInventoryRequestCommandHandler`, derivando janela, prazos e prioridade no servidor.
- [ ] 22.2 Implementar `UpdateInventoryRequestCommandHandler`, com `REQUEST_ALREADY_CLOSED`, cálculo de `processedWithinSla` e vínculo com a alteração resultante.
- [ ] 22.3 Implementar `ListInventoryRequestsQueryHandler` com `priorityThenReceivedAt` como ordenação padrão, filtros e `overdue` calculado no servidor; e `GetInventoryRequestQueryHandler`.
- [ ] 22.4 Testar: aviso de madrugada no topo da fila, derivação do SLA, `overdue` correto, reabertura recusada e ordenação estável.

## Sequenciamento

- Bloqueado por: 3.0, 9.0, 14.0, 15.0, 16.0
- Desbloqueia: 30.0, 31.0
- Paralelizável: Sim; cria arquivos exclusivos.

## Rastreabilidade

- Esta tarefa cobre: RF-04 na parte de fila e SLA, e RN-14 na camada de aplicação.
- Evidência esperada: `InventoryRequestCommandHandlerTests` reproduz o caso do PRD — aviso emergencial recebido fora da janela aparece no topo da fila com o horário original preservado.

## Detalhes de Implementação

Caso canônico do PRD e do contrato:

```
Recebido:  2026-07-26T03:40:00Z  (00h40 local, domingo)  emergency: true
Derivado:  receivedOutsideWindow = true
           slaStartsAt = 2026-07-26T11:00:00Z   (08h00 do próximo período útil)
           slaDueAt    = 2026-07-26T15:00:00Z   (quatro horas úteis depois)
           priority    = emergency
Efeito:    aparece no topo da fila por priorityThenReceivedAt
```

> É essa ordenação que cumpre a decisão do PRD: **sem plantão fora da janela**, o aviso de madrugada não vira bloqueio às 00h40 — vira a primeira ação das 08h00. O horário original fica registrado para que a métrica de "exposição fora da janela" seja apurável.

Critérios de aceite de RF-04 cobertos aqui:

| Critério | Verificação |
|---|---|
| Origem, canal, responsável e horário registrados para apuração do SLA | Campos persistidos no registro |
| Recebimento fora da janela conta a partir das 08h00 do próximo período útil | `slaStartsAt` derivado |
| Aviso emergencial fora da janela aparece no topo, antes de qualquer allotment | Ordenação `priorityThenReceivedAt` |

**Convenções da stack (das skills consultadas):**

- Campos derivados no servidor, nunca aceitos do cliente (`dotnet-architecture`).
- Ordenação coberta pelo índice declarado em 13.0 (`dotnet-performance`).
- Filtros e ordenação exatamente como declarados no contrato (`restful-api`).
- Logs com `requestId`, `channel`, `priority`, `result` — nunca o conteúdo da mensagem do parceiro (`dotnet-production-readiness`).

## Critérios de Sucesso (Verificáveis)

- [ ] Testes passam: `dotnet test ../localizestay-backend/LocalizeStay.sln --filter "FullyQualifiedName~InventoryRequestCommandHandlerTests"`
- [ ] Build compila sem erros: `dotnet build ../localizestay-backend/LocalizeStay.sln --no-restore`
- [ ] Formatação válida: `dotnet format ../localizestay-backend/LocalizeStay.sln --verify-no-changes --no-restore`
- [ ] O caso canônico produz os quatro campos derivados exatamente como no contrato.
- [ ] `priority` enviada pelo cliente é ignorada; a derivada de `emergency` prevalece.
- [ ] Uma solicitação emergencial recebida depois de uma padrão aparece **antes** dela na fila.
- [ ] `overdue: true` filtra apenas pendentes com `slaDueAt` no passado.
- [ ] Solicitação `processed` que tenta voltar a `pending` produz `REQUEST_ALREADY_CLOSED`.
- [ ] Registrar solicitação **não** altera nenhuma linha de `daily_inventory`.
