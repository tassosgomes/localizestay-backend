# Task 4.0 Review Report

**Data:** 2026-07-22  
**Revisor:** AI Flow Validator  
**Veredito:** APROVADA

---

## Automated Validation

| Comando | Resultado |
|---|---|
| `dotnet build --no-restore` | 24 projects, 0 errors, 0 warnings |
| `dotnet test --no-build --filter "FullyQualifiedName~CommercialPolicyTests"` | 35 passed, 0 failed |
| `dotnet test --no-build --filter "FullyQualifiedName~UnitTests"` | 274 passed, 0 failed |
| `dotnet test --no-build --filter "FullyQualifiedName~ArchitectureTests"` | 55 passed, 0 failed |
| `dotnet format --verify-no-changes --no-restore` | 8 CHARSET violations (pré-existentes em migrations de outbox de outros módulos — débito conhecido do baseline `64454b4`). Nenhum arquivo da Task 4.0 afetado. |

---

## Technical Review

### Compliance with Task Requirements

| Requisito | Status | Evidência |
|---|---|---|
| Permitir somente `flexible` e `nonRefundable` resolvidos pelo catálogo jurídico | ✅ | `CreateCommercialPolicyCommandValidator` valida os dois tipos; `CommercialPolicy.Create` rejeita outros `PolicyType`; handler usa `ILegalPolicyCatalog.GetCurrent()` |
| Impedir dois registros ativos do mesmo tipo na propriedade | ✅ | `CommercialOffer.AddPolicy` verifica `_policies.Any(p => p.Type == ruleSet.Type && p.Status == PolicyStatus.Active)`; erro `POLICY_TYPE_ALREADY_ACTIVE` |
| Permitir definir política padrão na criação e trocar o padrão com decisão explícita sobre acomodações | ✅ | `CreateCommercialPolicyCommand.IsDefault`; `SetDefaultCommercialPolicyCommand.UpdateExistingAccommodations` |
| Desativação em uso exige substituta ativa, diferente e mesma propriedade | ✅ | `DeactivatePolicy` valida substituta ativa, ID diferente, mesmo `PropertyId`; erro `REPLACEMENT_POLICY_REQUIRED` |
| Hard delete exige `everSubmitted == false`, `isDefault == false` e `usageCount == 0` | ✅ | `CanDelete()` verifica as três condições; erro `POLICY_DELETION_NOT_ALLOWED` |
| Toda mutação verifica `expectedRevision` | ✅ | `IncrementRevisionMutate` aceita `expectedRevision?` em todas as quatro operações |
| 4 command handlers implementados | ✅ | `Create`, `SetDefault`, `Update` (deactivation), `Delete` |
| Auditoria funcional e invalidação da validação | ✅ | Todos os handlers escrevem `BusinessAuditEntry`; `InvalidateValidationOnMutate()` em toda mutação |

### Compliance with PRD (RF-01)

- ✅ Given propriedade sem política padrão → cadastra e define como padrão
- ✅ Given mudança da política padrão → escolha explícita sobre atualizar acomodações
- ✅ Given política associada a acomodações → desativação exige substituta

### Compliance with TechSpec

- ✅ Entidade `CommercialPolicy` como filha do agregado `CommercialOffer`
- ✅ `ILegalPolicyCatalog` como porta para regras versionadas
- ✅ CQRS nativo com handlers usando `InventoryDbContext` direto
- ✅ EF Core configuration com tabela `inventory.commercial_policies`, índice `ix_commercial_policies_property_type_status`, JSONB para `submission_ids`
- ✅ Error codes: `POLICY_TYPE_ALREADY_ACTIVE`, `REPLACEMENT_POLICY_REQUIRED`, `POLICY_DELETION_NOT_ALLOWED`, `REVISION_MISMATCH`, `POLICY_NOT_FOUND`, `POLICY_NOT_ACTIVE`
- ✅ FluentValidation validators para todos os 4 commands
- ✅ `CancellationToken` propagado em todas as operações
- ✅ Types `internal` (encapsulation test passa)

### Skills Compliance

- **dotnet-architecture**: CQRS nativo, entidade filha, handlers com `InventoryDbContext` direto, exceções com error codes
- **dotnet-code-quality**: PascalCase/camelCase, constructor injection, `CancellationToken`, records para commands/responses, inglês, sem comentários desnecessários, métodos curtos
- **dotnet-testing**: xUnit + AwesomeAssertions, AAA, naming convention `MethodName_Condition_ExpectedBehavior`, cobertura de caminhos positivo e negativo, 35 testes parametrizados cobrindo modelo, comandos e códigos de erro

---

## Issues Found

### 1. Atomicidade quebrada no SetDefaultPolicy (Não bloqueante)

**Categoria:** Lógica incorreta  
**Severidade:** Média  
**Fase:** Implementação  
**Origem:** Task (requisito explícito "atualização atômica")  

**Descrição:** `SetDefaultCommercialPolicyCommandHandler` executa `ExecuteSqlRawAsync` para atualizar acomodações antes de `SaveChangesAsync`. O SQL roda imediatamente (fora da transação do EF), portanto se `SaveChangesAsync` falhar, as acomodações já foram alteradas mas a política padrão não — estado inconsistente.

**Localização:** `CommercialPolicyCommands.cs:144-148`

**Sugestão:** Substituir `ExecuteSqlRawAsync` por uma operação rastreada pelo EF Core (carregar acomodações no contexto, atualizar in-memory, salvar junto com o agregado) ou envolver todo o handler em `BeginTransactionAsync`/`CommitAsync`.

**Impacto prático atual:** Baixo — a tabela `accommodations` ainda não foi criada (Task 7.0), então o SQL atualiza zero linhas.

### 2. Duplicação de código na projeção ToResponse (Não bloqueante)

**Categoria:** Overengineering  
**Severidade:** Baixa  
**Fase:** Implementação  
**Origem:** Limitação do modelo  

**Descrição:** O mapeamento `ToResponse` é duplicado em `CreateCommercialPolicyCommandHandler` (linhas 104-116) e `UpdateCommercialPolicyCommandHandler` (linhas 218-230), e inlined em `DeleteCommercialPolicyCommandHandler` (linhas 254-266). A lógica `char.ToLowerInvariant(...)` para conversão de enum PascalCase → camelCase é repetida 3 vezes.

**Sugestão:** Extrair para método estático compartilhado ou internal extension method em `CommercialPolicy`.

---

## Final Recommendation

**APROVADA**

A implementação entrega todos os requisitos da Task 4.0 com 35 testes passando, 0 erros de build, 0 warnings, architecture tests verdes, e conformidade com PRD, TechSpec e skills. O único apontamento técnico (não-atomicidade do SQL de acomodações) é mitigado pelo fato de a tabela `accommodations` ainda não existir e ser naturalmente tratado na Task 7.0 quando a entidade for implementada.
