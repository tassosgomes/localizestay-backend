# Especificação Técnica — F02: Estruturar Acomodações, Tarifas e Políticas

> **Modo de operação:** API-First  
> **PRD de origem:** `tasks/prd-estruturar-acomodacoes-tarifas-e-politicas/prd.md`  
> **API Contract:** `tasks/prd-estruturar-acomodacoes-tarifas-e-politicas/api-contract.yaml`  
> **Data:** 2026-07-22  
> **Status:** Aprovado

---

## Resumo Executivo

A F02 será implementada como capacidade vertical do módulo `Inventory`. Uma nova entidade `IncorporatedProperty`, identificada pelo mesmo UUID de `PropertyOnboarding`, estabelecerá a propriedade canônica produzida pela F01. Cada propriedade terá um único agregado `CommercialOffer`, responsável por políticas, acomodações, tarifas, revisão otimista, validação independente, envios e devoluções.

Minimal APIs mapearão os schemas HTTP para Commands e Queries internos. O agregado persistirá em tabelas normalizadas do schema `inventory`; campos-resumo da oferta serão atualizados na mesma transação para sustentar fila, filtros e métricas sem cache ou projeção assíncrona. O envio gravará snapshot, auditoria e `oferta-inventario.oferta-estruturada` na outbox transacional.

O trade-off primário é carregar e serializar mutações pelo agregado da oferta, reduzindo concorrência independente entre políticas, acomodações e tarifas. Em troca, `revision`, invalidação da validação, histórico e regras comerciais permanecem fortemente consistentes. A separação por módulo, contratos e query handlers preserva a possibilidade de extração futura sem introduzir consistência eventual antes de existir evidência operacional.

---

## Skills de Referência

| Skill | Caminho | Decisões Influenciadas |
|---|---|---|
| `dotnet-architecture` | `/home/tsgomes/.agents/skills/csharp/dotnet-architecture/SKILL.md` | CQRS nativo, domínio, exceções e limites de componentes |
| `dotnet-dependency-config` | `/home/tsgomes/.agents/skills/csharp/dotnet-dependency-config/SKILL.md` | EF Core, PostgreSQL, FluentValidation, migrations e outbox |
| `dotnet-code-quality` | `/home/tsgomes/.agents/skills/csharp/dotnet-code-quality/SKILL.md` | Naming, DI, assincronismo e `CancellationToken` |
| `dotnet-testing` | `/home/tsgomes/.agents/skills/csharp/dotnet-testing/SKILL.md` | xUnit, AwesomeAssertions, WebApplicationFactory e Testcontainers |
| `dotnet-observability` | `/home/tsgomes/.agents/skills/csharp/dotnet-observability/SKILL.md` | Logs estruturados, métricas e tracing |
| `dotnet-performance` | `/home/tsgomes/.agents/skills/csharp/dotnet-performance/SKILL.md` | Projeções EF, `AsNoTracking`, paginação e índices |
| `restful-api` | `/home/tsgomes/.agents/skills/common/restful-api/SKILL.md` | OpenAPI, versionamento, paginação e RFC 9457 |

---

## Arquitetura do Sistema

### Visão Geral dos Componentes

- `IncorporatedProperty` representa a propriedade canônica criada pela F01. Seu identificador é igual ao `PropertyOnboarding.Id` que originou a incorporação.
- `CommercialOffer` é o aggregate root da F02 e concentra `revision`, autor da revisão, estado, completude, prazos e validação vigente.
- `CommercialPolicy`, `Accommodation` e `CommercialRate` são entidades filhas persistidas em tabelas próprias.
- `OfferValidation`, `OfferSubmission` e `OfferReturn` preservam evidências imutáveis do workflow.
- Endpoints Minimal API convertem requests do OpenAPI em Commands/Queries internos.
- Handlers usam diretamente `InventoryDbContext`, seguindo o padrão vigente. Não será criado repositório genérico.
- `ILegalPolicyCatalog` resolve textos e versões jurídicas aprovadas sem aceitar texto livre do cliente.
- `BusinessAuditEntry` sustenta o histórico funcional, enquanto snapshots de envio preservam a oferta efetivamente encaminhada.
- `InventoryDbContext` grava estado, auditoria, idempotência e outbox na mesma transação.
- Queries usam projeções EF Core sem tracking. Não haverá Redis, banco adicional ou projeção assíncrona no MVP.

### Diagrama de Componentes

```text
JWT LogTo + policies
        |
Minimal APIs /api/v1
        |
        v
Commands / Queries
        |
        v
CommercialOffer aggregate ---- ILegalPolicyCatalog
        |
        v
InventoryDbContext (schema inventory)
  ├─ incorporated_properties
  ├─ commercial_offers
  ├─ commercial_policies
  ├─ accommodations
  ├─ commercial_rates
  ├─ offer_validations
  ├─ offer_submissions + snapshot JSONB
  ├─ offer_returns
  ├─ commercial_offer_idempotency_keys
  ├─ business_audit_entries
  └─ outbox_messages
           |
           └─ oferta-inventario.oferta-estruturada
```

---

## Design de Implementação

### Interfaces Principais

```csharp
internal interface ILegalPolicyCatalog
{
    CommercialPolicyRuleSet GetCurrent(PolicyType policyType);
}

internal sealed record CommercialPolicyRuleSet(
    PolicyType Type,
    string Title,
    string RulesSummary,
    string Version);
```

O consumidor de devoluções implementará a interface transversal já existente:

```csharp
internal sealed class CurationOfferReturnedHandler
    : IIntegrationEventHandler<CurationOfferReturnedV1>
{
    public Task HandleAsync(
        CurationOfferReturnedV1 integrationEvent,
        CancellationToken cancellationToken);
}
```

