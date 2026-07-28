---
status: pending
parallelizable: true
blocked_by: ["2.0", "27.0", "28.0", "29.0", "30.0", "31.0"]
---

<task_context>
<domain>inventory/testing/security</domain>
<type>testing</type>
<scope>core_feature</scope>
<complexity>medium</complexity>
<dependencies>http_server</dependencies>
<unblocks>"49.0"</unblocks>
<vertical_slice>Cada operação exige exatamente a permissão declarada, o endpoint público é alcançável sem token, e nenhuma resposta pública revela a composição interna do saldo.</vertical_slice>
</task_context>

# Tarefa 34.0: Certificar permissões, endpoint anônimo e não vazamento do saldo

## Relacionada às User Stories

- [US-01], [US-03] (suporte — acesso restrito à equipe interna autorizada é requisito do PRD)
- [US-04] (suporte — a resposta pública não pode revelar concorrência de checkout)

## Visão Geral

Duas garantias de segurança em um único teste: o **menor privilégio por operação** e a **não exposição da composição interna do saldo**.

A segunda é a mais sutil. A composição — comprometido, retido, bloqueado e o motivo de cada bloqueio — é informação operacional do parceiro e sinal de concorrência de checkout. Ela é legítima no backoffice autenticado e proibida na vitrine.

## Requisitos

- Matriz completa: cada uma das 18 operações autenticadas responde 401 sem token e 403 com token que não carrega a permissão declarada.
- `inventory:write` **não** concede `inventory:read`; a matriz precisa provar isso explicitamente.
- `getAvailability` responde 200 **sem token**.
- `getAvailability` está isento do rate limiter da aplicação.
- A resposta de `getAvailability` não contém `committedUnits`, `heldUnits`, `blockedUnits`, `blocks` nem motivo de bloqueio.
- `unavailabilityReason` só assume valores genéricos declarados no contrato.
- Token sem escopo `staff` recebe 403 em todas as operações autenticadas.
- Nenhuma resposta da F03 contém dado de viajante.

## Arquivos Envolvidos

- **Criar:**
  - `../localizestay-backend/tests/LocalizeStay.IntegrationTests/Inventory/InventorySecurityTests.cs`
- **Referência:**
  - `../localizestay-backend/tests/LocalizeStay.IntegrationTests/Inventory/CommercialOfferSecurityTests.cs` (padrão de teste de segurança da F02)
  - `../localizestay-backend/tests/LocalizeStay.IntegrationTests/Infrastructure/LocalizeStayWebApplicationFactory.cs` (emissão de JWT de teste)
  - `.tasks/prd-controlar-allotment-disponibilidade-bloqueios-e-retencoes/api-contract.yaml` (`x-required-permissions`)
- **Skills para consultar durante implementação:**
  - `dotnet-production-readiness` — menor privilégio, negação por padrão, sanitização
  - `restful-api` — distinção entre 401 e 403
  - `dotnet-testing` — `[Theory]` com `[MemberData]` para a matriz operação × permissão

## Subtarefas

- [ ] 34.1 Montar a matriz operação × permissão a partir de `x-required-permissions` e exercitar 401 e 403 para as 18 autenticadas.
- [ ] 34.2 Provar a ausência de hierarquia: token com `inventory:write` recebe 403 em toda operação que exige `inventory:read`, e vice-versa.
- [ ] 34.3 Provar que `getAvailability` responde 200 sem token e está isento do limiter.
- [ ] 34.4 Provar que nenhuma resposta pública contém composição interna do saldo, motivo de bloqueio ou dado de viajante.

## Sequenciamento

- Bloqueado por: 2.0, 27.0, 28.0, 29.0, 30.0, 31.0
- Desbloqueia: 49.0
- Paralelizável: Sim; roda em paralelo às demais tarefas da Fase 6.

## Rastreabilidade

- Esta tarefa cobre: as restrições de acesso do PRD, as cinco permissões do contrato e o requisito especial de que `GET /availability` não exponha composição interna nem dado do viajante.
- Evidência esperada: `InventorySecurityTests` com a matriz completa verde.

## Detalhes de Implementação

Matriz de permissões, derivada do contrato:

| Permissão | Operações que a exigem |
|---|---:|
| `inventory:read` | 10 |
| `inventory:write` | 5 |
| `inventory:block` | 3 |
| `inventory:metrics` | 1 |
| pública | 1 (`getAvailability`) |

Casos que provam a ausência de hierarquia:

```
token{inventory:write} → GET  /allotments             ==> 403
token{inventory:read}  → POST /allotments             ==> 403
token{inventory:read}  → POST /inventory-blocks       ==> 403
token{inventory:block} → GET  /inventory-blocks       ==> 403
token{inventory:read}  → GET  /inventory-metrics      ==> 403
```

> Isso é deliberado e diverge do caso especial existente para `commercial-offers`, onde `write` concede `read`. Composição de acesso pertence à role no LogTo, onde é visível em revisão de segurança. Hierarquia embutida no handler cresce sem controle e é invisível.

Campos proibidos na resposta de `getAvailability`:

```
committedUnits   heldUnits   blockedUnits   blocks
reason           reasonNote  holds          commitments
```

**Convenções da stack (das skills consultadas):**

- 401 (sem token) sempre distinto de 403 (token sem permissão) (`restful-api`).
- Negação por padrão como guardrail explícito (`dotnet-production-readiness`).
- Matriz como `[Theory]` com `[MemberData]`, não como testes copiados (`dotnet-testing`).

## Critérios de Sucesso (Verificáveis)

- [ ] Testes passam: `dotnet test ../localizestay-backend/tests/LocalizeStay.IntegrationTests --filter "FullyQualifiedName~InventorySecurityTests"`
- [ ] Build compila sem erros: `dotnet build ../localizestay-backend/LocalizeStay.sln --no-restore`
- [ ] Todas as 18 operações autenticadas respondem 401 sem token.
- [ ] Todas respondem 403 com token que não carrega a permissão declarada.
- [ ] Token com `inventory:write` recebe 403 em toda operação de leitura.
- [ ] `GET /api/v1/availability` responde 200 sem token.
- [ ] A resposta de `/availability` não contém nenhum dos oito campos proibidos.
- [ ] Token sem escopo `staff` recebe 403 em todas as operações autenticadas.
- [ ] Nenhuma resposta da F03 contém nome, documento, e-mail ou telefone de viajante.
