# Task 5.0 Review Report

**Data:** 2026-07-22  
**Revisor:** AI Flow Validator  
**Veredito:** VALIDAÇÃO APROVADA

---

## Automated Validation

| Comando | Resultado |
|---|---|
| `dotnet build --no-restore` | 24 projects, 0 errors, 0 warnings |
| `dotnet test --no-build --filter "FullyQualifiedName~AccommodationTests"` | 54 passed, 0 failed |
| `dotnet test --no-build --filter "FullyQualifiedName~UnitTests"` | 328 passed, 0 failed (274 prior + 54 new) |
| `dotnet test --no-build --filter "FullyQualifiedName~ArchitectureTests"` | 55 passed, 0 failed |
| `dotnet format --verify-no-changes --no-restore` | 8 CHARSET violations (pré-existentes em migrations de outros módulos — débito conhecido do baseline). Nenhum arquivo da Task 5.0 afetado. |

---

## Technical Review

### Compliance with Task Requirements

| Requisito | Status | Evidência |
|---|---|---|
| Modelar `Accommodation` com `commercialName` mínimo, ocupação, camas, características, política e faixa etária | ✅ | `Accommodation.cs`: private setters, `CommercialName`, `MaxAdults`/`MaxChildren`/`TotalCapacity`, `BedConfiguration` (IReadOnlyList), `StructuralFeatures`, `PolicyId`, `ChildAgeRangeSource`/`ChildMinimumAge`/`ChildMaximumAge` |
| `BedConfiguration` como value object/coleção semântica, persistido em JSONB | ✅ | `BedEntry` record + `BedType` enum; `AccommodationConfiguration` mapeia `_bedConfiguration` como `jsonb` com `ValueComparer` |
| `ChildAgeRangeSource` com `propertyDefault`, `accommodationOverride`, `none` | ✅ | Enum definido em `CommercialOfferValues.cs:36-41` |
| Herdar política padrão e faixa etária da propriedade | ✅ | `CreateAccommodationCommandHandler` busca `defaultPolicyId` do offer e `propertyDefaultChildAgeRange`; `CommercialOffer.AddAccommodation` os consome |
| Validar `maxAdults + maxChildren <= totalCapacity` e coerência camas/capacidade | ✅ | `ValidateOccupancy()` e `ValidateCapacityMatchesBeds()`; erro `INVALID_OCCUPANCY_CONFIGURATION` |
| Omissão mantém herança; `null` remove override; objeto define override (PATCH) | ✅ | `UpdateAccommodationCommand` com flags `HasX` + `ChildAgeRangeUpdateInput.IsNull`; omissão preserva, `IsNull=true` → `RevertChildAgeRangeToPropertyDefault`, objeto → `SetChildAgeRangeOverride` |
| Desativação exige motivo; hard delete somente antes de envio | ✅ | `Deactivate(reason)` requer string não-vazia; `DeleteAccommodation` verifica `CanDelete()` → `!EverSubmitted`; erro `ACCOMMODATION_DELETION_NOT_ALLOWED` |
| Fotos, descrição e comodidades editoriais não bloqueiam a acomodação | ✅ | `IsCommerciallyComplete()` verifica apenas `CommercialName`, `MaxAdults`, `TotalCapacity`, `MealPlan`, `PolicyId`, `OccupancyValid` — sem conteúdo editorial |
| Recalcular completude e pendências somente com campos comerciais F02 | ✅ | `RecalculateCompletenessFromAccommodations` conta acomodações ativas e completas; usa `CommercialOfferCompleteness.Compute` |
| Invalidar validação e incrementar revisão por mutação | ✅ | `IncrementRevisionMutate` incrementa `Revision`, chama `InvalidateValidationOnMutate()`; testado em `UpdateAccommodation_ShouldInvalidateValidation` |
| Testar matrizes de capacidade, camas, faixa infantil, política, rascunho, desativação e delete | ✅ | 54 testes: `[Theory]` com valores 0/1/2/20/30 para ocupação; testes de herança, override, revert, clear de faixa; herança de política; desativação com/sem motivo; delete protegido; revisão; error codes |
| 3 command handlers: Create, Update, Delete | ✅ | `CreateAccommodationCommandHandler`, `UpdateAccommodationCommandHandler`, `DeleteAccommodationCommandHandler` |
| FluentValidation para commands | ✅ | `CreateAccommodationCommandValidator`, `UpdateAccommodationCommandValidator`, `DeleteAccommodationCommandValidator` em `InventoryValidators.cs:315-393` |
| `CancellationToken` obrigatório em handlers | ✅ | Todos os 3 handlers declaram e propagam `cancellationToken` |
| Auditoria funcional em todas as mutações | ✅ | Todos os handlers registram `BusinessAuditEntry` com `"AccommodationCreated"`, `"AccommodationUpdated"`, `"AccommodationDeleted"` |

### Compliance with PRD (RF-02 and RF-04)

- ✅ **RF-02**: Given nova acomodação → dados registrados com validação coerente de capacidade/adultos/crianças/camas
- ✅ **RF-02**: Given propriedade com faixa etária → acomodação herda, permitindo substituição específica
- ✅ **RF-02**: Given ausência de conteúdo editorial → acomodação comercial completa pode avançar
- ✅ **RF-04**: Given informações incompletas → salva com pendências (rascunho)
- ✅ **RF-04**: Given item nunca enviado → hard delete permitido
- ✅ **RF-04**: Given item já enviado → desativação com motivo e histórico preservado

