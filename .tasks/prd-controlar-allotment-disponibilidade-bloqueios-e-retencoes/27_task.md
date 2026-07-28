---
status: pending
parallelizable: true
blocked_by: ["2.0", "23.0", "24.0"]
---

<task_context>
<domain>inventory/endpoints/availability</domain>
<type>implementation</type>
<scope>core_feature</scope>
<complexity>medium</complexity>
<dependencies>http_server</dependencies>
<unblocks>"33.0, 34.0"</unblocks>
<vertical_slice>As quatro operações de leitura de saldo ficam acessíveis por HTTP — availability pública e anônima, o resto com inventory:read.</vertical_slice>
</task_context>

# Tarefa 27.0: Expor os endpoints de disponibilidade, vendabilidade e calendário

## Relacionada às User Stories

- [US-02] Enxergar o calendário e diagnosticar a data (cobertura direta)

## Visão Geral

Quatro operações Minimal API: `getAvailability` (**pública**), `getPropertySellability`, `getInventoryCalendar` e `getDailyInventoryDetail`.

`getAvailability` é a **primeira superfície anônima da plataforma**. ADR-005 decide que seu rate limiting é inteiramente responsabilidade da borda: no backend, a única mudança é `.AllowAnonymous()` junto de `.DisableRateLimiting()`. `Program.cs`, `UseForwardedHeaders` e `RateLimitOptions` ficam **fora do escopo**.

## Requisitos

- `getAvailability`: `.AllowAnonymous()` **e** `.DisableRateLimiting()`. Sem policy, sem token.
- As outras três exigem a policy `inventory:read`.
- O ramo autenticado do limiter permanece **inalterado** — as demais 22 operações seguem particionadas por `sub`, com os limites já certificados por F01 e F02.
- Paths, verbos, `operationId`, parâmetros e status conforme o `api-contract.yaml`, que é a fonte soberana.
- Registro de um `MapXEndpoints()` por arquivo em `InventoryEndpoints.cs` — uma linha por tarefa de endpoint.
- Ator vem do JWT, nunca do corpo ou da query.
- Erros traduzidos pelo `GlobalExceptionHandler` existente; nenhum `try/catch` no endpoint.

## Arquivos Envolvidos

- **Criar:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Endpoints/AvailabilityEndpoints.cs`
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Endpoints/InventoryCalendarEndpoints.cs`
  - `../localizestay-backend/tests/LocalizeStay.IntegrationTests/Inventory/AvailabilityEndpointsTests.cs`
- **Modificar:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Endpoints/InventoryEndpoints.cs` (duas linhas de registro)
- **Referência:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Endpoints/CommercialOfferEndpoints.cs` (padrão de Minimal API do módulo)
  - `.tasks/prd-controlar-allotment-disponibilidade-bloqueios-e-retencoes/adrs/adr-005.md`
  - `../localizestay-backend/src/BuildingBlocks/LocalizeStay.SharedKernel/Security/RateLimitingServiceCollectionExtensions.cs`
  - `../localizestay-backend/tests/LocalizeStay.IntegrationTests/Infrastructure/LocalizeStayWebApplicationFactory.cs`
- **Skills para consultar durante implementação:**
  - `restful-api` — versionamento em path, status por operação, Problem Details
  - `dotnet-architecture` — Minimal API delegando ao `IDispatcher`
  - `dotnet-testing` — `WebApplicationFactory` + Testcontainers

## Subtarefas

- [ ] 27.1 Criar `AvailabilityEndpoints` com `getAvailability` anônimo e isento do limiter, e `getPropertySellability` com `inventory:read`.
- [ ] 27.2 Criar `InventoryCalendarEndpoints` com `getInventoryCalendar` e `getDailyInventoryDetail`, ambos com `inventory:read`.
- [ ] 27.3 Registrar os dois grupos em `InventoryEndpoints.cs`.
- [ ] 27.4 Testar por HTTP: `/availability` sem token responde 200; as outras três sem token respondem 401; a composição interna não aparece na resposta pública.

