# Task Review — 1.0: Propagar metadados estruturados de erro para Problem Details

## Gate Determinístico

```text
GATE: APROVADO
arquivos alterados: 5 (.cs: 4)
format: ok (4 arquivos)
build: ok 0 Warning(s) 0 Error(s)
testes: ok (FullyQualifiedName~BusinessRuleViolationMetadataTests=3 FullyQualifiedName~SecurityAndProblemDetailsTests=14)
```

## Revisão Semântica

A implementação atende aos requisitos da task:

- `BusinessRuleViolationException` recebeu a propriedade `IReadOnlyDictionary<string, object?> Metadata` e um novo construtor que a aceita.
- Os dois construtores existentes foram preservados com as mesmas assinaturas; apenas o corpo foi estendido para inicializar `Metadata` como dicionário vazio.
- `GlobalExceptionHandler.BuildMetadata` agora mescla `conflictingResourceId` (de `ConflictException`) com os metadados da `BusinessRuleViolationException` e retorna `null` quando o dicionário resultante está vazio, garantindo que a chave `metadata` seja omitida nesses casos.
- Os campos `code`, `traceId` e `errors` continuam preenchidos.
- A suíte de testes `BusinessRuleViolationMetadataTests` cobre:
  - metadados presentes no corpo da resposta;
  - ausência da chave `metadata` quando não há metadados;
  - preservação do comportamento de `ConflictException`.
- A suíte de integração `SecurityAndProblemDetailsTests` foi ajustada para refletir a omissão da chave `metadata` quando vazia, mantendo a cobertura contratual da F01/F02.

## Bloqueantes

Nenhum.

## Observações

1. **`dotnet-tools.json` não está rastreado**: o manifesto vazio aparece como arquivo novo (`??`) e não consta nos arquivos envolvidos da task. Recomenda-se removê-lo ou incluí-lo no controle de versões se for intencional.
2. **`SecurityAndProblemDetailsTests.cs` não estava listado como arquivo a modificar**: a alteração é tecnicamente necessária para alinhar os testes ao novo comportamento, mas a task poderia ser atualizada para listá-lo explicitamente.
3. **`JsonElementExtensions.GetobjectLength` ficou sem uso** em `SecurityAndProblemDetailsTests.cs` após a troca para `TryGetProperty`. Trata-se de código morto, não bloqueante.

## Recomendação Final

**APROVADA**
