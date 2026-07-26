# Resumo de Tarefas de Implementação — F02: Estruturar Acomodações, Tarifas e Políticas

## Visão Geral

Este plano implementa a F02 como capacidade vertical do módulo `Inventory` no backend .NET. O trabalho cria a propriedade incorporada canônica, o agregado `CommercialOffer`, políticas, acomodações, tarifas, validação independente, submissão idempotente, devolução, 20 operações HTTP API-first, persistência PostgreSQL, telemetria, segurança e certificação automatizada.

O plano foi dividido em 12 tarefas e quatro fases para limitar o tamanho dos incrementos e permitir paralelização depois das fundações. O contrato OpenAPI permanece a fonte soberana da superfície HTTP.

## Skills de Stack Consultadas

| Skill | Caminho | Influência |
|---|---|---|
| `dotnet-architecture` | `/home/tsgomes/.agents/skills/csharp/dotnet-architecture/SKILL.md` | CQRS nativo, domínio, DI, exceções e limites de camadas |
| `dotnet-testing` | `/home/tsgomes/.agents/skills/csharp/dotnet-testing/SKILL.md` | xUnit, AwesomeAssertions, AAA, WebApplicationFactory e PostgreSQL Testcontainers |
| `dotnet-code-quality` | `/home/tsgomes/.agents/skills/csharp/dotnet-code-quality/SKILL.md` | Naming em inglês, métodos/classes pequenos, async e `CancellationToken` |
| `dotnet-production-readiness` | `/home/tsgomes/.agents/skills/csharp/dotnet-production-readiness/SKILL.md` | Autorização, rate limiting, sanitização de logs e checklist de deploy |
| `dotnet-observability` | `/home/tsgomes/.agents/skills/csharp/dotnet-observability/SKILL.md` | Métricas, spans, logs estruturados, correlação e verificação de health checks |
| `dotnet-dependency-config` | `/home/tsgomes/.agents/skills/csharp/dotnet-dependency-config/SKILL.md` | EF Core/PostgreSQL, FluentValidation, Options, migrations e outbox |
| `dotnet-performance` | `/home/tsgomes/.agents/skills/csharp/dotnet-performance/SKILL.md` | Projeções `AsNoTracking`, paginação e índices |
| `restful-api` | `/home/tsgomes/.agents/skills/common/restful-api/SKILL.md` | OpenAPI design-first, status HTTP, `Location`, 204 e RFC 9457 |

## Decisões e Gates de Execução

- Executar os comandos de verificação a partir de viajora-meta; os caminhos da solution/projetos apontam explicitamente para ../localizestay-backend.
- Preservar os desvios aprovados na TechSpec: um assembly por módulo, handlers com `InventoryDbContext` direto, mapeamento manual, sem cache e sem projeção assíncrona.
- Aprovar `ruleSetVersion` e conteúdo jurídico antes de ativar tráfego com dinheiro real; a implementação pode usar configuração validada no startup.
- Ratificar as permissões `commercial-offers:read`, `commercial-offers:write`, `commercial-offers:review` e `commercial-offers:metrics` antes da certificação de segurança.
- Aprovar `CurationOfferReturnedV1` antes do teste ponta a ponta de devolução.
- Tratar `completeInformationReceivedAt` como o primeiro instante em que a oferta fica completa no sistema até decisão contrária.
- Manter `childAgeRangeSource = none` quando ainda não existir faixa padrão da propriedade.

## Fases de Implementação

### Fase 1 — Fundações e domínio

Sincroniza o contrato, configura segurança e catálogo jurídico, materializa `IncorporatedProperty` e implementa o núcleo do agregado. Tarefas: 1.0 a 3.0.

### Fase 2 — Incrementos comerciais paralelos

Implementa políticas, acomodações e tarifas como fatias de domínio/aplicação testáveis em paralelo após o agregado base. Tarefas: 4.0 a 6.0.

### Fase 3 — Persistência, leitura, workflow e API

Cria schema e backfill, consultas e métricas, submissão/devolução e as 20 operações Minimal API. Tarefas: 7.0 a 10.0.

### Fase 4 — Operação e certificação

Fecha telemetria, documentação e a matriz completa de contrato, persistência, segurança e fluxo ponta a ponta. Tarefas: 11.0 e 12.0.

## Tarefas