## Sequenciamento

- Bloqueado por: 2.0, 23.0, 24.0
- Desbloqueia: 33.0, 34.0
- Paralelizável: Sim; cria arquivos de endpoint disjuntos das tarefas 28.0 a 31.0. Cada uma acrescenta apenas suas linhas de registro em `InventoryEndpoints.cs` — edição trivialmente mesclável.

## Rastreabilidade

- Esta tarefa cobre: RF-03 e RF-04 na superfície HTTP, e ADR-005.
- Evidência esperada: `AvailabilityEndpointsTests` prova o acesso anônimo, a isenção do limiter e o não vazamento da composição interna.

## Detalhes de Implementação

```csharp
endpoints.MapGet("/api/v1/availability", GetAvailabilityAsync)
    .WithName("getAvailability")
    .AllowAnonymous()
    .DisableRateLimiting();   // ADR-005: controle por cliente é da borda
```

Mapa das quatro operações:

| Operação | Path | Permissão |
|---|---|---|
| `getAvailability` | `GET /api/v1/availability` | **pública** |
| `getPropertySellability` | `GET /api/v1/properties/{propertyId}/sellability` | `inventory:read` |
| `getInventoryCalendar` | `GET /api/v1/properties/{propertyId}/inventory-calendar` | `inventory:read` |
| `getDailyInventoryDetail` | `GET /api/v1/properties/{propertyId}/accommodations/{accommodationId}/daily-inventory/{date}` | `inventory:read` |

> **Por que `.DisableRateLimiting()` e não um limite maior:** o limiter atual recolhe todo tráfego não autenticado em uma partição literal `"anonymous"` com balde único. Submeter a consulta pública de D01 a ela criaria um gargalo global compartilhado, não uma proteção. Particionar por IP dentro da aplicação exigiria `UseForwardedHeaders` como primeira chamada do pipeline do host inteiro — mudança desproporcional por uma rota. A borda (Cloudflare + Traefik) já conhece o IP real e barra o abuso antes de consumir thread ou conexão.

**Consequência aceita e registrada:** em desenvolvimento local e em testes de integração **não há rate limiting algum** nessa rota, e o `429` declarado no contrato vem da borda, sem formato RFC 9457. O teste de contrato (33.0) registra isso como exceção conhecida.

**Convenções da stack (das skills consultadas):**

- Endpoints delegam ao `IDispatcher` e não contêm regra de negócio (`dotnet-architecture`).
- `operationId` do contrato vira `.WithName(...)`; `Produces` declara os status (`restful-api`).
- Policies referenciadas pela constante do catálogo, nunca por string literal (`dotnet-production-readiness`).
- Testes de integração com `WebApplicationFactory` e Testcontainers PostgreSQL (`dotnet-testing`).

## Critérios de Sucesso (Verificáveis)

- [ ] Testes passam: `dotnet test ../localizestay-backend/tests/LocalizeStay.IntegrationTests --filter "FullyQualifiedName~AvailabilityEndpointsTests"`
- [ ] Build compila sem erros: `dotnet build ../localizestay-backend/LocalizeStay.sln --no-restore`
- [ ] Formatação válida: `dotnet format ../localizestay-backend/LocalizeStay.sln --verify-no-changes --no-restore`
- [ ] `GET /api/v1/availability` **sem token** responde 200.
- [ ] `GET /api/v1/properties/{id}/sellability` sem token responde 401; com token sem `inventory:read`, 403.
- [ ] A resposta de `/availability` não contém `committedUnits`, `heldUnits`, `blockedUnits` nem `blocks`.
- [ ] O endpoint público declara `.DisableRateLimiting()`.
- [ ] Nenhuma alteração em `Program.cs` nem em `RateLimitingServiceCollectionExtensions.cs`.
- [ ] Calendário de 93 dias responde 422 com `code = DATE_RANGE_TOO_LARGE`.
