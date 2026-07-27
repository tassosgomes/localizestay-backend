---
status: pending
parallelizable: true
blocked_by: []
---

<task_context>
<domain>infra/shared-kernel/error-handling</domain>
<type>implementation</type>
<scope>middleware</scope>
<complexity>medium</complexity>
<dependencies>http_server</dependencies>
<unblocks>"18.0, 19.0, 21.0, 42.0"</unblocks>
<vertical_slice>Um erro 422 de regra de negócio chega ao cliente com metadata estruturada no corpo Problem Details.</vertical_slice>
</task_context>

# Tarefa 1.0: Propagar metadados estruturados de erro para Problem Details

## Relacionada às User Stories

- [US-01] Registrar allotment (suporte — `ALLOTMENT_BELOW_COMMITTED` precisa listar as datas em conflito)
- [US-03] Bloquear datas imediatamente (suporte — `INSUFFICIENT_FREE_BALANCE` precisa informar o saldo livre por data)

## Visão Geral

Três códigos de erro do contrato da F03 exigem que o corpo `application/problem+json` carregue um campo `metadata` com conteúdo estruturado: `ALLOTMENT_BELOW_COMMITTED` (`conflictingDates`), `INSUFFICIENT_FREE_BALANCE` (`freeBalanceByDate`) e `INSUFFICIENT_AVAILABILITY` (`unavailableDates`). O `BuildMetadata` atual do `GlobalExceptionHandler` só popula `conflictingResourceId` a partir de `ConflictException`, e a `BusinessRuleViolationException` não tem onde carregar dados.

Esta tarefa habilita esse transporte sem alterar nenhum comportamento existente da F01 e da F02, que não usam o campo.

## Requisitos

- `BusinessRuleViolationException` passa a aceitar, opcionalmente, um dicionário de metadados arbitrários junto com a mensagem e o `errorCode`.
- Os construtores atuais permanecem funcionando sem alteração de assinatura para os chamadores existentes.
- `GlobalExceptionHandler` propaga esse dicionário para a chave `metadata` da resposta RFC 9457, preservando `code`, `traceId` e `errors`.
- Quando não há metadados, a chave `metadata` **não** aparece no corpo — nenhuma resposta atual da F01/F02 muda de formato.
- Nenhum dado sensível é aceito no dicionário; o conteúdo é sempre derivado de identificadores, datas e quantidades.

## Arquivos Envolvidos

- **Criar:**
  - `../localizestay-backend/tests/LocalizeStay.UnitTests/ErrorHandling/BusinessRuleViolationMetadataTests.cs`
- **Modificar:**
  - `../localizestay-backend/src/BuildingBlocks/LocalizeStay.SharedKernel/ErrorHandling/BusinessRuleViolationException.cs` (novo construtor com metadados e propriedade `Metadata`)
  - `../localizestay-backend/src/BuildingBlocks/LocalizeStay.SharedKernel/ErrorHandling/GlobalExceptionHandler.cs` (estender `BuildMetadata` para carregar metadados arbitrários)
- **Referência:**
  - `../localizestay-backend/src/BuildingBlocks/LocalizeStay.SharedKernel/ErrorHandling/ConflictException.cs` (padrão vigente de `conflictingResourceId`)
  - `.tasks/prd-controlar-allotment-disponibilidade-bloqueios-e-retencoes/api-contract.md` (exemplo canônico do corpo de `ALLOTMENT_BELOW_COMMITTED`)
- **Skills para consultar durante implementação:**
  - `restful-api` — RFC 9457, `application/problem+json`, campos de extensão
  - `dotnet-architecture` — tratamento global de erros com `IExceptionHandler`
  - `dotnet-code-quality` — construtores com sobrecarga, ausência de flag params

## Subtarefas

- [ ] 1.1 Adicionar a `BusinessRuleViolationException` uma propriedade `IReadOnlyDictionary<string, object?> Metadata` e um construtor que a recebe, mantendo os dois construtores atuais intactos.
- [ ] 1.2 Estender `BuildMetadata` no `GlobalExceptionHandler` para mesclar os metadados da exceção com os já produzidos, omitindo a chave quando o dicionário estiver vazio.
- [ ] 1.3 Testar: metadados presentes aparecem em `metadata`; ausentes não criam a chave; `ConflictException` continua produzindo `conflictingResourceId`; `code` e `traceId` seguem preenchidos.

## Sequenciamento

- Bloqueado por: Nenhum
- Desbloqueia: 18.0, 19.0, 21.0, 42.0
- Paralelizável: Sim; toca apenas o SharedKernel de tratamento de erros, que nenhuma outra tarefa da F03 altera.

## Rastreabilidade

- Esta tarefa cobre: US-01 e US-03 como suporte; viabiliza os três códigos de erro com `metadata` exigidos pelo contrato.
- Evidência esperada: `BusinessRuleViolationMetadataTests` prova o formato de saída, e as tarefas 18.0, 19.0 e 42.0 consomem o mecanismo.

## Detalhes de Implementação

Formato-alvo do corpo, conforme o `api-contract.md`:

```json
{
  "type": "https://api.localizestay.com/problems/allotment-below-committed",
  "title": "Redução abaixo do comprometido",
  "status": 422,
  "detail": "Existem datas com capacidade comprometida acima da nova quantidade. Registre um bloqueio para reduzir a venda sem alterar o contrato.",
  "code": "ALLOTMENT_BELOW_COMMITTED",
  "traceId": "00-4bf9...-01",
  "metadata": {
    "conflictingDates": [
      { "date": "2026-09-14", "committedUnits": 3 }
    ]
  }
}
```

Assinatura sugerida:

```csharp
public BusinessRuleViolationException(
    string message,
    string errorCode,
    IReadOnlyDictionary<string, object?> metadata)
```

`Metadata` deve ser inicializado como dicionário vazio nos construtores antigos, para que o handler nunca precise checar `null`.

**Convenções da stack (das skills consultadas):**

- Erros seguem RFC 9457 com `code` estável do contrato, conforme `restful-api`.
- Exceção customizada herda de `DomainException`; nenhuma nova hierarquia é criada (`dotnet-architecture`).
- Nomes em inglês, propriedade somente leitura, sem flag param (`dotnet-code-quality`).
- Testes xUnit + AwesomeAssertions em AAA, naming `Metodo_Condicao_ComportamentoEsperado` (`dotnet-testing`).
- Nenhum dado sensível transita em `metadata` (`dotnet-production-readiness`).

## Critérios de Sucesso (Verificáveis)

- [ ] Testes passam: `dotnet test ../localizestay-backend/LocalizeStay.sln --filter "FullyQualifiedName~BusinessRuleViolationMetadataTests"`
- [ ] Build compila sem erros: `dotnet build ../localizestay-backend/LocalizeStay.sln --no-restore`
- [ ] Formatação válida: `dotnet format ../localizestay-backend/LocalizeStay.sln --verify-no-changes --no-restore`
- [ ] Exceção com metadados produz corpo com `metadata` contendo exatamente as chaves informadas.
- [ ] Exceção sem metadados produz corpo **sem** a chave `metadata`.
- [ ] Suíte existente da F01/F02 segue verde: `dotnet test ../localizestay-backend/tests/LocalizeStay.IntegrationTests --filter "FullyQualifiedName~SecurityAndProblemDetailsTests"`
