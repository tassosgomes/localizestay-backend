# Review — Tarefa 2.0: Declarar as cinco permissões `inventory:*` e suas policies

Data: 2026-07-27 | Iteração: 1 | Modo: full

## 1. Gate Determinístico

Comando:

```bash
scripts/ai-flow/gate.sh --filter="FullyQualifiedName~InventoryControlPermissionsTests" --filter="FullyQualifiedName~CommercialOfferSecurityTests"
```

Output (verbatim):

```text
GATE: APROVADO
arquivos alterados: 3 (.cs: 3)
format: ok (3 arquivos)
build: ok 0 Warning(s) 0 Error(s) 
testes: ok (FullyQualifiedName~InventoryControlPermissionsTests=16 FullyQualifiedName~CommercialOfferSecurityTests=12)
```

Resultado: **APROVADO** (format, build, 16 testes da task + 12 testes de regressão F02).

## 2. Revisão Semântica

Escopo: `PermissionRequirement.cs` (catálogo), `SecurityServiceCollectionExtensions.cs` (policies), `InventoryControlPermissionsTests.cs` (novo). Skills consultadas: `roles-naming`, `restful-api`, `dotnet-code-quality`.

Verificações:

- **Catálogo (2.1):** as cinco constantes `Read`, `Write`, `Block`, `Hold`, `Metrics` em PascalCase com valores kebab-case `inventory:*`, seguindo a convenção dos catálogos `PortfolioOnboardingPermissions`/`CommercialOfferPermissions`. XML doc documenta explicitamente o escopo de controle de inventário (allotment, bloqueios, retenções) vs. capacidades F01/F02 e a ausência deliberada de hierarquia, com o racional do ADR. ✔
- **Policies (2.2):** cinco policies registradas em `SecurityServiceCollectionExtensions.cs` no formato idêntico às existentes (mesmo `AuthenticationScheme`, `RequireAuthenticatedUser()`, `PermissionRequirement` correspondente). ✔
- **Ausência de hierarquia:** o `PermissionHandler.HasPermission` mantém o caso especial apenas para `commercial-offers:write → read`; nenhum acoplamento entre `inventory:write` e `inventory:read`. Composição de acesso fica na role do LogTo, conforme requisito. ✔
- **Contrato:** as cinco permissões existem em `x-required-permissions` no `api-contract.yaml` (28 ocorrências), coerentes com o mapa operação → permissão da task. ✔
- **Testes (2.3):** 16 testes cobrem (a) resolução de cada policy pelo `IAuthorizationPolicyProvider` com exigência de autenticação (`DenyAnonymousAuthorizationRequirement`), esquema e `PermissionRequirement` correto; (b) sucesso com escopo `staff` + permissão correspondente; (c) `inventory:write` **não** satisfaz `inventory:read` (invariante central da task, devidamente asserido); (d) principal sem escopo `staff` falha em todas as cinco. xUnit + AwesomeAssertions, padrão AAA. ✔
- **Regressão F01/F02:** `CommercialOfferSecurityTests` (12) passa no gate. ✔

## 3. Achados

### Bloqueantes

Nenhum.

### Observações (não bloqueantes)

1. **Localização do catálogo** — `InventoryControlPermissions` foi criado em `PermissionRequirement.cs`, enquanto os catálogos irmãos (`PortfolioOnboardingPermissions`, `CommercialOfferPermissions`) vivem em `LogToOptions.cs`. O task file mandava explicitamente o arquivo, então o implementer cumpriu a instrução; fica o registro para eventual consolidação futura dos catálogos num único arquivo. Origem: task file; severidade baixa.

## 4. Recomendação Final

**APROVADA**
