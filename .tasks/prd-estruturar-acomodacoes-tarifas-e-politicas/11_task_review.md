# Task 11.0 — Revisão de Validação

- **PRD:** prd-estruturar-acomodacoes-tarifas-e-politicas
- **Task:** 11.0 — Instrumentar, documentar e preparar a operação
- **Iteração:** 1 (validação full)
- **Modelo (implementação):** glm-5.2
- **Data:** 2026-07-26
- **Branch:** feature/prd-estruturar-acomodacoes-tarifas-e-politicas

---

## 1. Gate determinístico

Comando executado (a partir da raiz do repo, prefixo `../localizestay-backend/` removido):

```bash
scripts/ai-flow/gate.sh --filter="FullyQualifiedName~CommercialOfferObservabilityTests"
```

Saída (verbatim):

```
GATE: APROVADO
arquivos alterados: 59 (.cs: 8)
format: ok (8 arquivos)
build: ok 0 Warning(s) 0 Error(s)
testes: ok (FullyQualifiedName~CommercialOfferObservabilityTests=14)
```

- Filtro extraído do critério de sucesso da task: `FullyQualifiedName~CommercialOfferObservabilityTests` (único declarado).
- Build: 0 erros, 0 warnings. Formatação escopada: OK. `git diff --check`: OK. 14 testes aprovados.

**Resultado Stage 1: APROVADO.**

---

## 2. Escopo revisado (diff)

Arquivos da task no diff (`git diff HEAD` + untracked):

- Criados:
  - `docs/runbooks/commercial-offers.md`
  - `tests/LocalizeStay.IntegrationTests/Inventory/CommercialOfferObservabilityTests.cs`
- Modificados:
  - `src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/Observability/InventoryTelemetry.cs`
  - `src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/CommercialOffers/AccommodationCommands.cs`
  - `src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/CommercialOffers/CommercialOfferQueries.cs`
  - `src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/CommercialOffers/CommercialOfferWorkflowCommands.cs`
  - `src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/CommercialOffers/CommercialPolicyCommands.cs`
  - `src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/CommercialOffers/CommercialRateCommands.cs`
  - `src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/CommercialOffers/CurationOfferReturnedHandler.cs`
  - `README.md`

Material de apoio aberto: `techspec.md` § "Monitoramento e Observabilidade" (linhas 455-485) para confirmar nomes canônicos de métricas/spans e a lista de alertas (a task referencia "da TechSpec" como fonte autoritativa no critério de sucesso). Sem outras aberturas além do arquivo de task e do diff.

---

## 3. Revisão semântica

### Critérios de sucesso (todos atendidos)

| # | Critério | Evidência | Status |
|---|---|---|---|
| 1 | Build sem erros | Gate: `0 Error(s)` | ✅ |
| 2 | Filtro `FullyQualifiedName~CommercialOfferObservabilityTests` | Gate: 14 testes aprovados | ✅ |
| 3 | `dotnet format --verify-no-changes` | Gate: format OK (8 arquivos) | ✅ |
| 4a | `GET /health/live` 200 sem dependências | Teste `HealthLive_ShouldReturn200WithoutTouchingPostgreSql` (200 + sem `postgres`/`connection`/...) | ✅ |
| 4b | `GET /health/ready` inclui PostgreSQL | Cabeamento em `ModuleDatabaseExtensions.cs:29` (`AddDbContextCheck<TDbContext>("inventory-database", tags:[ready])`) | ✅ (ver observação O1) |
| 5 | Sem conteúdo sensível nos templates de log da F02 | Teste `CommercialOfferHandlers_LogTemplates_ShouldNotContainSensitiveContent` varre todos `.cs` em `CommercialOffers/`; templates só referenciam `EventId`/`PropertyId`/`SubmissionId` | ✅ |
| 6 | Runbook com deploy/rollback/replay/alertas/diagnóstico | `docs/runbooks/commercial-offers.md` cobre migration+backfill, rollback app-first/schema-last, replay de outbox, alertas, SLI/SLO, troubleshooting (`REVISION_MISMATCH`, `RATE_PERIOD_OVERLAP`, replay, devolução ignorada, outbox) | ✅ |
| 7 | Métricas e spans registrados uma única vez | Teste `InventoryTelemetry_ShouldRegisterAllCommercialOfferMetricsExactlyOnce` (9 métricas F02, cada uma com field único) + `InventoryTelemetry_ShouldExposeAllCommercialOfferSpanNames` (5 spans) | ✅ |

