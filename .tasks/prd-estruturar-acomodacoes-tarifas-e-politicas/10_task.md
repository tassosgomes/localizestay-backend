---
status: pending
parallelizable: false
blocked_by: ["8.0", "9.0"]
---

<task_context>
<domain>inventory/endpoints/commercial-offers</domain>
<type>integration</type>
<scope>middleware</scope>
<complexity>high</complexity>
<dependencies>http_server</dependencies>
<unblocks>"12.0"</unblocks>
</task_context>

# Tarefa 10.0: Expor e validar as 20 operações Minimal API

## Relacionada às User Stories

- [US-01] Operar acomodações e tarifas progressivamente (direta)
- [US-02] Operar políticas reutilizáveis (direta)
- [US-03] Validar e enviar a oferta (direta)
- [US-05] Consultar métricas (direta)

## Visão Geral

Mapear Commands e Queries para as 20 operações do OpenAPI com Minimal APIs, DTOs HTTP internos, FluentValidation, autenticação/autorização, metadados de contrato e RFC 9457. A tarefa deve preservar exatamente paths, verbos, operationIds, status e headers declarados.

## Requisitos

- Expor exatamente os 20 `operationId` do contrato sob `/api/v1`.
- Derivar atores do JWT; nunca aceitar autor/revisor no body.
- Aplicar as permissões `read`, `write`, `review` e `metrics` por operação.
- Retornar `201 + Location` para criações e `204` sem body para deletes.
- Diferenciar 400 sintático, 404 ausente, 409 concorrência/duplicidade e 422 regra de negócio.
- Propagar RFC 9457 com `code` e `traceId` sem detalhes sensíveis.
- Reutilizar rate limiter global e expor 429 conforme contrato.

## Arquivos Envolvidos

- **Criar:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/CommercialOffers/CommercialOfferValidators.cs`
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Endpoints/CommercialOfferEndpoints.cs`
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Endpoints/CommercialPolicyEndpoints.cs`
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Endpoints/AccommodationEndpoints.cs`
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Endpoints/CommercialRateEndpoints.cs`
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Endpoints/CommercialOfferWorkflowEndpoints.cs`
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Endpoints/CommercialOfferMetricsEndpoints.cs`
  - `../localizestay-backend/tests/LocalizeStay.IntegrationTests/Inventory/CommercialOfferEndpointsTests.cs`
- **Modificar:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/InventoryModule.cs` (registrar catálogo/handlers e mapear endpoints)
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/CommercialOffers/CommercialOfferCommands.cs` (criação idempotente do draft, se ainda não concluída)
- **Referência:**
  - `tasks/prd-estruturar-acomodacoes-tarifas-e-politicas/api-contract.yaml`
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Endpoints/PartnerEndpoints.cs`
  - `../localizestay-backend/src/BuildingBlocks/LocalizeStay.SharedKernel/ErrorHandling/GlobalExceptionHandler.cs`
  - `../localizestay-backend/src/BuildingBlocks/LocalizeStay.SharedKernel/Security/PermissionRequirement.cs`
- **Skills para consultar durante implementação:**
  - `dotnet-architecture` — Minimal API → Dispatcher → CQRS
  - `restful-api` — paths, status, paginação e Problem Details
  - `dotnet-code-quality` — DTOs, async, null handling e validação
  - `dotnet-production-readiness` — autenticação, autorização e rate limiting
  - `dotnet-testing` — WebApplicationFactory e testes HTTP reais

## Subtarefas

- [ ] 10.1 Criar validators para todos os Commands/Queries e códigos de campo do contrato.
- [ ] 10.2 Mapear fila/detalhe em `CommercialOfferEndpoints`.
- [ ] 10.3 Mapear cinco operações de política em `CommercialPolicyEndpoints`.
- [ ] 10.4 Mapear cinco operações de acomodação em `AccommodationEndpoints`.
- [ ] 10.5 Mapear quatro operações de tarifa em `CommercialRateEndpoints`.
- [ ] 10.6 Mapear validação, submissão e histórico em `CommercialOfferWorkflowEndpoints`.
- [ ] 10.7 Mapear métricas e aplicar a policy `commercial-offers:metrics`.
- [ ] 10.8 Registrar todos os endpoints no módulo e metadados `WithName`/`WithContractResponses`.
- [ ] 10.9 Criar testes HTTP focados em happy path, PATCH null/omitido, `Location`, 204 e erros reais.

## Sequenciamento

- Bloqueado por: 8.0 e 9.0
- Desbloqueia: 12.0
- Paralelizável: Não; integra todas as fatias em uma superfície única e deve evitar colisões de rotas/DTOs.

## Rastreabilidade

- Esta tarefa cobre: US-01, US-02, US-03 e US-05 diretamente; RF-01 a RF-06.
- Evidência esperada: os 20 endpoints existem com método/path/nome/policies/responses coerentes com o YAML.

## Detalhes de Implementação

Mapeamento obrigatório:

| Arquivo | Operações |
|---|---|
| `CommercialOfferEndpoints.cs` | `listCommercialOffers`, `getCommercialOffer` |
| `CommercialPolicyEndpoints.cs` | `listCommercialPolicies`, `createCommercialPolicy`, `setDefaultCommercialPolicy`, `updateCommercialPolicy`, `deleteCommercialPolicy` |
| `AccommodationEndpoints.cs` | `listAccommodations`, `createAccommodation`, `getAccommodation`, `updateAccommodation`, `deleteAccommodation` |
| `CommercialRateEndpoints.cs` | `listCommercialRates`, `createCommercialRate`, `updateCommercialRate`, `deleteCommercialRate` |
| `CommercialOfferWorkflowEndpoints.cs` | `createCommercialOfferValidation`, `createCommercialOfferSubmission`, `listCommercialOfferHistory` |
| `CommercialOfferMetricsEndpoints.cs` | `getCommercialOfferMetrics` |

Para PATCH, usar presença explícita de propriedades para distinguir omitido de `null`. Todos os métodos async recebem `CancellationToken` e despacham um único Command/Query. Reutilizar `WithContractResponses` e o `GlobalExceptionHandler` existentes.

**Convenções da stack (das skills consultadas):**

- Endpoints apenas mapeiam HTTP/claims para Commands/Queries; não contêm regra de negócio.
- Requests/responses são records internos e mapeamento é manual.
- `201` sempre inclui `Location`; `204` não serializa `null`.
- FluentValidation gera 400 para shape/input; exceções de domínio mapeiam 409/422.
- Testes usam WebApplicationFactory + PostgreSQL Testcontainers e requests HTTP reais.

## Critérios de Sucesso (Verificáveis)

- [ ] Testes focados passam: `dotnet test ../localizestay-backend/LocalizeStay.sln --filter "FullyQualifiedName~CommercialOfferEndpointsTests"`
- [ ] Build compila sem erros: `dotnet build ../localizestay-backend/LocalizeStay.sln --no-restore`
- [ ] Formatação válida: `dotnet format ../localizestay-backend/LocalizeStay.sln --verify-no-changes --no-restore`
- [ ] Metadados expõem exatamente 20 operationIds, todos únicos.
- [ ] POST de política/acomodação/tarifa/validação/submissão retorna 201 com `Location`.
- [ ] DELETE de política/acomodação/tarifa retorna 204 com corpo vazio.
- [ ] Requests anônimos retornam 401; token sem permissão retorna 403; excesso retorna 429.
- [ ] Erros 400/404/409/422/500 usam `application/problem+json`, `code` e `traceId`.