### Modelos de Dados

| Entidade de negócio | Modelo técnico | Persistência |
|---|---|---|
| Propriedade | `IncorporatedProperty` | `inventory.incorporated_properties` |
| Oferta comercial | `CommercialOffer` | `inventory.commercial_offers` |
| Política da Propriedade | `CommercialPolicy` | `inventory.commercial_policies` |
| Acomodação | `Accommodation` | `inventory.accommodations` |
| Tarifa Comercial | `CommercialRate` | `inventory.commercial_rates` |
| Validação | `OfferValidation` | `inventory.offer_validations` |
| Envio | `OfferSubmission` | `inventory.offer_submissions` |
| Devolução | `OfferReturn` | `inventory.offer_returns` |

Decisões de persistência:

- `IncorporatedProperty.Id` será igual ao `PropertyOnboarding.Id`.
- `CommercialOffer.PropertyId` será chave primária e chave estrangeira para `IncorporatedProperty`.
- `CommercialOffer.Revision` será concurrency token do EF Core.
- Toda mutação comercial incrementará a revisão uma única vez, atualizará o autor da revisão e invalidará a validação vigente.
- Estado, completude, quantidade de bloqueios, quantidade de acomodações e prazo serão campos-resumo do agregado.
- Pendências detalhadas serão recalculadas pelo domínio; somente os contadores necessários à fila serão persistidos.
- Configuração de camas será persistida como JSONB; características estruturais, como coleção de valores semânticos da acomodação.
- Faixa etária infantil terá colunas explícitas e indicação da origem: padrão da propriedade, override ou ausência.
- Valores monetários serão `long` em centavos. Moeda `BRL` e taxas obrigatórias incluídas serão invariantes do servidor.
- Datas tarifárias usarão `DateOnly`; instantes de auditoria e workflow usarão `DateTimeOffset` UTC.
- `OfferSubmission.SnapshotJson` armazenará JSONB imutável, versionado e independente dos DTOs HTTP.
- Histórico será projetado de `business_audit_entries`; não haverá segunda tabela de timeline.
- Uma chave de idempotência será única globalmente no escopo de submissões comerciais e armazenará fingerprint do payload.
- Índices cobrirão:
  - `commercial_offers(status, target_submission_at)`;
  - `commercial_offers(property_id)`;
  - `commercial_policies(property_id, type, status)`;
  - `accommodations(property_id, status)`;
  - `commercial_rates(accommodation_id, condition_code, policy_id, meal_plan, valid_from, valid_to)`;
  - `offer_submissions(property_id, revision)`;
  - chave de idempotência e identificador do evento.
- Todas as entidades comerciais pertencem ao schema `inventory`; nenhum join ou FK atravessará módulos.

DTOs HTTP serão records internos próximos aos endpoints. Commands e Queries usarão records próprios da camada de aplicação. O mapeamento será manual e explícito, seguindo o padrão existente e evitando acoplar o domínio aos schemas OpenAPI.

### Regras do Agregado

- A oferta nasce em `draft`.
- O primeiro `GET` após a incorporação cria o rascunho de modo idempotente, conforme `x-backend-notes`.
- Criação concorrente do primeiro rascunho será protegida pela unicidade de `property_id`; o perdedor recarrega a oferta criada.
- O autor inicial será o operador registrado na incorporação. Cada mutação posterior passa a autoria da revisão ao operador autenticado.
- A oferta passa a `readyForValidation` quando houver pelo menos uma acomodação completa com tarifa ativa atual ou futura.
- Validação exige revisor diferente do autor da revisão e `expectedRevision` igual à revisão atual.
- Qualquer alteração de preço, ocupação, política ou período marca a validação anterior como `invalidated`.
- Envio exige uma validação válida da mesma revisão.
- Envio marca como `everSubmitted` todos os recursos incluídos no snapshot.
- Hard delete só será permitido quando o recurso nunca tiver sido enviado.
- Oferta `published` rejeitará mutações da F02 com `PUBLISHED_OFFER_CHANGE_REQUIRES_F04`.
- A sobreposição tarifária será verificada em intervalos inclusivos para a mesma acomodação, `conditionCode`, política e regime alimentar.
- Toda criação ou mutação também atualizará a linha do aggregate root. O concurrency token impedirá que duas alterações concorrentes confirmem invariantes calculadas sobre a mesma revisão.
- O instante em que a oferta se torna comercialmente completa pela primeira vez preencherá `completeInformationReceivedAt`; o prazo será calculado com dois dias úteis.

### Endpoints de API

> Os endpoints, schemas, autenticação, paginação e formato de erros são definidos no [API Contract](api-contract.yaml). Esta TechSpec não duplica essas definições.