### Conferência contra a TechSpec (nomes canônicos)

Métricas (techspec linhas 459-467) — todas presentes em `InventoryTelemetry` e incrementadas na fonte declarada:

- `inventory.commercial_offer.created` → `OfferCreated` (`CommercialOfferQueries`, draft creation)
- `inventory.commercial_offer.mutation` → `OfferMutation` (accommodation/policy/rate CRUD, tag `operation`)
- `inventory.commercial_offer.validation` → `OfferValidation` (`CreateOfferValidationCommandHandler`)
- `inventory.commercial_offer.validation_invalidated` → `OfferValidationInvalidated` (mutações + revalidação)
- `inventory.commercial_offer.submission` → `OfferSubmission` (`SubmitCommercialOfferCommandHandler`)
- `inventory.commercial_offer.returned` → `OfferReturned` (`CurationOfferReturnedHandler`)
- `inventory.commercial_offer.rate_overlap` → `OfferRateOverlap` (create/update com `RATE_PERIOD_OVERLAP`)
- `inventory.commercial_offer.submission_duration` → `OfferSubmissionDuration` (histograma s)
- `inventory.commercial_offer.outbox_failure` → `OfferOutboxFailure` (`DbUpdateException` no submit)

Spans (techspec linhas 474-478): `load`, `validate`, `submit`, `return`, `metrics` — todos expostos via `InventoryTelemetry.Spans` e iniciados no handler correspondente (load→Queries, validate/submit→WorkflowCommands, return→CurationOfferReturnedHandler, metrics→GetCommercialOfferMetricsQueryHandler). Tags de baixa cardinalidade (`operation`/`result`) em métricas; identificadores apenas em span tags/log scopes. Conforme padrão da skill `dotnet-observability`.

Alertas (techspec linhas 482-485): todos reproduzidos na tabela de alertas do runbook, com responsáveis e evidência de correlação.

### Qualidade da instrumentação

- IDs (`propertyId`, `offerRevision`, `validationId`, `submissionId`, `eventId`, `correlationId`) trafegam somente em span tags/log scopes, nunca como labels de métrica — cardinalidade controlada.
- Refatoração de literais de span/tag para constantes (`InventoryTelemetry.Spans`/`Tags`) preserva o comportamento dos handlers existentes (mudança semanticamente equivalente, sem regressão).
- `operation` = `policy_deactivated` no `UpdateCommercialPolicyCommandHandler` está correto: o handler chama `offer.DeactivatePolicy(...)` (o nome "Update" do comando é débito pré-existente de naming, fora do escopo da task).
- Templates de log em `CurationOfferReturnedHandler` registram apenas `EventId`/`PropertyId`/`SubmissionId`; `Reason` é persistido via auditoria, nunca logado — alinhado a RF/proibição de conteúdo sensível.

---

## 4. Achados

### Bloqueantes
*(nenhum)*

### Observações (não bloqueantes)

- **O1 — Asserção runtime de `/health/ready` delegada à tarefa 12.0.** O cabeamento de PostgreSQL no readiness existe (`ModuleDatabaseExtensions` registra `inventory-database` no tag `ready`), então o critério comportamental está atendido. A verificação por Testcontainers não foi adicionada nesta task porque a 12.0 é a responsável pela certificação de readiness. Origem: `Task mal fragmentada` (critério dividido entre 11.0 e 12.0). Categoria: `Teste inadequado`. Severidade: Baixa.
- **O2 — Tag `result` dos contadores `validation`/`submission`/`returned` é emitida apenas no caminho de sucesso.** Falhas de validação não incrementam `validation{result="failure"}`; falhas de persistência no submit são capturadas por `outbox_failure`, não por `submission{result="failure"}`. Dashboards de taxa de falha precisarão combinar contadores. A TechSpec apenas prescreveu a dimensão `result`, não a semântica do caminho de falha. Origem: `Lacuna na TechSpec`. Categoria: `Edge case ignorado`. Severidade: Baixa.

---

## 5. Recomendação final

**APROVADA.**

Gate determinístico aprovado; revisão semântica aprovada. Nenhum achado bloqueante. 2 observações não bloqueantes registradas em `docs/ai-dev/quality-ledger.jsonl`.
