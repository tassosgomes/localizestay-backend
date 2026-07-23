# Task Review Report — Task 6.0

**PRD:** prd-estruturar-acomodacoes-tarifas-e-politicas  
**Task:** 6.0 — Implementar tarifas comerciais e períodos  
**Data:** 2026-07-22  
**Revisor:** AI Flow Validator  
**Iteração:** 2 (Revalidação pós-correção)

---

## 0. Correção Aplicada (Iteração 1 → 2)

| Issue Anterior | Correção | Status |
|---|---|---|
| Issue 1: `MealPlan` enum mismatch — `None` vs `RoomOnly` | `MealPlan.None` → `MealPlan.RoomOnly` em `CommercialOfferValues.cs:56` | ✅ Corrigido |
| Issue 2: Teste `Rate_MandatoryFeesIncluded_AlwaysTrue` não verifica invariante | Não bloqueante — mantido como observação | ⚠️ Persiste (baixa severidade) |

**Verificação do fix:**
- Enum de domínio: `RoomOnly, Breakfast, HalfBoard, FullBoard`
- API Contract: `[roomOnly, breakfast, halfBoard, fullBoard]`
- `Enum.Parse<MealPlan>("roomOnly", true)` agora resolve para `MealPlan.RoomOnly` (case-insensitive match)
- Zero referências a `MealPlan.None` no codebase
- Teste em `AccommodationTests.cs:690` já usa `MealPlan.RoomOnly`

---

## 1. Automated Validation

| Comando | Resultado |
|---|---|
| `dotnet build --no-restore` | 24 projects, 0 errors, 0 warnings |
| `dotnet test --filter "CommercialRateTests"` | 52 passed, 0 failed, 0 skipped |
| `dotnet test --filter "UnitTests"` | 380 passed, 0 failed, 0 skipped |

**Resultado:** Todos os comandos de validação automatizada passaram.

---

## 2. Technical Review

### 2.1 Coverage against Task Requirements

| Subtarefa | Status | Evidência |
|---|---|---|
| 6.1 Modelar `CommercialRate` | ✅ | Entidade com `long` para centavos, `DateOnly?` para períodos, `PolicyId`, `MealPlan`, `RateStatus`, `EverSubmitted`, `SubmissionIds` |
| 6.2 `CreateCommercialRateCommandHandler` | ✅ | Handler com FluentValidation, rascunho progressivo e ativação quando todos os campos preenchidos |
| 6.3 `UpdateCommercialRateCommandHandler` e invalidação | ✅ | Handler implementado. `IncrementRevisionMutate` invoca `InvalidateValidationOnMutate()`. Teste `UpdateRate_ShouldInvalidateValidation` comprova |
| 6.4 `DeleteCommercialRateCommandHandler` | ✅ | Hard delete para nunca enviado; submetido bloqueia com `RATE_DELETION_NOT_ALLOWED`. Desativação via Update com `DeactivationReason` |
| 6.5 Algoritmo de sobreposição inclusiva | ✅ | `OverlapsWith` usa comparação inclusiva (`<=`), confere `ConditionCode`, `PolicyId` e `MealPlan`, ignora rascunhos |
| 6.6 Prontidão/completude | ✅ | Ambos handlers chamam `RecalculateCompletenessFromAccommodations`. Overlap reportado como `PendingIssueType.RatePeriodOverlap` |
| 6.7 Testes de matriz temporal, BRL, taxas, hóspedes, delete | ✅ | 52 testes cobrem adjacência, interseção, contenção, datas iguais, draft, delete, revisão, completude |

### 2.2 Persisting Observation (Não Bloqueante)

#### Observation: Teste `Rate_MandatoryFeesIncluded_AlwaysTrue` não verifica `mandatoryFeesIncluded`

- **Categoria Técnica:** Teste inadequado
- **Severidade:** Baixa
- **Fase Detectada:** Revisão (Iteração 1)
- **Origem Provável:** Task
- **Necessitou Reimplementação Significativa:** Não

**Descrição:**

O teste `Rate_MandatoryFeesIncluded_AlwaysTrue` (`CommercialRateTests.cs:964`) apenas faz `rate.IsComplete().Should().BeTrue()`. A entidade `CommercialRate` não possui campo `mandatoryFeesIncluded` nem `currency`. Embora a tarefa declare que estes são "invariantes do servidor" (não persistidos), o teste não comprova nenhuma invariante — apenas verifica completude, o que não corresponde ao nome do teste.

### 2.3 PRD/User Story Coverage

- **US-01 (Cadastrar condições comerciais progressivamente):** ✅ Dados parciais criam rascunho; todos os campos preenchidos ativam a tarifa.
- **US-03 (Conferir preços e condições):** ✅ Overlap bloqueia inconsistências; `RecalculateCompleteness` verifica tanto `MissingActiveRate` quanto `RatePeriodOverlap`.
- **RF-03 (Definir tarifas comerciais):** ✅ Valor-base, hóspedes incluídos, adicionais, período, mínimo de noites, política e alimentação. Sobreposição bloqueada.
- **RF-04 (Rascunhos e pendências):** ✅ Delete condicional; desativação via Update com motivo.

### 2.4 TechSpec Compliance

| Item | Status |
|---|---|
| `CommercialRate` entidade com `long` centavos, `DateOnly`, `RateStatus` | ✅ |
| Overlap por `accommodation_id, condition_code, policy_id, meal_plan, valid_from, valid_to` | ✅ Via `GetOverlappingRates` |
| Índice `ix_commercial_rates_overlap` | ✅ Em `CommercialRateConfiguration` |
| `RATE_PERIOD_OVERLAP` 409 | ✅ No handler |
| `RATE_DELETION_NOT_ALLOWED` 422 | ✅ No agregado |
| `RATE_NOT_FOUND` 404 | ✅ No handler e agregado |
| `REVISION_MISMATCH` 409 | ✅ Em `IncrementRevisionMutate` |
| CQRS nativo, EF Core direto, `CancellationToken` | ✅ |
| Logs não expõem preços completos | ✅ Auditoria registra IDs e nomes, sem valores monetários |
| `MealPlan` enum alinhado com API Contract | ✅ `RoomOnly` no domínio = `roomOnly` no contrato |

### 2.5 Skill Compliance

- **dotnet-architecture:** ✅ CQRS nativo, domínio puro, regras no agregado, tipos `internal`.
- **dotnet-code-quality:** ✅ `CancellationToken`, nomes em inglês, exceções específicas com códigos de erro.
- **dotnet-testing:** ✅ xUnit, AwesomeAssertions, AAA, `Theory` para limites, 52 testes.
- **dotnet-performance:** ✅ Índice de overlap configurado, `AsNoTracking` em queries (via handler pattern).

---

## 3. Final Recommendation

**VALIDAÇÃO APROVADA**

O Issue 1 (bloqueante da iteração anterior) foi corrigido: `MealPlan.None` → `MealPlan.RoomOnly` alinhando o enum de domínio com os valores camelCase do API Contract. `Enum.Parse<MealPlan>("roomOnly", true)` agora resolve corretamente. Zero referências residuais a `MealPlan.None`. Build, 52 CommercialRateTests e 380 UnitTests passam.

Issue 2 (teste com nome enganoso) persiste como observação não bloqueante de baixa severidade.

---

## 4. Test Execution Summary

```
Passed! - Failed: 0, Passed: 52, Skipped: 0, Total: 52 - CommercialRateTests
Passed! - Failed: 0, Passed: 380, Skipped: 0, Total: 380 - UnitTests (all)
```

Iterações até estabilização: 2
