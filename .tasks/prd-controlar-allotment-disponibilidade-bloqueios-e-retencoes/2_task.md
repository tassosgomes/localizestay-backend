---
status: pending
parallelizable: true
blocked_by: []
---

<task_context>
<domain>infra/shared-kernel/security</domain>
<type>configuration</type>
<scope>configuration</scope>
<complexity>low</complexity>
<dependencies>http_server</dependencies>
<unblocks>"27.0, 28.0, 29.0, 30.0, 31.0, 34.0, 43.0"</unblocks>
<vertical_slice>As cinco permissões inventory:* existem como policies nomeadas e podem ser referenciadas por qualquer endpoint da F03.</vertical_slice>
</task_context>

# Tarefa 2.0: Declarar as cinco permissões `inventory:*` e suas policies

## Relacionada às User Stories

- [US-01] Registrar allotment (suporte — acesso restrito à equipe interna autorizada)
- [US-03] Bloquear datas (suporte — ação de bloqueio tem permissão própria)

## Visão Geral

O contrato da F03 declara cinco permissões distintas em `x-required-permissions`: `inventory:read`, `inventory:write`, `inventory:block`, `inventory:hold` e `inventory:metrics`. Elas separam consulta, manutenção de allotment, ação de bloqueio, ciclo de checkout e indicadores.

A tarefa registra o catálogo e as policies, sem tocar em endpoint algum. Os endpoints as consomem a partir da Fase 5.

## Requisitos

- Criar o catálogo `InventoryControlPermissions` com as cinco constantes, seguindo a convenção `<recurso-kebab>:<ação>` já usada por `PortfolioOnboardingPermissions` e `CommercialOfferPermissions`.
- Registrar uma policy por permissão em `SecurityServiceCollectionExtensions`, no mesmo formato das policies existentes.
- **Não replicar a hierarquia embutida** que faz `commercial-offers:write` conceder `commercial-offers:read`. `inventory:write` **não** concede `inventory:read`. Composição de acesso pertence à role no LogTo.
- Documentar no XML doc do catálogo que a permissão cobre **controle de inventário** (allotment, bloqueios, retenções) e não as capacidades de F01/F02, que também vivem no módulo `Inventory`.

## Arquivos Envolvidos

- **Criar:**
  - `../localizestay-backend/tests/LocalizeStay.UnitTests/Inventory/InventoryControlPermissionsTests.cs`
- **Modificar:**
  - `../localizestay-backend/src/BuildingBlocks/LocalizeStay.SharedKernel/Security/PermissionRequirement.cs` (catálogo `InventoryControlPermissions`)
  - `../localizestay-backend/src/BuildingBlocks/LocalizeStay.SharedKernel/Security/SecurityServiceCollectionExtensions.cs` (cinco policies)
- **Referência:**
  - `.tasks/prd-controlar-allotment-disponibilidade-bloqueios-e-retencoes/api-contract.yaml` (`x-required-permissions` por operação)
  - `../localizestay-backend/src/BuildingBlocks/LocalizeStay.SharedKernel/Security/LogToOptions.cs` (claim de permissão e escopo `staff`)
- **Skills para consultar durante implementação:**
  - `common-roles-naming` — kebab-case para permissões finas, catálogo oficial
  - `restful-api` — mapeamento operação → permissão declarada no contrato
  - `dotnet-code-quality` — constantes em PascalCase, nomes em inglês

## Subtarefas

- [ ] 2.1 Declarar `InventoryControlPermissions` com `Read`, `Write`, `Block`, `Hold` e `Metrics`, com XML doc explicando o escopo e a ausência deliberada de hierarquia.
- [ ] 2.2 Registrar as cinco policies em `SecurityServiceCollectionExtensions`, exigindo autenticação e o `PermissionRequirement` correspondente.
- [ ] 2.3 Testar: cada policy existe e resolve; um principal com `inventory:write` **não** satisfaz `inventory:read`; principal sem escopo `staff` falha em todas.

## Sequenciamento

- Bloqueado por: Nenhum
- Desbloqueia: 27.0, 28.0, 29.0, 30.0, 31.0, 34.0, 43.0
- Paralelizável: Sim; toca somente o SharedKernel de segurança.

## Rastreabilidade

- Esta tarefa cobre: a restrição de acesso do PRD ("acesso restrito à equipe interna autorizada") e as cinco permissões declarativas do contrato.
- Evidência esperada: `InventoryControlPermissionsTests` prova a ausência de hierarquia; 34.0 certifica 401 e 403 fim a fim.

## Detalhes de Implementação

Mapa operação → permissão, conforme o contrato:

| Permissão | Operações |
|---|---|
| `inventory:read` | `getPropertySellability`, `getInventoryCalendar`, `getDailyInventoryDetail`, `listAllotments`, `getAllotment`, `listInventoryBlocks`, `getInventoryBlock`, `listInventoryRequests`, `getInventoryRequest`, `getInventoryHold` |
| `inventory:write` | `createAllotment`, `updateAllotment`, `cancelAllotment`, `createInventoryRequest`, `updateInventoryRequest` |
| `inventory:block` | `createInventoryBlock`, `previewInventoryBlockImpact`, `removeInventoryBlock` |
| `inventory:hold` | `createInventoryHold`, `releaseInventoryHold`, `commitInventoryHold` |
| `inventory:metrics` | `getInventoryMetrics` |

`getAvailability` é público e não usa policy alguma.

Racional do ADR: hierarquia embutida no handler é invisível em revisão de segurança e cresce sem controle. Um operador que precisa ler e escrever recebe as duas permissões via role no LogTo.

**Convenções da stack (das skills consultadas):**

- Permissões finas em kebab-case, roles em SCREAMING_SNAKE_CASE (`common-roles-naming`).
- Endpoints referenciam a policy pelo nome da constante, nunca por string literal.
- Testes AAA com xUnit + AwesomeAssertions (`dotnet-testing`).

## Critérios de Sucesso (Verificáveis)

- [ ] Testes passam: `dotnet test ../localizestay-backend/LocalizeStay.sln --filter "FullyQualifiedName~InventoryControlPermissionsTests"`
- [ ] Build compila sem erros: `dotnet build ../localizestay-backend/LocalizeStay.sln --no-restore`
- [ ] Formatação válida: `dotnet format ../localizestay-backend/LocalizeStay.sln --verify-no-changes --no-restore`
- [ ] As cinco policies são resolvíveis por nome a partir do `IAuthorizationPolicyProvider`.
- [ ] Principal com `inventory:write` falha na policy `inventory:read`.
- [ ] Nenhuma policy existente da F01/F02 muda de comportamento: `dotnet test ../localizestay-backend/tests/LocalizeStay.IntegrationTests --filter "FullyQualifiedName~CommercialOfferSecurityTests"`
