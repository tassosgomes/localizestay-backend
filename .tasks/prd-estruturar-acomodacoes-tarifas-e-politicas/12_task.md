---
status: pending
parallelizable: false
blocked_by: ["10.0", "11.0"]
---

<task_context>
<domain>inventory/testing/commercial-offers</domain>
<type>testing</type>
<scope>core_feature</scope>
<complexity>high</complexity>
<dependencies>database,http_server,external_apis</dependencies>
<unblocks>"release"</unblocks>
</task_context>

# Tarefa 12.0: Certificar contrato, segurança e fluxo ponta a ponta

## Relacionada às User Stories

- [US-01] Operar rascunhos, correções e reenvios (direta)
- [US-02] Operar políticas consistentes (direta)
- [US-03] Garantir dupla validação antes do envio (direta)
- [US-05] Confiar nas métricas operacionais (direta)

## Visão Geral

Executar a certificação cruzada da F02 com PostgreSQL Testcontainers e WebApplicationFactory. O conjunto valida o OpenAPI, migration, endpoints, atomicidade, permissões, workflow, métricas e o fluxo F01 → F02 → validação → envio → devolução → correção → reenvio.

## Requisitos

- Parser OpenAPI reutilizável deve suportar GET, POST, PUT, PATCH e DELETE.
- Certificar exatamente 20 `operationId`, schemas, requests, responses, `Location` e 204.
- Exercitar 400, 401, 403, 404, 409, 422, 429 e 500 em requests reais.
- Certificar transação única de estado, snapshot, auditoria, idempotência e outbox.
- Testar criação concorrente do primeiro draft e conflitos de revisão.
- Testar quatro permissões e a regra adicional de revisor diferente do autor.
- Cobrir migration/backfill, JSONB, paginação, filtros, listas vazias e métricas.
- Executar E2E completo, incluindo devolução duplicada/fora de ordem e reenvio.

## Arquivos Envolvidos

- **Criar:**
  - `../localizestay-backend/tests/LocalizeStay.IntegrationTests/Infrastructure/OpenApiContractDocument.cs`
  - `../localizestay-backend/tests/LocalizeStay.IntegrationTests/Inventory/CommercialOfferApiContractTests.cs`
  - `../localizestay-backend/tests/LocalizeStay.IntegrationTests/Inventory/CommercialOfferWorkflowTests.cs`
  - `../localizestay-backend/tests/LocalizeStay.IntegrationTests/Inventory/CommercialOfferOutboxAndAuditTests.cs`
  - `../localizestay-backend/tests/LocalizeStay.IntegrationTests/Inventory/CommercialOfferMetricsTests.cs`
  - `../localizestay-backend/tests/LocalizeStay.IntegrationTests/Inventory/CommercialOfferSecurityTests.cs`
  - `../localizestay-backend/tests/LocalizeStay.IntegrationTests/Inventory/CommercialOfferEndToEndTests.cs`
- **Modificar:**
  - `../localizestay-backend/tests/LocalizeStay.IntegrationTests/Inventory/CommercialOfferEndpointsTests.cs` (ampliar matriz HTTP)
  - `../localizestay-backend/tests/LocalizeStay.IntegrationTests/Inventory/CommercialOfferPersistenceTests.cs` (integrar à suíte)
  - `../localizestay-backend/tests/LocalizeStay.IntegrationTests/Inventory/ApiContractTests.cs` (extrair parser sem regredir F01)
- **Referência:**
  - `../localizestay-backend/.tasks/prd-estruturar-acomodacoes-tarifas-e-politicas/api-contract.yaml`
  - `../localizestay-backend/tests/LocalizeStay.IntegrationTests/Infrastructure/LocalizeStayWebApplicationFactory.cs`
  - `../localizestay-backend/tests/LocalizeStay.UnitTests/Inventory/CommercialOfferWorkflowTests.cs`
  - `tasks/prd-estruturar-acomodacoes-tarifas-e-politicas/prd.md`