| operationId | Caminho de Implementação |
|---|---|
| `listCommercialOffers` | `CommercialOfferEndpoints.ListAsync` → `ListCommercialOffersQueryHandler` → projeção de `CommercialOffers` |
| `getCommercialOffer` | `CommercialOfferEndpoints.GetAsync` → `GetCommercialOfferQueryHandler` → `IncorporatedProperty` → criação idempotente → `CommercialOfferMapper` |
| `listCommercialPolicies` | `CommercialPolicyEndpoints.ListAsync` → `ListCommercialPoliciesQueryHandler` → projeção EF |
| `createCommercialPolicy` | `CommercialPolicyEndpoints.CreateAsync` → `CreateCommercialPolicyCommandHandler` → `ILegalPolicyCatalog` → `CommercialOffer.AddPolicy` |
| `setDefaultCommercialPolicy` | `CommercialPolicyEndpoints.SetDefaultAsync` → `SetDefaultCommercialPolicyCommandHandler` → `CommercialOffer.SetDefaultPolicy` |
| `updateCommercialPolicy` | `CommercialPolicyEndpoints.UpdateAsync` → `UpdateCommercialPolicyCommandHandler` → substituição/desativação no agregado |
| `deleteCommercialPolicy` | `CommercialPolicyEndpoints.DeleteAsync` → `DeleteCommercialPolicyCommandHandler` → hard delete protegido |
| `listAccommodations` | `AccommodationEndpoints.ListAsync` → `ListAccommodationsQueryHandler` → projeção EF paginada |
| `createAccommodation` | `AccommodationEndpoints.CreateAsync` → `CreateAccommodationCommandHandler` → herança da propriedade/oferta |
| `getAccommodation` | `AccommodationEndpoints.GetAsync` → `GetAccommodationQueryHandler` → projeção com pendências |
| `updateAccommodation` | `AccommodationEndpoints.UpdateAsync` → `UpdateAccommodationCommandHandler` → invariantes de ocupação |
| `deleteAccommodation` | `AccommodationEndpoints.DeleteAsync` → `DeleteAccommodationCommandHandler` → hard delete protegido |
| `listCommercialRates` | `CommercialRateEndpoints.ListAsync` → `ListCommercialRatesQueryHandler` → projeção EF paginada |
| `createCommercialRate` | `CommercialRateEndpoints.CreateAsync` → `CreateCommercialRateCommandHandler` → `CommercialOffer.AddRate` |
| `updateCommercialRate` | `CommercialRateEndpoints.UpdateAsync` → `UpdateCommercialRateCommandHandler` → `CommercialOffer.UpdateRate` |
| `deleteCommercialRate` | `CommercialRateEndpoints.DeleteAsync` → `DeleteCommercialRateCommandHandler` → hard delete protegido |
| `createCommercialOfferValidation` | `CommercialOfferWorkflowEndpoints.ValidateAsync` → `CreateOfferValidationCommandHandler` → `CommercialOffer.Validate` |
| `createCommercialOfferSubmission` | `CommercialOfferWorkflowEndpoints.SubmitAsync` → `SubmitCommercialOfferCommandHandler` → snapshot + auditoria + outbox |
| `listCommercialOfferHistory` | `CommercialOfferWorkflowEndpoints.HistoryAsync` → `ListCommercialOfferHistoryQueryHandler` → `BusinessAuditEntries` |
| `getCommercialOfferMetrics` | `CommercialOfferMetricsEndpoints.GetAsync` → `GetCommercialOfferMetricsQueryHandler` → agregação reprocessável |

### Validações Adicionais

| Endpoint/operação | Validação | Local |
|---|---|---|
| Todas as mutações | Propriedade incorporada, oferta não publicada e escopo correto do recurso | handler/agregado |
| `createCommercialPolicy` | Um único tipo ativo por propriedade; regra jurídica obtida do catálogo | domínio/infraestrutura |
| `setDefaultCommercialPolicy` | Política ativa da mesma propriedade e revisão esperada | agregado |
| `updateCommercialPolicy` | Política em uso exige substituta ativa diferente | agregado |
| Exclusões | `everSubmitted == false`, sem uso e sem condição de padrão | agregado |
| `createAccommodation` | Herança explícita de política e faixa infantil quando disponíveis | agregado |
| `updateAccommodation` | Capacidade, adultos, crianças, camas e faixa infantil coerentes | agregado |
| Criação/alteração de tarifa | Período inclusivo válido, hóspedes incluídos compatíveis e ausência de sobreposição | agregado |
| `createCommercialOfferValidation` | Oferta pronta, revisão atual e revisor diferente do autor | agregado |
| `createCommercialOfferSubmission` | Validação válida da mesma revisão e idempotência por fingerprint | handler/agregado |
| Evento de devolução | `submissionId`, propriedade e revisão correspondem a envio existente | consumer/agregado |
| Métricas | Intervalo válido, calendário oficial e denominadores explícitos | query handler |

### Mapeamento de Exceções para Problem Details

| Condição | HTTP | `code` |
|---|---:|---|
| Propriedade não encontrada | 404 | `PROPERTY_NOT_FOUND` |
| Política não encontrada | 404 | `POLICY_NOT_FOUND` |
| Acomodação não encontrada | 404 | `ACCOMMODATION_NOT_FOUND` |
| Tarifa não encontrada | 404 | `RATE_NOT_FOUND` |
| Revisão concorrente | 409 | `REVISION_MISMATCH` |
| Tipo de política ativo duplicado | 409 | `POLICY_TYPE_ALREADY_ACTIVE` |
| Sobreposição tarifária | 409 | `RATE_PERIOD_OVERLAP` |
| Chave idempotente com outro payload | 409 | `IDEMPOTENCY_KEY_REUSED` |
| Ocupação incoerente | 422 | `INVALID_OCCUPANCY_CONFIGURATION` |
| Política em uso sem substituta | 422 | `REPLACEMENT_POLICY_REQUIRED` |
| Exclusão de política não permitida | 422 | `POLICY_DELETION_NOT_ALLOWED` |
| Exclusão de acomodação não permitida | 422 | `ACCOMMODATION_DELETION_NOT_ALLOWED` |
| Exclusão de tarifa não permitida | 422 | `RATE_DELETION_NOT_ALLOWED` |
| Oferta incompleta | 422 | `OFFER_NOT_READY` |
| Autor tentando validar a própria revisão | 422 | `SELF_VALIDATION_NOT_ALLOWED` |
| Envio sem validação vigente | 422 | `VALIDATION_REQUIRED` |
| Oferta publicada | 422 | `PUBLISHED_OFFER_CHANGE_REQUIRES_F04` |
| Validação sintática do request | 400 | `BAD_REQUEST` |
| Falha inesperada | 500 | `INTERNAL_ERROR` |

