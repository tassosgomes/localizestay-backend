---
status: pending
parallelizable: true
blocked_by: ["33.0", "34.0", "43.0"]
---

<task_context>
<domain>inventory/testing/contract</domain>
<type>testing</type>
<scope>core_feature</scope>
<complexity>medium</complexity>
<dependencies>http_server</dependencies>
<unblocks>"50.0"</unblocks>
<vertical_slice>As quatro operações da Onda B são provadas conformes ao contrato e protegidas pela permissão inventory:hold.</vertical_slice>
</task_context>

# Tarefa 49.0: Certificar o contrato e a segurança da Onda B

## Relacionada às User Stories

- [US-04] Acomodação separada durante o checkout (suporte — a certificação prova que a superfície entregue é a acordada com D03)

## Visão Geral

Fecha a certificação formal: as 23 operações do contrato passam a estar cobertas, e a matriz de permissões passa a incluir `inventory:hold`.

Estende os dois arquivos criados nas tarefas 33.0 e 34.0, em vez de criar uma segunda suíte paralela.

## Requisitos

- As quatro operações da Onda B presentes com método, path e `operationId` do contrato, totalizando **23**.
- Status declarados por operação exercitados, incluindo `409 HOLD_ALREADY_COMMITTED` e `422 COMMITMENT_WITHOUT_AVAILABILITY`.
- `Idempotency-Key` obrigatório em `createInventoryHold` e `commitInventoryHold`.
- `metadata` presente em `INSUFFICIENT_AVAILABILITY`, completando os três códigos exigidos pela TechSpec.
- `Location` no `201` de `createInventoryHold`; corpo ausente no `204` de `releaseInventoryHold`.
- Matriz de segurança estendida: as três operações de `inventory:hold` respondem 401 sem token e 403 com token sem a permissão; `getInventoryHold` exige `inventory:read`.
- Ausência de hierarquia também para `inventory:hold`: quem tem `hold` não lê.
- Nenhuma resposta da Onda B contém dado do viajante.

## Arquivos Envolvidos

- **Modificar:**
  - `../localizestay-backend/tests/LocalizeStay.IntegrationTests/Inventory/InventoryContractTests.cs` (as quatro operações da Onda B)
  - `../localizestay-backend/tests/LocalizeStay.IntegrationTests/Inventory/InventorySecurityTests.cs` (matriz com `inventory:hold`)
- **Referência:**
  - `.tasks/prd-controlar-allotment-disponibilidade-bloqueios-e-retencoes/api-contract.yaml` (fonte soberana)
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Endpoints/InventoryHoldEndpoints.cs` (criado em 43.0)
- **Skills para consultar durante implementação:**
  - `restful-api` — OpenAPI 3.1, status por operação, RFC 9457
  - `dotnet-production-readiness` — menor privilégio por operação
  - `dotnet-testing` — `[Theory]` com `[MemberData]` para estender a matriz

## Subtarefas

- [ ] 49.1 Estender `InventoryContractTests` com as quatro operações da Onda B, elevando a contagem total para 23.
- [ ] 49.2 Exercitar os status da Onda B, `Idempotency-Key` obrigatório, `Location` no 201, corpo vazio no 204 e `metadata` em `INSUFFICIENT_AVAILABILITY`.
- [ ] 49.3 Estender `InventorySecurityTests` com `inventory:hold`, incluindo a prova de ausência de hierarquia.

## Sequenciamento

- Bloqueado por: 33.0, 34.0, 43.0
- Desbloqueia: 50.0
- Paralelizável: Sim; roda em paralelo às demais tarefas da Fase 9.

## Rastreabilidade

- Esta tarefa cobre: a seção Testes de Contrato da TechSpec, na parte da Onda B, e a certificação de `inventory:hold`.
- Evidência esperada: `InventoryContractTests` cobrindo 23 operações e `InventorySecurityTests` com a matriz completa das cinco permissões.

## Detalhes de Implementação

As quatro operações da Onda B:

| Operação | Permissão | Header | Sucesso |
|---|---|---|---:|
| `createInventoryHold` | `inventory:hold` | `Idempotency-Key` | 201 + `Location` |
| `getInventoryHold` | `inventory:read` | — | 200 |
| `releaseInventoryHold` | `inventory:hold` | — | 204 |
| `commitInventoryHold` | `inventory:hold` | `Idempotency-Key` | 201 |

Casos de ausência de hierarquia a acrescentar:

```
token{inventory:hold} → GET  /inventory-holds/{id}          ==> 403
token{inventory:read} → POST /inventory-holds               ==> 403
token{inventory:read} → POST /inventory-holds/{id}/commitment ==> 403
```

Estado final da certificação de contrato:

| | Onda A | Onda B | Total |
|---|---:|---:|---:|
| Operações | 19 | 4 | **23** |
| Exceções conhecidas | 1 (`getAvailability` + `429`, ADR-005) | 0 | 1 |
| Códigos com `metadata` | 2 | 1 | **3** |

> Os três códigos com `metadata` — `ALLOTMENT_BELOW_COMMITTED`, `INSUFFICIENT_FREE_BALANCE` e `INSUFFICIENT_AVAILABILITY` — são exatamente os que a TechSpec exige, e são a razão pela qual a tarefa 1.0 existe.

**Convenções da stack (das skills consultadas):**

- O contrato é a fonte soberana; o teste falha quando a implementação diverge (`restful-api`).
- Estender a matriz existente com `[MemberData]`, não copiar testes (`dotnet-testing`).
- 401 sempre distinto de 403 (`restful-api`).

## Critérios de Sucesso (Verificáveis)

- [ ] Testes passam: `dotnet test ../localizestay-backend/tests/LocalizeStay.IntegrationTests --filter "FullyQualifiedName~InventoryContractTests"`
- [ ] Testes passam: `dotnet test ../localizestay-backend/tests/LocalizeStay.IntegrationTests --filter "FullyQualifiedName~InventorySecurityTests"`
- [ ] Build compila sem erros: `dotnet build ../localizestay-backend/LocalizeStay.sln --no-restore`
- [ ] O teste de contrato cobre **23** operações.
- [ ] `createInventoryHold` e `commitInventoryHold` sem `Idempotency-Key` respondem 400.
- [ ] `INSUFFICIENT_AVAILABILITY` traz `metadata.unavailableDates`.
- [ ] `releaseInventoryHold` responde 204 com corpo vazio.
- [ ] Token com `inventory:hold` recebe 403 em `GET /inventory-holds/{id}`.
- [ ] Nenhuma resposta da Onda B contém dado do viajante.