- **Skills para consultar durante implementação:**
  - `dotnet-testing` — WebApplicationFactory, Testcontainers, AAA e isolamento
  - `restful-api` — certificação OpenAPI/RFC 9457
  - `dotnet-production-readiness` — segurança, rate limit e smoke checks
  - `dotnet-observability` — correlação e falhas de outbox
  - `dotnet-performance` — paginação, filtros e índices

## Subtarefas

- [ ] 12.1 Extrair `OpenApiContractDocument` e manter todos os 18 testes/operações da F01 verdes.
- [ ] 12.2 Certificar metadados e schemas das 20 operações F02 contra o YAML.
- [ ] 12.3 Ampliar testes de endpoints para requests/responses e todos os status reais.
- [ ] 12.4 Certificar migration, JSONB, constraints, índices e backfill no PostgreSQL.
- [ ] 12.5 Certificar validação, invalidação, submissão, devolução, correção e reenvio.
- [ ] 12.6 Certificar atomicidade de estado/snapshot/auditoria/outbox e rollback em conflito.
- [ ] 12.7 Certificar métricas com calendário, numeradores, denominadores e reprocessamento.
- [ ] 12.8 Certificar 401/403, quatro permissões, segregação de função e 429.
- [ ] 12.9 Executar o E2E completo e validar o evento `oferta-inventario.oferta-estruturada`.
- [ ] 12.10 Executar build, format e suíte integral sem testes ignorados.

## Sequenciamento

- Bloqueado por: 10.0 e 11.0
- Desbloqueia: release/piloto da F02
- Paralelizável: Não; é o gate final e precisa da superfície integrada.

## Rastreabilidade

- Esta tarefa cobre: US-01, US-02, US-03 e US-05 diretamente; RF-01 a RF-06.
- Evidência esperada: suíte verde em PostgreSQL real, contrato certificado e evento/snapshot/auditoria coerentes.

## Detalhes de Implementação

O parser compartilhado deve ler operações, métodos, paths, request schemas, response schemas/content types, status e headers. A extração não pode mudar a cobertura da F01. A suíte F02 deve derivar a matriz do YAML sempre que possível, evitando listas duplicadas que possam divergir.

Matriz mínima E2E:

1. criar/submeter onboarding F01 e materializar `IncorporatedProperty`;
2. abrir draft F02;
3. criar políticas e padrão;
4. criar acomodação e tarifa completa;
5. validar com segundo operador;
6. submeter com idempotência;
7. conferir snapshot, auditoria e outbox;
8. consumir devolução, corrigir e exigir nova validação;
9. reenviar e confirmar histórico/métricas.

**Convenções da stack (das skills consultadas):**

- Usar PostgreSQL Testcontainers; nunca EF InMemory para persistência/integração.
- Testes seguem AAA, AwesomeAssertions e naming em inglês.
- Isolar dados por teste e limpar recursos; evitar dependência de ordem.
- Claims/JWT de teste devem representar permissões mínimas.
- Testes E2E backend usam HttpClient da WebApplicationFactory; Playwright não é necessário sem frontend neste escopo.

## Critérios de Sucesso (Verificáveis)

- [ ] Suíte F02 passa: `dotnet test ../localizestay-backend/LocalizeStay.sln --filter "FullyQualifiedName~CommercialOffer"`
- [ ] Suíte integral passa: `dotnet test ../localizestay-backend/LocalizeStay.sln --no-restore`
- [ ] Build Release passa: `dotnet build ../localizestay-backend/LocalizeStay.sln -c Release --no-restore`
- [ ] Formatação válida: `dotnet format ../localizestay-backend/LocalizeStay.sln --verify-no-changes --no-restore`
- [ ] O parser encontra exatamente 20 operações F02 e preserva as 18 operações F01.
- [ ] Cada status declarado possui pelo menos um cenário real ou certificação de metadado explícita.
- [ ] Fluxo E2E produz uma única outbox por submissão idempotente e preserva histórico após devolução/reenvio.
- [ ] Nenhum teste novo está skipped e não há dependência de ordem ou banco externo.