- [x] [1.0 Sincronizar contrato, catálogo jurídico, configuração e permissões](1_task.md)
- [x] [2.0 Materializar a propriedade incorporada a partir da F01](2_task.md)
- [x] [3.0 Implementar o agregado CommercialOffer, revisão e completude](3_task.md)
- [x] [4.0 Implementar políticas comerciais reutilizáveis](4_task.md)
- [x] [5.0 Implementar acomodações, ocupação e heranças](5_task.md)
- [x] [6.0 Implementar tarifas comerciais e períodos](6_task.md)
- [x] [7.0 Persistir a oferta comercial e executar migration/backfill](7_task.md)
- [x] [8.0 Implementar consultas, DTOs, histórico e métricas](8_task.md)
- [x] [9.0 Implementar validação, submissão, outbox e devolução](9_task.md)
- [x] [10.0 Expor e validar as 20 operações Minimal API](10_task.md)
- [x] [11.0 Instrumentar, documentar e preparar a operação](11_task.md)
- [ ] [12.0 Certificar contrato, segurança e fluxo ponta a ponta](12_task.md)

## Catálogo de User Stories

| ID | User Story |
|---|---|
| US-01 | Como operador, quero cadastrar acomodações e condições comerciais progressivamente para tratar informações incompletas sem perder o trabalho realizado. |
| US-02 | Como operador, quero reutilizar políticas cadastradas e definir uma política padrão por propriedade para reduzir inconsistências. |
| US-03 | Como revisor, quero conferir preços, ocupação e políticas antes do envio para evitar ofertas incorretas. |
| US-04 | Como parceiro, quero fornecer condições comerciais pelos canais já utilizados. |
| US-05 | Como gestor, quero medir prazo, completude e retrabalho para avaliar a capacidade operacional. |

## Rastreabilidade US → Tasks

| User Story | Tasks Relacionadas | Tipo de Cobertura |
|---|---|---|
| US-01 | 3.0, 5.0, 6.0, 7.0, 10.0, 12.0 | Direta |
| US-02 | 1.0, 4.0, 7.0, 10.0, 12.0 | Direta |
| US-03 | 3.0, 8.0, 9.0, 10.0, 12.0 | Direta |
| US-04 | 2.0, 8.0, 9.0 | Suporte; reutiliza registros humanos da F01 |
| US-05 | 8.0, 11.0, 12.0 | Direta |

## Cobertura das Operações do Contrato

| Operação | Implementação principal | Certificação |
|---|---|---|
| `listCommercialOffers` | 8.0, 10.0 | 12.0 |
| `getCommercialOffer` | 3.0, 8.0, 10.0 | 12.0 |
| `listCommercialPolicies` | 4.0, 8.0, 10.0 | 12.0 |
| `createCommercialPolicy` | 4.0, 10.0 | 12.0 |
| `setDefaultCommercialPolicy` | 4.0, 10.0 | 12.0 |
| `updateCommercialPolicy` | 4.0, 10.0 | 12.0 |
| `deleteCommercialPolicy` | 4.0, 10.0 | 12.0 |
| `listAccommodations` | 5.0, 8.0, 10.0 | 12.0 |
| `createAccommodation` | 5.0, 10.0 | 12.0 |
| `getAccommodation` | 5.0, 8.0, 10.0 | 12.0 |
| `updateAccommodation` | 5.0, 10.0 | 12.0 |
| `deleteAccommodation` | 5.0, 10.0 | 12.0 |
| `listCommercialRates` | 6.0, 8.0, 10.0 | 12.0 |
| `createCommercialRate` | 6.0, 10.0 | 12.0 |
| `updateCommercialRate` | 6.0, 10.0 | 12.0 |
| `deleteCommercialRate` | 6.0, 10.0 | 12.0 |
| `createCommercialOfferValidation` | 9.0, 10.0 | 12.0 |
| `createCommercialOfferSubmission` | 9.0, 10.0 | 12.0 |
| `listCommercialOfferHistory` | 8.0, 10.0 | 12.0 |
| `getCommercialOfferMetrics` | 8.0, 10.0 | 12.0 |

## Validação de Cobertura

### Requisitos Funcionais

