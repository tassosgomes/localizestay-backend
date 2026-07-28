---
status: pending
parallelizable: true
blocked_by: ["2.0", "42.0"]
---

<task_context>
<domain>inventory/endpoints/inventory-holds</domain>
<type>implementation</type>
<scope>core_feature</scope>
<complexity>medium</complexity>
<dependencies>http_server</dependencies>
<unblocks>"47.0, 49.0, 50.0"</unblocks>
<vertical_slice>As quatro operações de retenção ficam acessíveis por HTTP para D03, com Idempotency-Key nas escritas críticas.</vertical_slice>
</task_context>

# Tarefa 43.0: Expor os quatro endpoints de retenção

## Relacionada às User Stories

- [US-04] Acomodação separada durante o checkout (cobertura direta)

## Visão Geral

Quatro operações sobre `/api/v1/inventory-holds`: `createInventoryHold`, `getInventoryHold`, `releaseInventoryHold` e `commitInventoryHold`.

O consumidor é **D03**, no início do checkout. O viajante nunca chama diretamente. Todas exigem `inventory:hold`, exceto a consulta, que exige `inventory:read`.

## Requisitos

- `POST /inventory-holds` e `POST /inventory-holds/{holdId}/commitment` exigem header `Idempotency-Key`; ausência produz `400`.
- `POST /inventory-holds` responde `201` com `Location`; `POST .../commitment` responde `201`.
- `DELETE /inventory-holds/{holdId}` responde `204` **sem corpo**, com query opcional `reason`.
- `GET /inventory-holds/{holdId}` exige `inventory:read`; as demais exigem `inventory:hold`.
- Paths, verbos, `operationId`, parâmetros e status conforme o `api-contract.yaml`.
- `expiresAt` é devolvido pelo servidor; nenhum parâmetro de duração é aceito.
- Erros traduzidos pelo `GlobalExceptionHandler`, incluindo `metadata.unavailableDates` em `INSUFFICIENT_AVAILABILITY`.

## Arquivos Envolvidos

- **Criar:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Endpoints/InventoryHoldEndpoints.cs`
  - `../localizestay-backend/tests/LocalizeStay.IntegrationTests/Inventory/InventoryHoldEndpointsTests.cs`
- **Modificar:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Endpoints/InventoryEndpoints.cs` (uma linha de registro)
- **Referência:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Endpoints/InventoryBlockEndpoints.cs` (padrão de `Idempotency-Key`, criado em 29.0)
  - `.tasks/prd-controlar-allotment-disponibilidade-bloqueios-e-retencoes/api-contract.yaml`
- **Skills para consultar durante implementação:**
  - `restful-api` — 201 com `Location`, 204 sem corpo, header de idempotência
  - `dotnet-architecture` — Minimal API delegando ao `IDispatcher`
  - `dotnet-testing` — `WebApplicationFactory` + Testcontainers

## Subtarefas

- [ ] 43.1 Criar `InventoryHoldEndpoints` com as quatro rotas, policies e `Produces` declarados.
- [ ] 43.2 Exigir e propagar `Idempotency-Key` nas duas operações que o contrato marca como obrigatório.
- [ ] 43.3 Registrar o grupo em `InventoryEndpoints.cs`.
- [ ] 43.4 Testar por HTTP: criação com `expiresAt` devolvido pelo servidor, recusa por saldo, liberação idempotente, comprometimento vigente e revalidado.

## Sequenciamento

- Bloqueado por: 2.0, 42.0
- Desbloqueia: 47.0, 49.0, 50.0
- Paralelizável: Sim; arquivo de endpoint exclusivo.

## Rastreabilidade

- Esta tarefa cobre: RF-06, RF-07 e RF-08 na superfície HTTP.
- Evidência esperada: `InventoryHoldEndpointsTests` exercita as quatro operações; 49.0 certifica a conformidade formal.

## Detalhes de Implementação

| Operação | Verbo e path | Permissão | Header | Sucesso |
|---|---|---|---|---:|
| `createInventoryHold` | `POST /api/v1/inventory-holds` | `inventory:hold` | `Idempotency-Key` | 201 + `Location` |
| `getInventoryHold` | `GET /api/v1/inventory-holds/{holdId}` | `inventory:read` | — | 200 |
| `releaseInventoryHold` | `DELETE /api/v1/inventory-holds/{holdId}` | `inventory:hold` | — | 204 |
| `commitInventoryHold` | `POST /api/v1/inventory-holds/{holdId}/commitment` | `inventory:hold` | `Idempotency-Key` | 201 |

Erros a exercitar:

| HTTP | `code` | Cenário |
|---:|---|---|
| 422 | `INSUFFICIENT_AVAILABILITY` | Sem saldo em alguma noite, com `metadata.unavailableDates` |
| 422 | `COMMITMENT_WITHOUT_AVAILABILITY` | Retenção expirada sem saldo para comprometer |
| 409 | `HOLD_ALREADY_COMMITTED` | `DELETE` sobre retenção já comprometida |
| 409 | `IDEMPOTENCY_KEY_REUSED` | Mesma chave, corpo diferente |
| 404 | `HOLD_NOT_FOUND` | Retenção inexistente |

> `DELETE` sobre retenção já expirada, liberada ou invalidada responde **204**, não 404 nem 409. A idempotência é por desenho, e é o que permite a D03 chamar a liberação sem antes consultar o estado.

**Convenções da stack (das skills consultadas):**

- Endpoints delegam ao `IDispatcher`; nenhuma regra de negócio na camada HTTP (`dotnet-architecture`).
- `201` com `Location`; `204` sem corpo (`restful-api`).
- Policy referenciada pela constante do catálogo (`dotnet-production-readiness`).
- Nenhum dado do viajante em resposta ou log — pertence a D03.

## Critérios de Sucesso (Verificáveis)

- [ ] Testes passam: `dotnet test ../localizestay-backend/tests/LocalizeStay.IntegrationTests --filter "FullyQualifiedName~InventoryHoldEndpointsTests"`
- [ ] Build compila sem erros: `dotnet build ../localizestay-backend/LocalizeStay.sln --no-restore`
- [ ] Formatação válida: `dotnet format ../localizestay-backend/LocalizeStay.sln --verify-no-changes --no-restore`
- [ ] `POST /inventory-holds` sem `Idempotency-Key` responde 400.
- [ ] A resposta de criação traz `expiresAt` igual a `heldAt + 15 min`.
- [ ] `DELETE` sobre retenção já expirada responde 204.
- [ ] `DELETE` sobre retenção comprometida responde 409 `HOLD_ALREADY_COMMITTED`.
- [ ] Recusa por saldo responde 422 com `metadata.unavailableDates`.
- [ ] Token com `inventory:read` mas sem `inventory:hold` recebe 403 no `POST`.
