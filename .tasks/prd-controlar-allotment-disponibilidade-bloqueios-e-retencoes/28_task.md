---
status: pending
parallelizable: true
blocked_by: ["2.0", "18.0"]
---

<task_context>
<domain>inventory/endpoints/allotments</domain>
<type>implementation</type>
<scope>core_feature</scope>
<complexity>medium</complexity>
<dependencies>http_server</dependencies>
<unblocks>"33.0, 34.0"</unblocks>
<vertical_slice>As cinco operações de allotment ficam acessíveis por HTTP com as permissões, os status e o header Location do contrato.</vertical_slice>
</task_context>

# Tarefa 28.0: Expor os cinco endpoints de allotment

## Relacionada às User Stories

- [US-01] Registrar o allotment contratado (cobertura direta)

## Visão Geral

Cinco operações Minimal API sobre `/api/v1/properties/{propertyId}/allotments`: `listAllotments`, `createAllotment`, `getAllotment`, `updateAllotment` e `cancelAllotment`.

O arquivo é disjunto dos demais grupos de endpoint; a única interseção com outras tarefas é a linha de registro em `InventoryEndpoints.cs`.

## Requisitos

- Leitura com `inventory:read`; escrita, alteração e cancelamento com `inventory:write`. **Sem hierarquia**: quem só tem `write` não lê.
- `POST` responde `201` com header `Location` apontando para o recurso criado.
- `DELETE` responde `204` **sem corpo**.
- `PATCH` recebe `expectedRevision` e propaga `409 REVISION_MISMATCH`.
- Paths, verbos, `operationId`, parâmetros e status conforme o `api-contract.yaml`.
- Ator (`createdBy`/`updatedBy`) extraído do JWT e passado ao Command; **nunca** aceito do corpo.
- Paginação `_page`/`_size` com teto de 100 na listagem.
- Erros traduzidos pelo `GlobalExceptionHandler`; nenhum `try/catch` no endpoint.

## Arquivos Envolvidos

- **Criar:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Endpoints/AllotmentEndpoints.cs`
  - `../localizestay-backend/tests/LocalizeStay.IntegrationTests/Inventory/AllotmentEndpointsTests.cs`
- **Modificar:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Endpoints/InventoryEndpoints.cs` (uma linha de registro)
- **Referência:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Endpoints/CommercialRateEndpoints.cs` (padrão de CRUD do módulo)
  - `.tasks/prd-controlar-allotment-disponibilidade-bloqueios-e-retencoes/api-contract.yaml` (fonte soberana)
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Endpoints/ContractResponseMetadataExtensions.cs`
- **Skills para consultar durante implementação:**
  - `restful-api` — 201 com `Location`, 204 sem corpo, paginação, Problem Details
  - `dotnet-architecture` — Minimal API delegando ao `IDispatcher`
  - `dotnet-testing` — `WebApplicationFactory` + Testcontainers

## Subtarefas

- [ ] 28.1 Criar `AllotmentEndpoints` com as cinco rotas, cada uma com a policy correta e os `Produces` declarados.
- [ ] 28.2 Extrair o ator do JWT e compor os Commands; garantir `Location` no `201` e ausência de corpo no `204`.
- [ ] 28.3 Registrar o grupo em `InventoryEndpoints.cs`.
- [ ] 28.4 Testar por HTTP: os cinco caminhos felizes e os status 401, 403, 404, 409 e 422 declarados no contrato.

## Sequenciamento

- Bloqueado por: 2.0, 18.0
- Desbloqueia: 33.0, 34.0
- Paralelizável: Sim; arquivo de endpoint exclusivo, disjunto de 27.0, 29.0, 30.0 e 31.0.

## Rastreabilidade

- Esta tarefa cobre: RF-01 na superfície HTTP.
- Evidência esperada: `AllotmentEndpointsTests` exercita as cinco operações e os erros do contrato; 33.0 certifica a conformidade formal.

## Detalhes de Implementação

| Operação | Verbo e path | Permissão | Sucesso |
|---|---|---|---:|
| `listAllotments` | `GET /properties/{propertyId}/allotments` | `inventory:read` | 200 |
| `createAllotment` | `POST /properties/{propertyId}/allotments` | `inventory:write` | 201 + `Location` |
| `getAllotment` | `GET /properties/{propertyId}/allotments/{allotmentId}` | `inventory:read` | 200 |
| `updateAllotment` | `PATCH /properties/{propertyId}/allotments/{allotmentId}` | `inventory:write` | 200 |
| `cancelAllotment` | `DELETE /properties/{propertyId}/allotments/{allotmentId}` | `inventory:write` | 204 |

Erros a exercitar no teste:

| HTTP | `code` | Cenário |
|---:|---|---|
| 404 | `ACCOMMODATION_NOT_FOUND` | Acomodação de outra propriedade |
| 409 | `ALLOTMENT_PERIOD_OVERLAP` | Período sobreposto |
| 409 | `REVISION_MISMATCH` | `expectedRevision` obsoleto |
| 422 | `INVALID_DATE_RANGE` | `endDate` antes de `startDate` |
| 422 | `ALLOTMENT_BELOW_COMMITTED` | Redução abaixo do comprometido, com `metadata.conflictingDates` |

> **`units: 1` responde 201**, não 422. A categoria abaixo do piso comercial é comercializável; ela apenas não conta para a meta de cobertura. É um caso de teste, não um erro.

**Convenções da stack (das skills consultadas):**

- Endpoints não contêm regra de negócio; delegam ao `IDispatcher` (`dotnet-architecture`).
- `201` sempre com `Location`; `204` sempre sem corpo (`restful-api`).
- Policy referenciada pela constante do catálogo (`dotnet-production-readiness`).
- Testes de integração com Testcontainers PostgreSQL (`dotnet-testing`).

## Critérios de Sucesso (Verificáveis)

- [ ] Testes passam: `dotnet test ../localizestay-backend/tests/LocalizeStay.IntegrationTests --filter "FullyQualifiedName~AllotmentEndpointsTests"`
- [ ] Build compila sem erros: `dotnet build ../localizestay-backend/LocalizeStay.sln --no-restore`
- [ ] Formatação válida: `dotnet format ../localizestay-backend/LocalizeStay.sln --verify-no-changes --no-restore`
- [ ] `POST` válido responde 201 com `Location` apontando para `getAllotment`.
- [ ] `DELETE` responde 204 sem corpo.
- [ ] Token com `inventory:write` mas sem `inventory:read` recebe 403 no `GET`.
- [ ] Sem token, todas as cinco respondem 401.
- [ ] `POST` com `units: 1` responde 201 com `belowCommercialFloor: true`.
- [ ] Todos os erros respondem `application/problem+json` com `code` e `traceId`.