| Requisito | Task(s) | Status |
|---|---|---|
| RF-01 — Manter políticas da propriedade | 1.0, 3.0, 4.0, 7.0, 10.0, 12.0 | ✅ Coberto |
| RF-02 — Estruturar acomodações e ocupação | 3.0, 5.0, 7.0, 10.0, 12.0 | ✅ Coberto |
| RF-03 — Definir tarifas comerciais | 3.0, 6.0, 7.0, 10.0, 12.0 | ✅ Coberto |
| RF-04 — Gerenciar rascunhos e pendências | 3.0, 4.0, 5.0, 6.0, 8.0, 10.0, 12.0 | ✅ Coberto |
| RF-05 — Validar e enviar a oferta | 3.0, 7.0, 9.0, 10.0, 11.0, 12.0 | ✅ Coberto |
| RF-06 — Corrigir ofertas devolvidas | 3.0, 8.0, 9.0, 10.0, 12.0 | ✅ Coberto |

### Artefatos da TechSpec

| Artefato | Task | Status |
|---|---:|---|
| `../localizestay-backend/.tasks/prd-estruturar-acomodacoes-tarifas-e-politicas/api-contract.yaml` | 1.0 | ✅ |
| `../localizestay-backend/tests/LocalizeStay.UnitTests/Inventory/LegalPolicyCatalogTests.cs` | 1.0 | ✅ |
| `../localizestay-backend/tests/LocalizeStay.IntegrationTests/Inventory/SecurityAndProblemDetailsTests.cs` | 1.0 | ✅ |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Domain/IncorporatedProperties/IncorporatedProperty.cs` | 2.0 | ✅ |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Domain/CommercialOffers/CommercialOffer.cs` | 3.0 | ✅ |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Domain/CommercialOffers/CommercialPolicy.cs` | 4.0 | ✅ |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Domain/CommercialOffers/Accommodation.cs` | 5.0 | ✅ |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Domain/CommercialOffers/CommercialRate.cs` | 6.0 | ✅ |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Domain/CommercialOffers/OfferValidation.cs` | 3.0 | ✅ |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Domain/CommercialOffers/OfferSubmission.cs` | 3.0, 9.0 | ✅ |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Domain/CommercialOffers/OfferReturn.cs` | 3.0, 9.0 | ✅ |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Domain/CommercialOffers/CommercialOfferValues.cs` | 3.0 | ✅ |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Domain/CommercialOffers/CommercialOfferCompleteness.cs` | 3.0 | ✅ |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Domain/CommercialOffers/CommercialOfferIdempotencyKey.cs` | 3.0, 9.0 | ✅ |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/LegalPolicies/ILegalPolicyCatalog.cs` | 1.0 | ✅ |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/CommercialOffers/CommercialOfferCommands.cs` | 3.0, 10.0 | ✅ |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/CommercialOffers/CommercialPolicyCommands.cs` | 4.0 | ✅ |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/CommercialOffers/AccommodationCommands.cs` | 5.0 | ✅ |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/CommercialOffers/CommercialRateCommands.cs` | 6.0 | ✅ |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/CommercialOffers/CommercialOfferWorkflowCommands.cs` | 9.0 | ✅ |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/CommercialOffers/CommercialOfferQueries.cs` | 8.0 | ✅ |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/CommercialOffers/CommercialOfferDtos.cs` | 8.0 | ✅ |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/CommercialOffers/CommercialOfferMapper.cs` | 8.0 | ✅ |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/CommercialOffers/CommercialOfferValidators.cs` | 10.0 | ✅ |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/CommercialOffers/CurationOfferReturnedHandler.cs` | 9.0 | ✅ |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Infrastructure/LegalPolicies/ConfiguredLegalPolicyCatalog.cs` | 1.0 | ✅ |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Infrastructure/Configurations/IncorporatedPropertyConfiguration.cs` | 7.0 | ✅ |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Infrastructure/Configurations/CommercialOfferConfiguration.cs` | 7.0 | ✅ |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Infrastructure/Configurations/CommercialPolicyConfiguration.cs` | 7.0 | ✅ |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Infrastructure/Configurations/AccommodationConfiguration.cs` | 7.0 | ✅ |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Infrastructure/Configurations/CommercialRateConfiguration.cs` | 7.0 | ✅ |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Infrastructure/Configurations/OfferValidationConfiguration.cs` | 7.0 | ✅ |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Infrastructure/Configurations/OfferSubmissionConfiguration.cs` | 7.0 | ✅ |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Infrastructure/Configurations/OfferReturnConfiguration.cs` | 7.0 | ✅ |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Infrastructure/Configurations/CommercialOfferIdempotencyKeyConfiguration.cs` | 7.0 | ✅ |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Infrastructure/Migrations/[timestamp]_AddCommercialOffers.cs` | 7.0 | ✅ |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Infrastructure/Migrations/[timestamp]_AddCommercialOffers.Designer.cs` | 7.0 | ✅ |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Endpoints/CommercialOfferEndpoints.cs` | 10.0 | ✅ |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Endpoints/CommercialPolicyEndpoints.cs` | 10.0 | ✅ |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Endpoints/AccommodationEndpoints.cs` | 10.0 | ✅ |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Endpoints/CommercialRateEndpoints.cs` | 10.0 | ✅ |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Endpoints/CommercialOfferWorkflowEndpoints.cs` | 10.0 | ✅ |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Endpoints/CommercialOfferMetricsEndpoints.cs` | 10.0 | ✅ |
| `../localizestay-backend/src/Modules/Curation/LocalizeStay.Modules.Curation.Contracts/CurationIntegrationEvents.cs` | 9.0 | ✅ |
| `../localizestay-backend/tests/LocalizeStay.IntegrationTests/Infrastructure/OpenApiContractDocument.cs` | 12.0 | ✅ |
| `../localizestay-backend/tests/LocalizeStay.UnitTests/Inventory/IncorporatedPropertyTests.cs` | 2.0 | ✅ |
| `../localizestay-backend/tests/LocalizeStay.UnitTests/Inventory/CommercialOfferTests.cs` | 3.0 | ✅ |
| `../localizestay-backend/tests/LocalizeStay.UnitTests/Inventory/CommercialPolicyTests.cs` | 4.0 | ✅ |
| `../localizestay-backend/tests/LocalizeStay.UnitTests/Inventory/AccommodationTests.cs` | 5.0 | ✅ |
| `../localizestay-backend/tests/LocalizeStay.UnitTests/Inventory/CommercialRateTests.cs` | 6.0 | ✅ |
| `../localizestay-backend/tests/LocalizeStay.UnitTests/Inventory/CommercialOfferWorkflowTests.cs` | 9.0 | ✅ |
| `../localizestay-backend/tests/LocalizeStay.UnitTests/Inventory/CommercialOfferCommandHandlerTests.cs` | 9.0 | ✅ |
| `../localizestay-backend/tests/LocalizeStay.UnitTests/Inventory/CommercialOfferMetricsQueryHandlerTests.cs` | 8.0 | ✅ |
| `../localizestay-backend/tests/LocalizeStay.UnitTests/Inventory/CurationOfferReturnedHandlerTests.cs` | 9.0 | ✅ |
| `../localizestay-backend/tests/LocalizeStay.IntegrationTests/Inventory/CommercialOfferApiContractTests.cs` | 12.0 | ✅ |
| `../localizestay-backend/tests/LocalizeStay.IntegrationTests/Inventory/CommercialOfferPersistenceTests.cs` | 7.0 | ✅ |
| `../localizestay-backend/tests/LocalizeStay.IntegrationTests/Inventory/CommercialOfferEndpointsTests.cs` | 10.0, 12.0 | ✅ |
| `../localizestay-backend/tests/LocalizeStay.IntegrationTests/Inventory/CommercialOfferWorkflowTests.cs` | 12.0 | ✅ |
| `../localizestay-backend/tests/LocalizeStay.IntegrationTests/Inventory/CommercialOfferOutboxAndAuditTests.cs` | 12.0 | ✅ |
| `../localizestay-backend/tests/LocalizeStay.IntegrationTests/Inventory/CommercialOfferMetricsTests.cs` | 12.0 | ✅ |
| `../localizestay-backend/tests/LocalizeStay.IntegrationTests/Inventory/CommercialOfferSecurityTests.cs` | 12.0 | ✅ |
| `../localizestay-backend/tests/LocalizeStay.IntegrationTests/Inventory/CommercialOfferEndToEndTests.cs` | 12.0 | ✅ |
| `../localizestay-backend/docs/runbooks/commercial-offers.md` | 11.0 | ✅ |
| `../localizestay-backend/tests/LocalizeStay.IntegrationTests/Inventory/CommercialOfferObservabilityTests.cs` | 11.0 | ✅ |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/InventoryModule.cs` | 1.0, 10.0 | ✅ |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Infrastructure/InventoryDbContext.cs` | 7.0 | ✅ |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Infrastructure/Migrations/InventoryDbContextModelSnapshot.cs` | 7.0 | ✅ |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/PropertyOnboardings/PropertyOnboardingCommands.cs` | 2.0 | ✅ |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/Timing/IBusinessCalendar.cs` | 8.0 | ✅ |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Infrastructure/Timing/ConfiguredBusinessCalendar.cs` | 8.0 | ✅ |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/Observability/InventoryTelemetry.cs` | 11.0 | ✅ |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory.Contracts/InventoryIntegrationEvents.cs` | 9.0 | ✅ |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/LocalizeStay.Modules.Inventory.csproj` | 9.0 | ✅ |
| `../localizestay-backend/src/BuildingBlocks/LocalizeStay.SharedKernel/Security/PermissionRequirement.cs` | 1.0 | ✅ |
| `../localizestay-backend/src/BuildingBlocks/LocalizeStay.SharedKernel/Security/SecurityServiceCollectionExtensions.cs` | 1.0 | ✅ |
| `../localizestay-backend/src/LocalizeStay.Api/appsettings.json` | 1.0 | ✅ |
| `../localizestay-backend/tests/LocalizeStay.IntegrationTests/Inventory/ApiContractTests.cs` | 12.0 | ✅ |
| `../localizestay-backend/README.md` | 11.0 | ✅ |

### Categorias Obrigatórias

| # | Categoria | Task(s) / N/A | Skill Relacionada | Status |
|---|---|---|---|---|
| 1 | Setup / Configuração | 1.0, 7.0, 11.0 | `dotnet-dependency-config` | ✅ |
| 2 | Modelos de Dados | 2.0 a 7.0 | `dotnet-architecture` | ✅ |
| 3 | Lógica de Negócio | 3.0 a 9.0 | `dotnet-architecture` | ✅ |
| 4 | Endpoints / Interfaces | 10.0 e 12.0 | `restful-api` | ✅ |
| 5 | Integrações Externas | 2.0 e 9.0; F01, Curadoria e outbox. WhatsApp/e-mail não recebem integração automática | `dotnet-dependency-config` | ✅ |
| 6 | Validações e Erros | 3.0 a 10.0 | `dotnet-code-quality` | ✅ |
| 7 | Testes | Subtarefas em 1.0 a 11.0; certificação em 12.0 | `dotnet-testing` | ✅ |
| 8 | Observabilidade | 9.0 e 11.0 | `dotnet-observability` | ✅ |
| 9 | Documentação | 1.0, 11.0 e 12.0 | — | ✅ |
| 10 | Segurança | 1.0, 10.0 e 12.0 | `dotnet-production-readiness` | ✅ |

## Análise de Paralelização

### Lanes de Execução Paralela

| Lane | Tarefas | Descrição |
|---|---|---|
| Lane A — Domínio | 2.0 → 3.0 → 4.0/5.0/6.0 | Fundações do agregado e três incrementos comerciais paralelos |
| Lane B — Plataforma | 1.0 → 7.0 | Contrato/configuração e persistência PostgreSQL |
| Lane C — Aplicação | 7.0 → 8.0/9.0 → 10.0 | Queries, workflow e superfície HTTP após domínio persistível |
| Lane D — Qualidade | 7.0 → 11.0; 10.0/11.0 → 12.0 | Observabilidade, documentação e certificação final |

Não há duplicação arquitetural: todas as mutações convergem no mesmo `CommercialOffer`, consultas usam projeções e nenhum repositório genérico novo é criado.

### Caminho Crítico

`1.0 → 2.0 → 3.0 → 4.0/5.0/6.0 → 7.0 → 9.0 → 10.0 → 12.0`

As tarefas 4.0, 5.0 e 6.0 podem avançar em paralelo após 3.0. A tarefa 8.0 pode avançar em paralelo com 9.0 após 7.0. A tarefa 11.0 pode começar após 7.0 e fechar após 9.0.

### Diagrama de Dependências

~~~text
1.0 ──┬──> 2.0 ──> 3.0 ──┬──> 4.0 ──┐
      │                   ├──> 5.0 ──┼──> 7.0 ──┬──> 8.0 ──┐
      │                   └──> 6.0 ──┘          ├──> 9.0 ──┼──> 10.0 ──┐
      └────────────────────────────────────────>│          └──> 11.0 ──┼──> 12.0
                                                └───────────────────────┘
~~~