`NotFoundException`, `ConflictException`, `BusinessRuleViolationException` e `FluentValidation.ValidationException` existentes serão reutilizadas com os códigos estáveis do contrato.

---

## Inventário de Artefatos

### Arquivos a Criar

| Caminho | Tipo | Skills Aplicáveis | Descrição |
|---|---|---|---|
| `../localizestay-backend/.tasks/prd-estruturar-acomodacoes-tarifas-e-politicas/api-contract.yaml` | Contrato | `restful-api` | Cópia versionada usada pelos testes do backend |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Domain/IncorporatedProperties/IncorporatedProperty.cs` | Entidade | `dotnet-architecture`, `dotnet-code-quality` | Propriedade canônica derivada da F01 |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Domain/CommercialOffers/CommercialOffer.cs` | Aggregate Root | `dotnet-architecture`, `dotnet-code-quality` | Revisão, estado, autoria e invariantes da oferta |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Domain/CommercialOffers/CommercialPolicy.cs` | Entidade | `dotnet-architecture` | Política jurídica versionada |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Domain/CommercialOffers/Accommodation.cs` | Entidade | `dotnet-architecture` | Acomodação, ocupação, camas e heranças |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Domain/CommercialOffers/CommercialRate.cs` | Entidade | `dotnet-architecture` | Condição tarifária e período inclusivo |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Domain/CommercialOffers/OfferValidation.cs` | Entidade | `dotnet-architecture` | Evidência imutável da segunda validação |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Domain/CommercialOffers/OfferSubmission.cs` | Entidade | `dotnet-architecture` | Envio e snapshot imutável |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Domain/CommercialOffers/OfferReturn.cs` | Entidade | `dotnet-architecture` | Devolução downstream e motivo |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Domain/CommercialOffers/CommercialOfferValues.cs` | Value Objects/Enums | `dotnet-code-quality` | Estados, tipos, atores, faixa infantil, camas e alimentação |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Domain/CommercialOffers/CommercialOfferCompleteness.cs` | Serviço de domínio | `dotnet-architecture` | Pendências e campos-resumo derivados |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Domain/CommercialOffers/CommercialOfferIdempotencyKey.cs` | Entidade | `dotnet-architecture` | Idempotência e fingerprint da submissão |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/LegalPolicies/ILegalPolicyCatalog.cs` | Porta | `dotnet-architecture` | Contrato interno do catálogo jurídico |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/CommercialOffers/CommercialOfferCommands.cs` | CQRS | `dotnet-architecture` | Criação idempotente e comandos gerais |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/CommercialOffers/CommercialPolicyCommands.cs` | CQRS | `dotnet-architecture` | Comandos de política |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/CommercialOffers/AccommodationCommands.cs` | CQRS | `dotnet-architecture` | Comandos de acomodação |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/CommercialOffers/CommercialRateCommands.cs` | CQRS | `dotnet-architecture` | Comandos de tarifa |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/CommercialOffers/CommercialOfferWorkflowCommands.cs` | CQRS | `dotnet-architecture`, `dotnet-dependency-config` | Validação, envio, snapshot e outbox |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/CommercialOffers/CommercialOfferQueries.cs` | CQRS | `dotnet-performance` | Fila, detalhe, recursos, histórico e métricas |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/CommercialOffers/CommercialOfferDtos.cs` | DTO | `dotnet-code-quality` | Respostas internas correspondentes ao contrato |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/CommercialOffers/CommercialOfferMapper.cs` | Mapper | `dotnet-code-quality` | Mapeamento manual domínio/DTO |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/CommercialOffers/CommercialOfferValidators.cs` | Validador | `dotnet-dependency-config` | FluentValidation dos Commands e Queries |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/CommercialOffers/CurationOfferReturnedHandler.cs` | Consumer | `dotnet-architecture`, `dotnet-observability` | Consumo idempotente de devolução |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Infrastructure/LegalPolicies/ConfiguredLegalPolicyCatalog.cs` | Adaptador | `dotnet-dependency-config` | Catálogo configurável e validado no startup |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Infrastructure/Configurations/IncorporatedPropertyConfiguration.cs` | EF Mapping | `dotnet-dependency-config` | Tabela da propriedade canônica |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Infrastructure/Configurations/CommercialOfferConfiguration.cs` | EF Mapping | `dotnet-dependency-config`, `dotnet-performance` | Root, revisão, resumos e índices |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Infrastructure/Configurations/CommercialPolicyConfiguration.cs` | EF Mapping | `dotnet-dependency-config` | Políticas e unicidade ativa |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Infrastructure/Configurations/AccommodationConfiguration.cs` | EF Mapping | `dotnet-dependency-config` | Ocupação, camas e características |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Infrastructure/Configurations/CommercialRateConfiguration.cs` | EF Mapping | `dotnet-dependency-config`, `dotnet-performance` | Tarifas e índices de sobreposição |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Infrastructure/Configurations/OfferValidationConfiguration.cs` | EF Mapping | `dotnet-dependency-config` | Evidências de validação |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Infrastructure/Configurations/OfferSubmissionConfiguration.cs` | EF Mapping | `dotnet-dependency-config` | Snapshot JSONB e envios |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Infrastructure/Configurations/OfferReturnConfiguration.cs` | EF Mapping | `dotnet-dependency-config` | Devoluções downstream |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Infrastructure/Configurations/CommercialOfferIdempotencyKeyConfiguration.cs` | EF Mapping | `dotnet-dependency-config` | Unicidade e fingerprints |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Infrastructure/Migrations/[timestamp]_AddCommercialOffers.cs` | Migration | `dotnet-dependency-config` | Schema, backfill e índices da F02 |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Infrastructure/Migrations/[timestamp]_AddCommercialOffers.Designer.cs` | Migration Metadata | `dotnet-dependency-config` | Modelo EF gerado |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Endpoints/CommercialOfferEndpoints.cs` | Minimal API | `dotnet-architecture`, `restful-api` | Fila e detalhe |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Endpoints/CommercialPolicyEndpoints.cs` | Minimal API | `dotnet-architecture`, `restful-api` | Operações de política |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Endpoints/AccommodationEndpoints.cs` | Minimal API | `dotnet-architecture`, `restful-api` | Operações de acomodação |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Endpoints/CommercialRateEndpoints.cs` | Minimal API | `dotnet-architecture`, `restful-api` | Operações tarifárias |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Endpoints/CommercialOfferWorkflowEndpoints.cs` | Minimal API | `dotnet-architecture`, `restful-api` | Validação, envio e histórico |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Endpoints/CommercialOfferMetricsEndpoints.cs` | Minimal API | `dotnet-architecture`, `restful-api` | Métricas gerenciais |
| `../localizestay-backend/src/Modules/Curation/LocalizeStay.Modules.Curation.Contracts/CurationIntegrationEvents.cs` | Contrato de evento | `dotnet-architecture` | Evento versionado de devolução |
| `../localizestay-backend/tests/LocalizeStay.IntegrationTests/Infrastructure/OpenApiContractDocument.cs` | Test Fixture | `dotnet-testing` | Parser reutilizável para GET/POST/PUT/PATCH/DELETE |
| `../localizestay-backend/tests/LocalizeStay.UnitTests/Inventory/IncorporatedPropertyTests.cs` | Teste | `dotnet-testing` | Identidade estável e sincronização com F01 |
| `../localizestay-backend/tests/LocalizeStay.UnitTests/Inventory/CommercialOfferTests.cs` | Teste | `dotnet-testing` | Revisão, estado, completude e invalidação |
| `../localizestay-backend/tests/LocalizeStay.UnitTests/Inventory/CommercialPolicyTests.cs` | Teste | `dotnet-testing` | RN-11, RN-12 e RN-13 |
| `../localizestay-backend/tests/LocalizeStay.UnitTests/Inventory/AccommodationTests.cs` | Teste | `dotnet-testing` | RN-01/RN-07 e ocupação |
| `../localizestay-backend/tests/LocalizeStay.UnitTests/Inventory/CommercialRateTests.cs` | Teste | `dotnet-testing` | RN-07/RN-10, períodos e valores |
| `../localizestay-backend/tests/LocalizeStay.UnitTests/Inventory/CommercialOfferWorkflowTests.cs` | Teste | `dotnet-testing` | Dupla validação, envio e correção |
| `../localizestay-backend/tests/LocalizeStay.UnitTests/Inventory/CommercialOfferCommandHandlerTests.cs` | Teste | `dotnet-testing` | Transações, concorrência e idempotência |
| `../localizestay-backend/tests/LocalizeStay.UnitTests/Inventory/CommercialOfferMetricsQueryHandlerTests.cs` | Teste | `dotnet-testing` | Indicadores e calendários |
| `../localizestay-backend/tests/LocalizeStay.UnitTests/Inventory/CurationOfferReturnedHandlerTests.cs` | Teste | `dotnet-testing` | Deduplicação e reordenação da devolução |
| `../localizestay-backend/tests/LocalizeStay.IntegrationTests/Inventory/CommercialOfferApiContractTests.cs` | Teste | `dotnet-testing`, `restful-api` | Conformidade das 20 operações |
| `../localizestay-backend/tests/LocalizeStay.IntegrationTests/Inventory/CommercialOfferPersistenceTests.cs` | Teste | `dotnet-testing` | Migration, JSONB, FKs e índices |
| `../localizestay-backend/tests/LocalizeStay.IntegrationTests/Inventory/CommercialOfferEndpointsTests.cs` | Teste | `dotnet-testing` | Requests, responses e erros reais |
| `../localizestay-backend/tests/LocalizeStay.IntegrationTests/Inventory/CommercialOfferWorkflowTests.cs` | Teste | `dotnet-testing` | Validação, invalidação, envio e devolução |
| `../localizestay-backend/tests/LocalizeStay.IntegrationTests/Inventory/CommercialOfferOutboxAndAuditTests.cs` | Teste | `dotnet-testing` | Atomicidade de estado, snapshot, auditoria e outbox |
| `../localizestay-backend/tests/LocalizeStay.IntegrationTests/Inventory/CommercialOfferMetricsTests.cs` | Teste | `dotnet-testing` | Métricas reprocessáveis |
| `../localizestay-backend/tests/LocalizeStay.IntegrationTests/Inventory/CommercialOfferSecurityTests.cs` | Teste | `dotnet-testing` | Escopo, permissões e segregação de função |
| `../localizestay-backend/tests/LocalizeStay.IntegrationTests/Inventory/CommercialOfferEndToEndTests.cs` | Teste | `dotnet-testing` | Fluxo completo da incorporação à correção |
| `../localizestay-backend/docs/runbooks/commercial-offers.md` | Runbook | `dotnet-observability` | Operação, telemetria, replay e diagnóstico |

