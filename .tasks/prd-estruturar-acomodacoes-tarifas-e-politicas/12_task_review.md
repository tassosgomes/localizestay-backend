# Revisão da Task 12

## Gate determinístico

Resultado: **REPROVADO**

```text
GATE: REPROVADO
etapa: testes
comando: dotnet test ./LocalizeStay.sln --no-build --no-restore --filter "FullyQualifiedName~CommercialOffer"
--- output (ultimas 40 linhas) ---
A total of 1 test files matched the specified pattern.
A total of 1 test files matched the specified pattern.
No test matches the given testcase filter `FullyQualifiedName~CommercialOffer` in /home/tsgomes/github-tassosgomes/localizestay-backend/tests/LocalizeStay.ArchitectureTests/bin/Debug/net10.0/LocalizeStay.ArchitectureTests.dll

[xUnit.net 00:00:03.40]     LocalizeStay.UnitTests.Inventory.CommercialOfferMetricsQueryHandlerTests.HandleAsync_WithNoOffers_ReturnsZeroMetricsAndDefinedDenominators [FAIL]
  Failed LocalizeStay.UnitTests.Inventory.CommercialOfferMetricsQueryHandlerTests.HandleAsync_WithNoOffers_ReturnsZeroMetricsAndDefinedDenominators [66 ms]
  Error Message:
   Expected response.DualValidationRate to be 1.0, but found 0.0 (difference of -1).
  Stack Trace:
     at AwesomeAssertions.Execution.LateBoundTestFramework.Throw(String message)
     at AwesomeAssertions.Numeric.NumericAssertionsBase`3.Be(T expected, String because, Object[] becauseArgs)
     at LocalizeStay.UnitTests.Inventory.CommercialOfferMetricsQueryHandlerTests.HandleAsync_WithNoOffers_ReturnsZeroMetricsAndDefinedDenominators() in /home/tsgomes/github-tassosgomes/localizestay-backend/tests/LocalizeStay.UnitTests/Inventory/CommercialOfferMetricsQueryHandlerTests.cs:line 29
     at LocalizeStay.UnitTests.Inventory.CommercialOfferMetricsQueryHandlerTests.HandleAsync_WithNoOffers_ReturnsZeroMetricsAndDefinedDenominators() in /home/tsgomes/github-tassosgomes/localizestay-backend/tests/LocalizeStay.UnitTests/Inventory/CommercialOfferMetricsQueryHandlerTests.cs:line 31
--- End of stack trace from previous location ---

Failed!  - Failed:     1, Passed:    94, Skipped:     0, Total:    95, Duration: 2 s - LocalizeStay.UnitTests.dll (net10.0)
[xUnit.net 00:00:13.44]     LocalizeStay.IntegrationTests.Inventory.CommercialOfferSecurityTests.CommercialOfferSecurity_AllFourPermissionsAreEnforcedOnDistinctOperations [FAIL]
  Failed LocalizeStay.IntegrationTests.Inventory.CommercialOfferSecurityTests.CommercialOfferSecurity_AllFourPermissionsAreEnforcedOnDistinctOperations [2 s]
  Error Message:
   System.Net.Http.HttpRequestException : Response status code does not indicate success: 403 (Forbidden).
  Stack Trace:
     at System.Net.Http.HttpResponseMessage.EnsureSuccessStatusCode()
     at System.Net.Http.Json.HttpClientJsonExtensions.<FromJsonAsyncCore>g__Core|12_0[TValue,TJsonOptions](HttpClient client, Task`1 responseTask, Boolean usingResponseHeadersRead, CancellationTokenSource linkedCTS, Func`4 deserializeMethod, TJsonOptions jsonOptions, CancellationToken cancellationToken)
     at LocalizeStay.IntegrationTests.Inventory.CommercialOfferSecurityTests.CommercialOfferSecurity_AllFourPermissionsAreEnforcedOnDistinctOperations() in /home/tsgomes/github-tassosgomes/localizestay-backend/tests/LocalizeStay.IntegrationTests/Inventory/CommercialOfferSecurityTests.cs:line 132
