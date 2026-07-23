---
status: pending
parallelizable: false
blocked_by: []
---

<task_context>
<domain>inventory/platform/configuration</domain>
<type>configuration</type>
<scope>configuration</scope>
<complexity>high</complexity>
<dependencies>authentication,authorization,openapi</dependencies>
<unblocks>"2.0, 3.0, 7.0, 10.0"</unblocks>
</task_context>

# Tarefa 1.0: Sincronizar contrato, catálogo jurídico, configuração e permissões

## Relacionada às User Stories

- [US-02] Reutilizar políticas e definir uma política padrão (suporte)
- [US-03] Conferir dados antes do envio (suporte de segregação de função)

## Visão Geral

Estabelecer as fundações API-first e de segurança da F02: copiar o contrato soberano para o backend, configurar o catálogo jurídico versionado, registrar quatro permissões locais e validar toda configuração no startup. Esta tarefa não implementa regras de política no agregado.

## Requisitos

- A cópia do OpenAPI no backend deve ser byte a byte equivalente ao contrato do PRD.
- `ILegalPolicyCatalog` aceita somente `flexible` e `nonRefundable` e nunca recebe texto jurídico do request.
- Opções jurídicas devem possuir título, resumo e `ruleSetVersion` não vazios e falhar no startup quando inválidas.
- Registrar `commercial-offers:read`, `write`, `review` e `metrics` com negação por padrão.
- Manter autenticação LogTo, escopo `staff` e rate limiter globais existentes.

## Arquivos Envolvidos

- **Criar:**
  - `../localizestay-backend/.tasks/prd-estruturar-acomodacoes-tarifas-e-politicas/api-contract.yaml`
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/LegalPolicies/ILegalPolicyCatalog.cs`
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Infrastructure/LegalPolicies/ConfiguredLegalPolicyCatalog.cs`
  - `../localizestay-backend/tests/LocalizeStay.UnitTests/Inventory/LegalPolicyCatalogTests.cs`
- **Modificar:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/InventoryModule.cs` (Options validadas e registro da porta)
  - `../localizestay-backend/src/BuildingBlocks/LocalizeStay.SharedKernel/Security/PermissionRequirement.cs` (catálogo `CommercialOfferPermissions`)
  - `../localizestay-backend/src/BuildingBlocks/LocalizeStay.SharedKernel/Security/SecurityServiceCollectionExtensions.cs` (quatro policies)
  - `../localizestay-backend/src/LocalizeStay.Api/appsettings.json` (catálogo jurídico versionado)
  - `../localizestay-backend/tests/LocalizeStay.IntegrationTests/Inventory/SecurityAndProblemDetailsTests.cs` (negação por padrão das novas policies)
- **Referência:**
  - `tasks/prd-estruturar-acomodacoes-tarifas-e-politicas/api-contract.yaml`
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Infrastructure/Upstream/ConfiguredEligibilityValidators.cs`
  - `../localizestay-backend/src/BuildingBlocks/LocalizeStay.SharedKernel/Security/RateLimitingServiceCollectionExtensions.cs`
- **Skills para consultar durante implementação:**
  - `dotnet-dependency-config` — Options, validação no startup e DI
  - `dotnet-architecture` — porta na Application e adaptador na Infrastructure
  - `dotnet-production-readiness` — autorização, secrets e rate limiting
  - `restful-api` — contrato design-first e segurança declarada
  - `dotnet-testing` — testes de configuração e autorização

## Subtarefas

- [ ] 1.1 Copiar o contrato OpenAPI para o caminho versionado do backend e adicionar verificação de equivalência.
- [ ] 1.2 Definir `ILegalPolicyCatalog.GetCurrent(PolicyType)` e o record imutável `CommercialPolicyRuleSet`.
- [ ] 1.3 Criar Options e `ConfiguredLegalPolicyCatalog` com validação de tipos, versões e conteúdo obrigatório.
- [ ] 1.4 Registrar Options e catálogo como singleton em `InventoryModule`.
- [ ] 1.5 Adicionar o catálogo de permissões e as quatro authorization policies.
- [ ] 1.6 Cobrir configuração inválida, resolução dos dois tipos e negação sem permissão com testes automatizados.

## Sequenciamento

- Bloqueado por: Nenhum
- Desbloqueia: 2.0, 3.0, 7.0, 10.0
- Paralelizável: Não; define nomes e contratos usados pelas demais tarefas.

## Rastreabilidade

- Esta tarefa cobre: US-02 e US-03 como suporte; RF-01 e RF-05 parcialmente.
- Evidência esperada: contrato sincronizado, startup rejeitando catálogo inválido e policies resolvidas com os nomes do OpenAPI.

## Detalhes de Implementação

Manter a interface definida na TechSpec:

~~~csharp
internal interface ILegalPolicyCatalog
{
    CommercialPolicyRuleSet GetCurrent(PolicyType policyType);
}

internal sealed record CommercialPolicyRuleSet(
    PolicyType Type,
    string Title,
    string RulesSummary,
    string Version);
~~~

O catálogo deve ser determinístico, não consultar rede e não aceitar override vindo do cliente. O valor jurídico aprovado deve entrar por Options versionadas. Não registrar títulos, resumos jurídicos ou tokens em logs. Ratificar os quatro nomes de permissão antes da certificação final.

**Convenções da stack (das skills consultadas):**

- Usar constructor injection, tipos `internal` e código em inglês.
- Options devem usar `ValidateOnStart`; configuração inválida impede o processo de subir.
- Policies exigem autenticação, escopo `staff` e a permissão específica; não criar fallback permissivo.
- Logs devem usar templates estruturados e não conter textos jurídicos ou claims sensíveis.
- Testes seguem xUnit + AwesomeAssertions, AAA e naming `Method_Condition_ExpectedBehavior`.

## Critérios de Sucesso (Verificáveis)

- [ ] Contratos idênticos: `cmp tasks/prd-estruturar-acomodacoes-tarifas-e-politicas/api-contract.yaml ../localizestay-backend/.tasks/prd-estruturar-acomodacoes-tarifas-e-politicas/api-contract.yaml`
- [ ] Testes focados passam: `dotnet test ../localizestay-backend/LocalizeStay.sln --filter "FullyQualifiedName~LegalPolicyCatalogTests|FullyQualifiedName~SecurityAndProblemDetailsTests"`
- [ ] Build compila sem erros: `dotnet build ../localizestay-backend/LocalizeStay.sln --no-restore`
- [ ] Formatação válida: `dotnet format ../localizestay-backend/LocalizeStay.sln --verify-no-changes --no-restore`
- [ ] Um token sem cada permissão recebe 403 e um request anônimo recebe 401.
- [ ] Configuração sem versão/título/resumo falha no startup; os dois tipos aprovados resolvem regras imutáveis.