### Arquivos a Modificar

| Caminho | Skills Aplicáveis | Alteração |
|---|---|---|
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/InventoryModule.cs` | `dotnet-architecture`, `dotnet-dependency-config` | Registrar catálogo jurídico e endpoints F02 |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Infrastructure/InventoryDbContext.cs` | `dotnet-dependency-config` | Adicionar DbSets comerciais |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Infrastructure/Migrations/InventoryDbContextModelSnapshot.cs` | `dotnet-dependency-config` | Atualizar snapshot EF |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/PropertyOnboardings/PropertyOnboardingCommands.cs` | `dotnet-architecture` | Materializar/sincronizar `IncorporatedProperty` no envio F01 |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/Timing/IBusinessCalendar.cs` | `dotnet-code-quality` | Expor cálculo do prazo comercial |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Infrastructure/Timing/ConfiguredBusinessCalendar.cs` | `dotnet-code-quality` | Suportar prazo de dois dias úteis |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/Observability/InventoryTelemetry.cs` | `dotnet-observability` | Métricas e spans da oferta |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory.Contracts/InventoryIntegrationEvents.cs` | `dotnet-architecture` | Adicionar `InventoryCommercialOfferStructuredV1` |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/LocalizeStay.Modules.Inventory.csproj` | `dotnet-architecture` | Referenciar somente `Curation.Contracts` |
| `../localizestay-backend/src/BuildingBlocks/LocalizeStay.SharedKernel/Security/PermissionRequirement.cs` | `restful-api` | Adicionar catálogo `CommercialOfferPermissions` |
| `../localizestay-backend/src/BuildingBlocks/LocalizeStay.SharedKernel/Security/SecurityServiceCollectionExtensions.cs` | `restful-api` | Registrar quatro policies do contrato |
| `../localizestay-backend/src/LocalizeStay.Api/appsettings.json` | `dotnet-dependency-config` | Catálogo jurídico e calendário |
| `../localizestay-backend/tests/LocalizeStay.IntegrationTests/Inventory/ApiContractTests.cs` | `dotnet-testing` | Extrair parser compartilhado sem alterar cobertura F01 |
| `../localizestay-backend/README.md` | `dotnet-code-quality` | Documentar contrato e certificação F02 |

### Arquivos de Referência

| Caminho | Motivo da Consulta |
|---|---|
| `tasks/prd-estruturar-acomodacoes-tarifas-e-politicas/api-contract.yaml` | Fonte soberana dos endpoints |
| `domains/oferta-inventario/domain.md` | Entidades, regras e eventos |
| `context/architecture-baseline.md` | Ownership, consistência, eventos e guardrails |
| `../localizestay-backend/src/BuildingBlocks/LocalizeStay.SharedKernel/Outbox/OutboxMessageFactory.cs` | Outbox transacional existente |
| `../localizestay-backend/src/BuildingBlocks/LocalizeStay.SharedKernel/Auditing/BusinessAuditWriter.cs` | Auditoria por módulo |
| `../localizestay-backend/src/BuildingBlocks/LocalizeStay.SharedKernel/ErrorHandling/GlobalExceptionHandler.cs` | Tradução RFC 9457 existente |
| `../localizestay-backend/src/BuildingBlocks/LocalizeStay.SharedKernel/Events/InProcessEventBus.cs` | Entrega at-least-once |
| `../localizestay-backend/tests/LocalizeStay.IntegrationTests/Infrastructure/LocalizeStayWebApplicationFactory.cs` | JWT de teste e PostgreSQL Testcontainers |

---

## Pontos de Integração

- **F01 / propriedade incorporada:** o envio de `PropertyOnboarding` cria ou sincroniza `IncorporatedProperty` na mesma transação.
- **LogTo:** JWT fornece `sub`, nome, escopo `staff` e permissões declarativas. Atores nunca são recebidos no body.
- **Curadoria:** `InventoryCommercialOfferStructuredV1` é publicado pela outbox. Devoluções chegam por `CurationOfferReturnedV1`, com entrega at-least-once e consumo idempotente.
- **Catálogo jurídico:** configuração versionada resolve título, resumo e `ruleSetVersion`; texto livre do cliente é proibido.
- **WhatsApp/e-mail:** a F02 reutiliza os registros humanos da F01 exclusivamente para a métrica de quatro horas úteis.
- **F04:** ofertas publicadas permanecem somente leitura na F02.

---

## Análise de Impacto

| Componente Afetado | Impacto | Descrição e Risco | Ação |
|---|---|---|---|
| `Inventory` | Alto | Novo agregado, migration e 20 operações | Implementar por incrementos verticais |
| F01 | Médio | Passa a materializar propriedade canônica | Cobrir compatibilidade e backfill |
| Schema `inventory` | Alto | Novas tabelas, índices e JSONB | Migration e testes PostgreSQL reais |
| Curation Contracts | Médio | Novo evento de devolução | Aprovar payload e versionamento |
| Segurança | Médio | Quatro novas permissões | Testar 401/403 e menor privilégio |
| Outbox | Médio | Novo evento e snapshots maiores | Medir tamanho, retry e atraso |
| Auditoria | Médio | Novo volume de histórico | Metadados seguros e paginação |
| Consultas da fila | Médio | Filtros e ordenação operacionais | Campos-resumo e índices |
| Métricas | Médio | Agregações comerciais e reutilização da F01 | Testar denominadores e calendário |
| Frontend | Referência | Consumirá o contrato existente | TechSpec própria, fora deste documento |
| Deploy | Baixo | Sem nova infraestrutura | Aplicar migration antes da ativação |

O contrato HTTP não será modificado por esta TechSpec.

---

## Abordagem de Testes

### Testes Unitários

Usar xUnit, AwesomeAssertions e AAA. Mockar somente relógio, catálogo jurídico, auditoria e outras portas externas.

Cobertura mínima:

- `RN-01`: somente ator interno autenticado chega aos Commands.
- `RN-07`: prontidão exige acomodação e tarifa comercial válidas.
- `RN-10`: snapshots submetidos são imutáveis.
- `RN-11`: somente políticas Flexível e Não-Reembolsável.
- `RN-12` e `RN-13`: catálogo resolve regras e versões aprovadas.
- `RN-14`: cálculo do SLA usa o calendário oficial.
- revisão otimista e concorrência;
- ocupação, camas e faixa infantil;
- sobreposição tarifária inclusiva;
- troca/desativação de política;
- hard delete versus desativação;
- invalidação da validação;
- proibição de autovalidação;
- idempotência de envio e devolução;
- bloqueio de oferta publicada;
- completude e pendências.

### Testes de Integração

Usar `WebApplicationFactory` com PostgreSQL Testcontainers:

- aplicar migration e backfill;
- validar constraints e índices;
- testar criação concorrente do primeiro rascunho;
- verificar transação única para estado, auditoria, snapshot e outbox;
- validar rollback em conflito de revisão;
- certificar JSONB e materialização;
- testar permissões `read`, `write`, `review` e `metrics`;
- testar 400, 401, 403, 404, 409, 422, 429 e 500;
- testar paginação, filtros, ordenação e listas vazias;
- executar o fluxo F01 → F02 → validação → envio → devolução → correção → reenvio.

### Testes de Contrato

O parser compartilhado carregará `api-contract.yaml` e validará:

- exatamente 20 `operationId`;
- métodos GET, POST, PUT, PATCH e DELETE;
- paths e nomes de endpoints;
- request types e response metadata;
- todos os status declarados;
- `Location` em respostas 201;
- ausência de body nos 204;
- `application/problem+json`;
- campos obrigatórios dos schemas;
- proteção anônima e permissões;
- exemplos críticos de erros reais.

---

## Sequenciamento de Desenvolvimento

### Build Order

1. Criar `IncorporatedProperty` e sincronização com F01 — sem novas dependências.
2. Criar agregado `CommercialOffer` e entidades filhas — depende de 1.
3. Criar mappings, DbSets, migration e backfill — depende de 1 e 2.
4. Configurar catálogo jurídico, calendário e permissões — depende de 1.
5. Implementar Commands, Queries, validators e mappers — depende de 2, 3 e 4.
6. Implementar snapshot, idempotência, evento estruturado e consumidor de devolução — depende de 3 e 5.
7. Mapear as 20 operações Minimal API — depende de 5 e 6.
8. Refatorar infraestrutura de testes OpenAPI e criar testes unitários — depende de 2 a 7.
9. Criar testes PostgreSQL, contrato e fluxo ponta a ponta — depende de 3 a 8.
10. Atualizar README e runbook — depende de 6 a 9.

### Dependências Técnicas Bloqueantes

- Aprovação do identificador e conteúdo inicial de `ruleSetVersion` antes de operar dinheiro real.
- Ratificação dos nomes das permissões no catálogo de Identidade e Acesso.
- Aprovação do payload `CurationOfferReturnedV1` antes de certificar RF-06 ponta a ponta.
- Nenhuma nova infraestrutura é necessária.

---

## Monitoramento e Observabilidade

Métricas OpenTelemetry:

- `inventory.commercial_offer.created`;
- `inventory.commercial_offer.mutation` por tipo e resultado;
- `inventory.commercial_offer.validation` por resultado;
- `inventory.commercial_offer.validation_invalidated`;
- `inventory.commercial_offer.submission` por resultado;
- `inventory.commercial_offer.returned`;
- `inventory.commercial_offer.rate_overlap`;
- `inventory.commercial_offer.submission_duration`;
- `inventory.commercial_offer.outbox_failure`;
- completude, dupla validação, primeira aceitação e retrabalho como métricas de negócio consultáveis.

Logs estruturados usarão `propertyId`, `offerRevision`, `operation`, `result`, `validationId`, `submissionId`, `eventId` e `correlationId`. Não registrarão preços completos em mensagens, textos jurídicos, comentários, PII ou snapshots.

Spans customizados:

- `inventory.commercial_offer.load`;
- `inventory.commercial_offer.validate`;
- `inventory.commercial_offer.submit`;
- `inventory.commercial_offer.return`;
- `inventory.commercial_offer.metrics`.

Alertas:

- outbox sem processamento após o limite de retries;
- falhas recorrentes de persistência ou concorrência;
- submissão sem validação válida, tratada como violação crítica de invariável;
- limiares de SLA e retrabalho serão calibrados no piloto.

---

## Considerações Técnicas

### Decisões Principais

- **Propriedade canônica:** `IncorporatedProperty.Id == PropertyOnboarding.Id`.
  - Racional: introduz identidade estável sem UUID ou mapeamento adicional.
  - Trade-off: a origem do identificador permanece historicamente ligada à primeira incorporação.
- **Aggregate root:** um `CommercialOffer` por propriedade.
  - Racional: revisão, validação e invalidação exigem consistência conjunta.
  - Trade-off: mutações concorrentes da mesma oferta são serializadas.
- **Leitura:** tabelas normalizadas com campos-resumo transacionais.
  - Racional: atende a escala do piloto sem consistência eventual.
  - Trade-off: o write model mantém alguns valores derivados.
- **Persistência:** EF Core direto nos handlers.
  - Racional: é o padrão atual e evita repositórios sem comportamento adicional.
- **Mapeamento:** manual.
  - Racional: mantém DTOs HTTP separados do domínio com baixa quantidade de conversões.
- **Eventos:** outbox transacional e entrega at-least-once.
  - Racional: preserva publicação confiável e fronteira de extração.
- **Testes:** pirâmide completa de backend.
  - Racional: regras, PostgreSQL, contrato e workflow possuem riscos distintos.

### Riscos Conhecidos

- Crescimento excessivo do agregado: mitigar mantendo consultas projetadas e monitorando volume por propriedade.
- Concorrência entre operadores: mitigar com `revision`, concurrency token e mensagens acionáveis.
- Campos-resumo divergentes: atualizar somente pelo agregado e criar testes de reconstrução.
- Snapshot incompatível com evolução: incluir `snapshotVersion` e nunca desserializar versões antigas como entidades atuais.
- Devolução duplicada ou fora de ordem: deduplicar por `eventId` e validar submissão/revisão.
- Catálogo jurídico desatualizado: validar configuração no startup e auditar a versão usada.
- Migração de dados F01 existentes: backfill determinístico e teste com estado anterior real.
- GET com criação de rascunho: comportamento exigido pelo contrato; manter idempotência e não produzir evento externo.

### Requisitos Especiais

- Permissões específicas e negação por padrão.
- Revisor diferente do autor, independentemente da permissão recebida.
- Dados de ator derivados do JWT.
- Auditoria funcional distinta de logs diagnósticos.
- Sem cache, broker ou banco adicional no MVP.
- Valores exclusivamente em BRL e centavos.
- Código, classes e membros em inglês; termos de negócio preservados no glossário e documentação.

### Conformidade com Skills

- CQRS, domínio e exceções seguem `dotnet-architecture`.
- Naming, DI e `CancellationToken` seguem `dotnet-code-quality`.
- PostgreSQL, EF Core, FluentValidation e outbox seguem `dotnet-dependency-config`.
- Testes seguem `dotnet-testing`.
- Telemetria segue `dotnet-observability`.
- Projeções, paginação e índices seguem `dotnet-performance`.
- OpenAPI e Problem Details seguem `restful-api`.

Desvios deliberados:

| Desvio | Skill | Justificativa |
|---|---|---|
| Um projeto por módulo, em vez de projetos separados por camada | `dotnet-architecture` | Baseline e código existente usam encapsulamento modular por assembly e tipos `internal` |
| Handlers usam `InventoryDbContext` diretamente | `dotnet-architecture` | O contexto atual já é Unit of Work; repositório adicional não agregaria comportamento |
| Mapeamento manual em vez de Mapster | `dotnet-dependency-config` | Alternativa permitida e já adotada no módulo |
| Sem cache | `dotnet-performance` | Escala inicial e ADR-0002 não justificam Redis ou cache local |
| Sem projeção assíncrona | `dotnet-performance` | Não há necessidade medida que compense consistência eventual |

---

## Questões em Aberto

- [ ] Aprovar `ruleSetVersion`, títulos e resumos jurídicos iniciais.
- [ ] Ratificar `commercial-offers:read`, `write`, `review` e `metrics`.
- [ ] Aprovar o contrato de `CurationOfferReturnedV1`.
- [ ] Confirmar se `completeInformationReceivedAt` deve representar o primeiro instante em que a oferta fica completa no sistema ou uma evidência explícita de recebimento externo.
- [ ] Definir a origem futura da faixa etária infantil padrão da propriedade; até lá, `childAgeRangeSource` poderá ser `none`.
- [ ] Não foi identificado conflito com o contrato HTTP. As duas últimas questões representam semânticas internas não expressas por operações de escrita no contrato.

---

## Architecture Decision Records

- [ADR-001: Estabelecer propriedade incorporada canônica a partir da F01](adrs/adr-001.md) — usa o identificador do onboarding como identidade estável.
- [ADR-002: Modelar oferta comercial como agregado com resumos transacionais](adrs/adr-002.md) — garante consistência e adia projeções assíncronas.
- [ADR-0001 global: Backend .NET modular](../../docs/adr/ADR-0001-backend-dotnet-monolito-modular.md) — plataforma e fronteiras.
- [ADR-0002 global: PostgreSQL único e infraestrutura distribuída adiada](../../docs/adr/ADR-0002-postgresql-unico-adiamento-mongo-redis-broker.md) — persistência e outbox.
- [ADR-0006 global: LogTo](../../docs/adr/ADR-0006-logto-provedor-identidade.md) — autenticação.
- [ADR-0007 global: OpenTelemetry e Grafana](../../docs/adr/ADR-0007-observabilidade-otel-grafanacloud.md) — observabilidade.
- [ADR-0010 global: Autorização local](../../docs/adr/ADR-0010-autorizacao-local-ecad-authz-como-referencia.md) — enforcement no módulo.
- [ADR F01-001: Implementar F01 no Inventory](../prd-incorporar-parceiros-e-propriedades/adrs/adr-001.md) — CQRS, schema e outbox.
- [ADR F01-003: Auditoria por módulo](../prd-incorporar-parceiros-e-propriedades/adrs/adr-003.md) — histórico funcional.

---

## Próximos Passos

1. Usar `flow-task-creator` para gerar as tarefas de implementação.
2. Usar `flow-frontend-techspec-creator` para a TechSpec do backoffice.
3. Resolver as questões abertas antes ou durante os incrementos correspondentes.