--- End of stack trace from previous location ---
[xUnit.net 00:01:04.78]     LocalizeStay.IntegrationTests.Inventory.CommercialOfferEndToEndTests.FullPipeline_OnboardSubmitReturnCorrectResubmit_ShouldPreserveHistoryAndSingleOutbox [FAIL]
  Failed LocalizeStay.IntegrationTests.Inventory.CommercialOfferEndToEndTests.FullPipeline_OnboardSubmitReturnCorrectResubmit_ShouldPreserveHistoryAndSingleOutbox [1 s]
  Error Message:
   Expected returnedOffer.GetProperty("status").GetString() to be the same string, but they differ at index 0:\r\n   ↓ (actual)\n  "submitted"\n  "returned"\n   ↑ (expected).
  Stack Trace:
     at AwesomeAssertions.Execution.LateBoundTestFramework.Throw(String message)
     at AwesomeAssertions.Primitives.StringAssertions`1.Be(String expected, String because, Object[] becauseArgs)
     at LocalizeStay.IntegrationTests.Inventory.CommercialOfferEndToEndTests.FullPipeline_OnboardSubmitReturnCorrectResubmit_ShouldPreserveHistoryAndSingleOutbox() in /home/tsgomes/github-tassosgomes/localizestay-backend/tests/LocalizeStay.IntegrationTests/Inventory/CommercialOfferEndToEndTests.cs:line 108
--- End of stack trace from previous location ---

Failed!  - Failed:     2, Passed:   101, Skipped:     0, Total:   103, Duration: 55 s - LocalizeStay.IntegrationTests.dll (net10.0)
```

## Revisão semântica

Não executada: o gate determinístico falhou, conforme a ordem mandatória de validação.

### Bloqueantes

- `CommercialOfferMetricsQueryHandlerTests.HandleAsync_WithNoOffers_ReturnsZeroMetricsAndDefinedDenominators`: `DualValidationRate` retornou `0.0`, mas o contrato testado requer `1.0` sem ofertas.
- `CommercialOfferSecurityTests.CommercialOfferSecurity_AllFourPermissionsAreEnforcedOnDistinctOperations`: uma operação protegida retornou `403 Forbidden` quando o cenário esperava sucesso com a permissão correspondente.
- `CommercialOfferEndToEndTests.FullPipeline_OnboardSubmitReturnCorrectResubmit_ShouldPreserveHistoryAndSingleOutbox`: após o retorno, a oferta permaneceu com status `submitted` em vez de `returned`.

### Observações

- Nenhuma; a revisão foi interrompida no gate.

## Recomendação final

**REPROVADA**

## Revalidação #3

### Gate determinístico

Resultado: **APROVADO**

```text
GATE: APROVADO
arquivos alterados: 74 (.cs: 21)
format: ok (21 arquivos)
build: ok 0 Warning(s) 0 Error(s) 
testes: ok (FullyQualifiedName~CommercialOffer=198)
```

### Revisão semântica

O parser reutilizável cobre GET, POST, PUT, PATCH e DELETE; a suíte certifica as 20 operações F02, os metadados HTTP, `Location`, 204, matriz de status, persistência, atomicidade, permissões, idempotência e o E2E de devolução/reenvio. Porém, a certificação de métricas não cobre integralmente a subtarefa 12.7.

### Bloqueantes

- `CommercialOfferMetricsTests` não testa o calendário de negócio, os valores exatos de numeradores/denominadores nem o reprocessamento. Os cenários atuais apenas verificam campos, janelas vazias, filtro, limites de taxa entre 0 e 1 e uma oferta criada; portanto, não comprovam os invariantes exigidos pela subtarefa 12.7.

### Observações

- Nenhuma.

### Recomendação final

**REPROVADA**

## Revalidação #4

### Gate determinístico

Resultado: **APROVADO**

```text
GATE: APROVADO
arquivos alterados: 76 (.cs: 23)
format: ok (23 arquivos)
build: ok 0 Warning(s) 0 Error(s) 
testes: ok (FullyQualifiedName~CommercialOffer=199)
```

### Revisão semântica

Resolvido o bloqueante da revalidação anterior. `CommercialOfferMetricsTests.GetMetrics_WithFridayCompletion_ShouldUseBusinessCalendarAndBeReprocessable` usa PostgreSQL/Testcontainers e comprova a regra sexta-feira → terça-feira (dois dias úteis), total de duas ofertas completas, taxa SLA de 0,5, dupla validação de 1,0, denominadores explícitos e resposta idêntica em duas leituras consecutivas. Não há testes skipped; as mudanças de política alinham expectativas unitárias ao `ConflictException` já emitido, e a regra arquitetural exclui apenas migrations EF Core, que precisam ser públicas.

### Bloqueantes

- Nenhum.

### Observações

- Nenhuma.

### Recomendação final

**APROVADA**