### Compliance with TechSpec

- ✅ Entidade `Accommodation` como filha do agregado `CommercialOffer`
- ✅ `ChildAgeRangeSource` enum com os três estados especificados
- ✅ Configuração de camas como JSONB; características estruturais como coleção de valores
- ✅ Faixa etária infantil com colunas explícitas (`child_minimum_age`, `child_maximum_age`, `child_age_range_source`)
- ✅ EF Core configuration `AccommodationConfiguration` com tabela `inventory.accommodations`, índice `ix_accommodations_property_status`, `ValueGeneratedNever()`
- ✅ Error codes: `INVALID_OCCUPANCY_CONFIGURATION`, `ACCOMMODATION_NOT_FOUND`, `ACCOMMODATION_DELETION_NOT_ALLOWED`, `ACCOMMODATION_ALREADY_INACTIVE`, `REVISION_MISMATCH`
- ✅ CQRS nativo com handlers usando `InventoryDbContext` direto
- ✅ Types `internal` (architecture tests pass)
- ✅ Entidade com setters privados e coleções somente leitura

### Skills Compliance

- **dotnet-architecture**: CQRS nativo, entidade filha, handlers com `InventoryDbContext` direto, exceções com error codes, `internal` types
- **dotnet-code-quality**: PascalCase/camelCase, constructor injection, `CancellationToken`, records para commands/responses, inglês, sem flag parameters (intenção modelada no Command via `HasX` flags)
- **dotnet-testing**: xUnit + AwesomeAssertions, AAA, naming convention `MethodName_Condition_ExpectedBehavior`, theories para limites 0/1/20/30, cobertura de caminhos positivo e negativo
- **dotnet-observability**: Audit writer registra propriedade/acomodação/autor sem dados editoriais ou PII

---

## Issues Found

### 1. `GetDefaultChildAgeRange()` retorna stub `null` (Não bloqueante)

**Categoria:** Feature incompleta  
**Severidade:** Baixa  
**Fase:** Implementação  
**Origem:** TechSpec (questão aberta)

**Descrição:** `CommercialOffer.GetDefaultChildAgeRange()` sempre retorna `null`. O comportamento é intencional — a TechSpec identifica como questão aberta: "Definir a origem futura da faixa etária infantil padrão da propriedade; até lá, `childAgeRangeSource` poderá ser `none`."

**Impacto:** Nenhum — as acomodações criadas sem override explícito de faixa etária recebem `ChildAgeRangeSource.None`, que é o comportamento esperado para o MVP.

**Sugestão:** Implementar a origem da faixa etária padrão da propriedade quando a decisão de design for tomada (possivelmente na propriedade incorporada ou no agregado).

### 2. `RecalculateCompletenessFromAccommodations` hardcoded com rates zerados (Não bloqueante)

**Categoria:** Feature incompleta  
**Severidade:** Baixa  
**Fase:** Implementação  
**Origem:** Task (escopo — rates são Task 6.0)

**Descrição:** `RecalculateCompletenessFromAccommodations` no `CommercialOffer` passa `activeRateCount = 0` e `hasAnyRateOverlap = false` para `RecalculateCompleteness`. Isso é esperado, pois as tarifas comerciais serão implementadas na Task 6.0.

**Impacto:** Baixo — a completude não considera tarifas ativas, o que é correto até que a Task 6.0 seja implementada. O cálculo de pendências pode indicar `IncompleteAccommodation` mesmo quando a única pendência real for `MissingActiveRate` (a acomodação em si está comercialmente completa).

**Sugestão:** Atualizar `RecalculateCompletenessFromAccommodations` na Task 6.0 para incluir `activeRateCount` e `hasAnyRateOverlap` reais.

### 3. Duplicação do mapeamento `ToResponse` / inline (Não bloqueante)

**Categoria:** Overengineering  
**Severidade:** Baixa  
**Fase:** Implementação  
**Origem:** Limitação do modelo

**Descrição:** O mapeamento `ToResponse` está duplicado no `CreateAccommodationCommandHandler` (linhas 169-187) e inline idêntico no `UpdateAccommodationCommandHandler` (linhas 312-331) e `DeleteAccommodationCommandHandler` (linhas 355-373). A lógica `bed.Type.ToString().ToLowerInvariant()` e `status.ToString().ToLowerInvariant()` é repetida 3 vezes.

**Sugestão:** Extrair para método estático compartilhado ou internal extension method. Similar ao apontamento feito na Task 4.0 (item 2).

---

## Final Recommendation

**VALIDAÇÃO APROVADA**

A implementação entrega todos os requisitos da Task 5.0 com 54 novos testes passando, 0 erros de build, 0 warnings, architecture tests verdes, e conformidade total com PRD (RF-02 e RF-04), TechSpec e skills. Os três apontamentos identificados são não-bloqueantes: dois são stubs intencionais para features de tasks subsequentes (faixa etária padrão da propriedade e tarifas), e um é duplicação de mapeamento de baixo impacto.
