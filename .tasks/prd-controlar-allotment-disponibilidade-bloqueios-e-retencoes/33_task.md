---
status: pending
parallelizable: true
blocked_by: ["27.0", "28.0", "29.0", "30.0", "31.0"]
---

<task_context>
<domain>inventory/testing/contract</domain>
<type>testing</type>
<scope>core_feature</scope>
<complexity>medium</complexity>
<dependencies>http_server</dependencies>
<unblocks>"49.0"</unblocks>
<vertical_slice>As 19 operações da Onda A são provadas conformes ao api-contract.yaml — método, path, status, headers e formato de erro.</vertical_slice>
</task_context>

# Tarefa 33.0: Certificar o contrato das 19 operações da Onda A

## Relacionada às User Stories

- [US-01], [US-02], [US-03], [US-05], [US-06] (suporte — a certificação prova que a superfície entregue é a acordada)

## Visão Geral

O `api-contract.yaml` é a fonte soberana. Esta tarefa prova, por teste automatizado, que a implementação da Onda A corresponde a ele, reutilizando `OpenApiContractDocument`, o parser criado pela F02.

Uma exceção precisa ser registrada explicitamente: o `429` de `getAvailability` é produzido pela borda, não pela aplicação, e não é exercitável em teste. O parser deve tratá-lo como exceção conhecida em vez de falhar.

## Requisitos

- Exatamente **19 operações da Onda A** presentes, com método e path correspondentes ao contrato (as quatro da Onda B entram em 49.0).
- Todos os status declarados por operação exercitados, com a exceção registrada de `getAvailability` + `429`.
- Header `Location` nas respostas `201`; ausência de corpo nos `204`.
- `application/problem+json` em todos os erros, com `code` e `traceId`.
- `metadata` presente em `ALLOTMENT_BELOW_COMMITTED`, `INSUFFICIENT_FREE_BALANCE`.
- `getAvailability` acessível sem token; as demais 18 protegidas pela permissão declarada em `x-required-permissions`.
- `Idempotency-Key` obrigatório em `createInventoryBlock`.
- Campos obrigatórios dos schemas e os exemplos críticos de erro do contrato.

## Arquivos Envolvidos

- **Criar:**
  - `../localizestay-backend/tests/LocalizeStay.IntegrationTests/Inventory/InventoryContractTests.cs`
- **Modificar:**
  - `../localizestay-backend/tests/LocalizeStay.IntegrationTests/Infrastructure/OpenApiContractDocument.cs` (registrar a exceção conhecida `getAvailability` + `429`)
- **Referência:**
  - `.tasks/prd-controlar-allotment-disponibilidade-bloqueios-e-retencoes/api-contract.yaml` (fonte soberana)
  - `../localizestay-backend/tests/LocalizeStay.IntegrationTests/Inventory/CommercialOfferApiContractTests.cs` (padrão de teste de contrato da F02)
  - `.tasks/prd-controlar-allotment-disponibilidade-bloqueios-e-retencoes/adrs/adr-005.md`
- **Skills para consultar durante implementação:**
  - `restful-api` — OpenAPI 3.1, status por operação, RFC 9457
  - `dotnet-testing` — `WebApplicationFactory` + Testcontainers, testes parametrizados

## Subtarefas

- [ ] 33.1 Carregar o `api-contract.yaml` e assertar que as 19 operações da Onda A existem com método, path e `operationId` correspondentes.
- [ ] 33.2 Registrar em `OpenApiContractDocument` a exceção conhecida `getAvailability` + `429`, com comentário citando ADR-005.
- [ ] 33.3 Exercitar os status declarados por operação, incluindo `Location` nos `201`, corpo ausente nos `204` e `problem+json` com `code` e `traceId` nos erros.
- [ ] 33.4 Assertar `metadata` nos dois códigos que a exigem na Onda A e `Idempotency-Key` obrigatório em `createInventoryBlock`.

## Sequenciamento

- Bloqueado por: 27.0, 28.0, 29.0, 30.0, 31.0
- Desbloqueia: 49.0
- Paralelizável: Sim; roda em paralelo às demais tarefas da Fase 6.

## Rastreabilidade

- Esta tarefa cobre: a seção Testes de Contrato da TechSpec, restrita à Onda A.
- Evidência esperada: `InventoryContractTests` verde, com a exceção do `429` documentada em vez de silenciada.

## Detalhes de Implementação

As 19 operações da Onda A:

```
getAvailability            getPropertySellability     getInventoryCalendar
getDailyInventoryDetail    listAllotments             createAllotment
getAllotment               updateAllotment            cancelAllotment
listInventoryBlocks        createInventoryBlock       previewInventoryBlockImpact
getInventoryBlock          removeInventoryBlock       listInventoryRequests
createInventoryRequest     getInventoryRequest        updateInventoryRequest
getInventoryMetrics
```

Exceção conhecida a registrar:

```csharp
// ADR-005: o 429 de getAvailability é produzido pela borda (Cloudflare + Traefik),
// não pela aplicação. Não é exercitável em teste de integração, onde não há borda.
// Registrado como exceção conhecida em vez de falhar a certificação.
private static readonly (string OperationId, int Status)[] EdgeProducedStatuses =
[
    ("getAvailability", 429)
];
```

> Silenciar a divergência com uma asserção mais frouxa esconderia o motivo. Registrá-la nomeadamente faz com que qualquer pessoa que investigue um `429` em produção encontre o rastro até ADR-005 e até o `localizestay-deploy` — que é onde o limite realmente vive.

**Convenções da stack (das skills consultadas):**

- O contrato é a fonte soberana; o teste falha quando a implementação diverge, não o contrário (`restful-api`).
- Reutilizar `OpenApiContractDocument` da F02 em vez de criar um segundo parser (`dotnet-architecture`).
- Testcontainers PostgreSQL para exercitar a superfície real (`dotnet-testing`).

## Critérios de Sucesso (Verificáveis)

- [ ] Testes passam: `dotnet test ../localizestay-backend/tests/LocalizeStay.IntegrationTests --filter "FullyQualifiedName~InventoryContractTests"`
- [ ] Build compila sem erros: `dotnet build ../localizestay-backend/LocalizeStay.sln --no-restore`
- [ ] As 19 operações da Onda A existem com método, path e `operationId` do contrato.
- [ ] Toda resposta `201` traz header `Location`; toda `204` tem corpo vazio.
- [ ] Todo erro responde `application/problem+json` com `code` e `traceId` preenchidos.
- [ ] `ALLOTMENT_BELOW_COMMITTED` e `INSUFFICIENT_FREE_BALANCE` trazem `metadata` populada.
- [ ] `getAvailability` responde 200 sem token; as demais 18 respondem 401 sem token.
- [ ] `createInventoryBlock` sem `Idempotency-Key` responde 400.
- [ ] A exceção `getAvailability` + `429` está registrada nomeadamente, com referência a ADR-005.
