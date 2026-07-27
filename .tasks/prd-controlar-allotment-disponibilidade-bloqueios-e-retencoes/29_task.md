---
status: pending
parallelizable: true
blocked_by: ["2.0", "19.0", "20.0", "21.0"]
---

<task_context>
<domain>inventory/endpoints/inventory-blocks</domain>
<type>implementation</type>
<scope>core_feature</scope>
<complexity>medium</complexity>
<dependencies>http_server</dependencies>
<unblocks>"33.0, 34.0"</unblocks>
<vertical_slice>As cinco operações de bloqueio ficam acessíveis por HTTP, com Idempotency-Key obrigatório na criação e a prévia de impacto alimentando a confirmação explícita.</vertical_slice>
</task_context>

# Tarefa 29.0: Expor os cinco endpoints de bloqueio

## Relacionada às User Stories

- [US-03] Bloquear datas imediatamente ao receber um aviso de indisponibilidade (cobertura direta)

## Visão Geral

Cinco operações sobre `/api/v1/properties/{propertyId}/inventory-blocks`: `listInventoryBlocks`, `createInventoryBlock`, `previewInventoryBlockImpact`, `getInventoryBlock` e `removeInventoryBlock`.

A criação exige header `Idempotency-Key`. A prévia de impacto é `POST` porque recebe corpo, mas **não cria recurso e não muda estado** — responde `200`, nunca `201`.

## Requisitos

- Leitura com `inventory:read`; criação, prévia e remoção com `inventory:block`.
- `Idempotency-Key` obrigatório em `createInventoryBlock`; ausência produz `400`, reuso com corpo diferente produz `409 IDEMPOTENCY_KEY_REUSED`.
- `POST /inventory-blocks` responde `201` com `Location`; `POST /impact-preview` responde `200`.
- `PATCH /{blockId}` com `{ "status": "removed", "removalReason": "..." }` responde `200`.
- Paths, verbos, `operationId`, parâmetros e status conforme o `api-contract.yaml`.
- Ator extraído do JWT; nunca do corpo.
- Erros traduzidos pelo `GlobalExceptionHandler`, incluindo `metadata.freeBalanceByDate` em `INSUFFICIENT_FREE_BALANCE`.

## Arquivos Envolvidos

- **Criar:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Endpoints/InventoryBlockEndpoints.cs`
  - `../localizestay-backend/tests/LocalizeStay.IntegrationTests/Inventory/InventoryBlockEndpointsTests.cs`
- **Modificar:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Endpoints/InventoryEndpoints.cs` (uma linha de registro)
- **Referência:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Endpoints/CommercialOfferWorkflowEndpoints.cs` (padrão de `Idempotency-Key` no módulo)
  - `.tasks/prd-controlar-allotment-disponibilidade-bloqueios-e-retencoes/api-contract.yaml`
- **Skills para consultar durante implementação:**
  - `restful-api` — 201 com `Location`, 200 para simulação, header de idempotência
  - `dotnet-architecture` — Minimal API delegando ao `IDispatcher`
  - `dotnet-testing` — `WebApplicationFactory` + Testcontainers

## Subtarefas

- [ ] 29.1 Criar `InventoryBlockEndpoints` com as cinco rotas, policies e `Produces` declarados.
- [ ] 29.2 Exigir e propagar `Idempotency-Key` na criação, com `400` quando ausente.
- [ ] 29.3 Registrar o grupo em `InventoryEndpoints.cs`.
- [ ] 29.4 Testar por HTTP: planejado aceito e recusado, emergencial com e sem confirmação, prévia, remoção, remoção dupla e bloqueio de curadoria não removível.

## Sequenciamento

- Bloqueado por: 2.0, 19.0, 20.0, 21.0
- Desbloqueia: 33.0, 34.0
- Paralelizável: Sim; arquivo de endpoint exclusivo, disjunto de 27.0, 28.0, 30.0 e 31.0.

## Rastreabilidade

- Esta tarefa cobre: RF-02 na superfície HTTP, com os cinco critérios de aceite.
- Evidência esperada: `InventoryBlockEndpointsTests` exercita planejado, emergencial, simulação e remoção; 33.0 certifica a conformidade formal.

## Detalhes de Implementação

| Operação | Verbo e path | Permissão | Header | Sucesso |
|---|---|---|---|---:|
| `listInventoryBlocks` | `GET .../inventory-blocks` | `inventory:read` | — | 200 |
| `createInventoryBlock` | `POST .../inventory-blocks` | `inventory:block` | `Idempotency-Key` | 201 + `Location` |
| `previewInventoryBlockImpact` | `POST .../inventory-blocks/impact-preview` | `inventory:block` | — | **200** |
| `getInventoryBlock` | `GET .../inventory-blocks/{blockId}` | `inventory:read` | — | 200 |
| `removeInventoryBlock` | `PATCH .../inventory-blocks/{blockId}` | `inventory:block` | — | 200 |

> A prévia usa `POST` por receber corpo, mas **responde 200 e não cria recurso**. Retornar `201` aqui seria um erro de contrato, não um detalhe: significaria que a simulação criou algo.

Erros a exercitar:

| HTTP | `code` | Cenário |
|---:|---|---|
| 422 | `INSUFFICIENT_FREE_BALANCE` | Planejado acima do saldo livre, com `metadata.freeBalanceByDate` |
| 422 | `EMERGENCY_BLOCK_CONFIRMATION_REQUIRED` | Emergencial sem `confirmEmergencyImpact` |
| 422 | `REASON_NOTE_REQUIRED` | `reason: other` sem `reasonNote` |
| 422 | `CURATION_BLOCK_NOT_REMOVABLE` | `PATCH` sobre bloqueio de curadoria |
| 409 | `IDEMPOTENCY_KEY_REUSED` | Mesma chave, corpo diferente |
| 409 | `BLOCK_ALREADY_REMOVED` | `PATCH` sobre bloqueio já removido |

**Convenções da stack (das skills consultadas):**

- Endpoints delegam ao `IDispatcher`; nenhuma regra de negócio na camada HTTP (`dotnet-architecture`).
- `Idempotency-Key` lido do header e repassado ao Command, como já feito na F02.
- Policy referenciada pela constante do catálogo (`dotnet-production-readiness`).
- Testes de integração com Testcontainers PostgreSQL (`dotnet-testing`).

## Critérios de Sucesso (Verificáveis)

- [ ] Testes passam: `dotnet test ../localizestay-backend/tests/LocalizeStay.IntegrationTests --filter "FullyQualifiedName~InventoryBlockEndpointsTests"`
- [ ] Build compila sem erros: `dotnet build ../localizestay-backend/LocalizeStay.sln --no-restore`
- [ ] Formatação válida: `dotnet format ../localizestay-backend/LocalizeStay.sln --verify-no-changes --no-restore`
- [ ] `POST /inventory-blocks` sem `Idempotency-Key` responde 400.
- [ ] `POST /impact-preview` responde **200** e não cria bloqueio algum.
- [ ] Emergencial sem `confirmEmergencyImpact` responde 422 com `EMERGENCY_BLOCK_CONFIRMATION_REQUIRED`.
- [ ] Planejado acima do saldo livre responde 422 com `metadata.freeBalanceByDate` preenchido.
- [ ] `PATCH` sobre bloqueio de curadoria responde 422 com `CURATION_BLOCK_NOT_REMOVABLE`.
- [ ] Token com `inventory:read` mas sem `inventory:block` recebe 403 no `POST`.
