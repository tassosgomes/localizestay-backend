---
status: pending
parallelizable: true
blocked_by: ["2.0", "22.0"]
---

<task_context>
<domain>inventory/endpoints/inventory-requests</domain>
<type>implementation</type>
<scope>core_feature</scope>
<complexity>medium</complexity>
<dependencies>http_server</dependencies>
<unblocks>"33.0, 34.0"</unblocks>
<vertical_slice>As quatro operações da fila ficam acessíveis por HTTP, com SLA derivado no servidor e ordenação que coloca o emergencial no topo.</vertical_slice>
</task_context>

# Tarefa 30.0: Expor os quatro endpoints da fila de solicitações

## Relacionada às User Stories

- [US-05] Parceiro solicita allotment e bloqueios pelos canais que já usa (cobertura direta)
- [US-06] Gestor mede o prazo de processamento (suporte)

## Visão Geral

Quatro operações sobre `/api/v1/inventory-requests`: `listInventoryRequests`, `createInventoryRequest`, `getInventoryRequest` e `updateInventoryRequest`.

O recurso é próprio, e não subordinado à propriedade, porque uma solicitação pode chegar antes de se saber a qual acomodação se refere.

## Requisitos

- Leitura com `inventory:read`; registro e atualização com `inventory:write`.
- `POST` responde `201` com `Location` e devolve os quatro campos derivados no servidor: `receivedOutsideWindow`, `slaStartsAt`, `slaDueAt` e `priority`.
- Campos derivados enviados pelo cliente são **ignorados**, não rejeitados.
- Ordenação padrão `priorityThenReceivedAt` ascendente; parâmetros `sort` e `order` conforme o contrato.
- Filtros: `propertyId`, `status`, `requestType`, `priority`, `channel`, `overdue`; paginação `_page`/`_size` com teto de 100.
- `PATCH` transiciona a situação e propaga `409 REQUEST_ALREADY_CLOSED`.
- Ator extraído do JWT; nunca do corpo.

## Arquivos Envolvidos

- **Criar:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Endpoints/InventoryRequestEndpoints.cs`
  - `../localizestay-backend/tests/LocalizeStay.IntegrationTests/Inventory/InventoryRequestEndpointsTests.cs`
- **Modificar:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Endpoints/InventoryEndpoints.cs` (uma linha de registro)
- **Referência:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Endpoints/PropertyOnboardingReadEndpoints.cs` (padrão de listagem com filtros do módulo)
  - `.tasks/prd-controlar-allotment-disponibilidade-bloqueios-e-retencoes/api-contract.md` (exemplo do aviso de madrugada)
- **Skills para consultar durante implementação:**
  - `restful-api` — filtros, ordenação, paginação, 201 com `Location`
  - `dotnet-architecture` — Minimal API delegando ao `IDispatcher`
  - `dotnet-testing` — `WebApplicationFactory` + Testcontainers

## Subtarefas

- [ ] 30.1 Criar `InventoryRequestEndpoints` com as quatro rotas, policies e `Produces` declarados.
- [ ] 30.2 Mapear filtros, ordenação e paginação da listagem exatamente como no contrato.
- [ ] 30.3 Registrar o grupo em `InventoryEndpoints.cs`.
- [ ] 30.4 Testar por HTTP: registro do aviso de madrugada com os quatro campos derivados, ordenação com emergencial no topo, filtro `overdue` e `REQUEST_ALREADY_CLOSED`.

## Sequenciamento

- Bloqueado por: 2.0, 22.0
- Desbloqueia: 33.0, 34.0
- Paralelizável: Sim; arquivo de endpoint exclusivo, disjunto de 27.0, 28.0, 29.0 e 31.0.

## Rastreabilidade

- Esta tarefa cobre: RF-04 na parte de fila, na superfície HTTP.
- Evidência esperada: `InventoryRequestEndpointsTests` reproduz o exemplo do contrato e prova a ordenação da fila.

## Detalhes de Implementação

| Operação | Verbo e path | Permissão | Sucesso |
|---|---|---|---:|
| `listInventoryRequests` | `GET /api/v1/inventory-requests` | `inventory:read` | 200 |
| `createInventoryRequest` | `POST /api/v1/inventory-requests` | `inventory:write` | 201 + `Location` |
| `getInventoryRequest` | `GET /api/v1/inventory-requests/{requestId}` | `inventory:read` | 200 |
| `updateInventoryRequest` | `PATCH /api/v1/inventory-requests/{requestId}` | `inventory:write` | 200 |

Resposta-alvo do registro, conforme o contrato:

```json
{
  "priority": "emergency",
  "status": "pending",
  "receivedAt": "2026-07-26T03:40:00Z",
  "receivedOutsideWindow": true,
  "slaStartsAt": "2026-07-26T11:00:00Z",
  "slaDueAt": "2026-07-26T15:00:00Z",
  "processedWithinSla": null,
  "resultingAllotmentId": null,
  "resultingBlockId": null
}
```

> **Registrar a solicitação não altera o inventário.** O vínculo com a alteração só acontece quando o operador envia `requestId` no `POST` de allotment (28.0) ou de bloqueio (29.0). Nenhum canal humano muda capacidade automaticamente — é decisão explícita do PRD.

**Convenções da stack (das skills consultadas):**

- Endpoints delegam ao `IDispatcher` (`dotnet-architecture`).
- Filtros e ordenação exatamente como declarados no contrato (`restful-api`).
- Policy referenciada pela constante do catálogo (`dotnet-production-readiness`).
- Nenhum conteúdo de mensagem de parceiro em log (`dotnet-production-readiness`).

## Critérios de Sucesso (Verificáveis)

- [ ] Testes passam: `dotnet test ../localizestay-backend/tests/LocalizeStay.IntegrationTests --filter "FullyQualifiedName~InventoryRequestEndpointsTests"`
- [ ] Build compila sem erros: `dotnet build ../localizestay-backend/LocalizeStay.sln --no-restore`
- [ ] Formatação válida: `dotnet format ../localizestay-backend/LocalizeStay.sln --verify-no-changes --no-restore`
- [ ] `POST` com `receivedAt = 2026-07-26T03:40:00Z` e `emergency: true` devolve os quatro campos derivados exatamente como no contrato.
- [ ] `priority` enviada no corpo é ignorada.
- [ ] A listagem devolve a solicitação emergencial antes de uma padrão recebida mais cedo.
- [ ] `overdue=true` devolve apenas pendentes com `slaDueAt` no passado.
- [ ] `PATCH` sobre solicitação `processed` responde 409 com `REQUEST_ALREADY_CLOSED`.
- [ ] Registrar solicitação não altera nenhuma linha de `daily_inventory`.
