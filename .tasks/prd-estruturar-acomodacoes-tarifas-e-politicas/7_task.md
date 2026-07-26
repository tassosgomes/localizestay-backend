---
status: pending
parallelizable: false
blocked_by: ["1.0", "2.0", "3.0", "4.0", "5.0", "6.0"]
---

<task_context>
<domain>inventory/infrastructure/persistence</domain>
<type>implementation</type>
<scope>configuration</scope>
<complexity>high</complexity>
<dependencies>database</dependencies>
<unblocks>"8.0, 9.0, 10.0, 11.0, 12.0"</unblocks>
</task_context>

# Tarefa 7.0: Persistir a oferta comercial e executar migration/backfill

## Relacionada às User Stories

- [US-01] Preservar cadastros progressivos (suporte)
- [US-02] Reutilizar políticas com consistência (suporte)
- [US-03] Revisar uma versão estável (suporte)

## Visão Geral

Mapear o agregado em tabelas normalizadas do schema `inventory`, configurar revisão otimista, JSONB, FKs internas, índices, DbSets e uma migration com backfill determinístico das propriedades F01 existentes. Certificar o modelo contra PostgreSQL real.

## Requisitos

- `CommercialOffer.PropertyId` é PK/FK para `IncorporatedProperty`; uma oferta por propriedade.
- `Revision` é concurrency token e conflitos viram `REVISION_MISMATCH`.
- Camas e snapshot usam JSONB; datas/instantes e centavos seguem os tipos da TechSpec.
- Não criar FK ou join físico entre módulos.
- Índices devem cobrir fila, tipos ativos, recursos, overlap, submissões, idempotência e eventId.
- Backfill cria `IncorporatedProperty` para F01 já incorporadas sem duplicar registros.
- Estado, auditoria, idempotência e outbox continuam no mesmo `InventoryDbContext`/transação.

## Arquivos Envolvidos

- **Criar:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Infrastructure/Configurations/IncorporatedPropertyConfiguration.cs`
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Infrastructure/Configurations/CommercialOfferConfiguration.cs`
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Infrastructure/Configurations/CommercialPolicyConfiguration.cs`
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Infrastructure/Configurations/AccommodationConfiguration.cs`
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Infrastructure/Configurations/CommercialRateConfiguration.cs`
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Infrastructure/Configurations/OfferValidationConfiguration.cs`
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Infrastructure/Configurations/OfferSubmissionConfiguration.cs`
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Infrastructure/Configurations/OfferReturnConfiguration.cs`
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Infrastructure/Configurations/CommercialOfferIdempotencyKeyConfiguration.cs`
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Infrastructure/Migrations/[timestamp]_AddCommercialOffers.cs`
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Infrastructure/Migrations/[timestamp]_AddCommercialOffers.Designer.cs`
  - `../localizestay-backend/tests/LocalizeStay.IntegrationTests/Inventory/CommercialOfferPersistenceTests.cs`
- **Modificar:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Infrastructure/InventoryDbContext.cs`
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Infrastructure/Migrations/InventoryDbContextModelSnapshot.cs`
- **Referência:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Infrastructure/Configurations/PropertyOnboardingConfiguration.cs`
  - `../localizestay-backend/src/BuildingBlocks/LocalizeStay.SharedKernel/Outbox/OutboxMessage.cs`
  - `tasks/prd-estruturar-acomodacoes-tarifas-e-politicas/techspec.md`
- **Skills para consultar durante implementação:**
  - `dotnet-dependency-config` — EF Core, Fluent API, PostgreSQL e migrations
  - `dotnet-architecture` — DbContext como Unit of Work e limites do módulo
  - `dotnet-performance` — índices, projeções futuras e constraints
  - `dotnet-testing` — Testcontainers/PostgreSQL e isolamento
  - `dotnet-code-quality` — tipos, naming e tratamento de concorrência

## Subtarefas

- [ ] 7.1 Adicionar DbSets de todas as entidades comerciais ao `InventoryDbContext`.
- [ ] 7.2 Criar mappings com schema, nomes, precisão/tipos, JSONB, relacionamentos e delete behaviors.
- [ ] 7.3 Configurar `Revision` como concurrency token e traduzir `DbUpdateConcurrencyException`.
- [ ] 7.4 Criar constraints/índices de unicidade, fila, overlap, submissão, idempotência e eventos.
- [ ] 7.5 Gerar migration e revisar manualmente o SQL produzido.
- [ ] 7.6 Implementar backfill idempotente das propriedades incorporadas existentes.
- [ ] 7.7 Testar migration vazia e sobre estado F01 prévio, JSONB, FKs, constraints, índices e rollback.

## Sequenciamento

- Bloqueado por: 1.0 a 6.0
- Desbloqueia: 8.0 a 12.0
- Paralelizável: Não; consolida o modelo compartilhado e deve ser integrado após as três fatias de domínio.

## Rastreabilidade

- Esta tarefa cobre: suporte persistente a US-01, US-02 e US-03; RF-01 a RF-06.
- Evidência esperada: migration reprodutível e testes PostgreSQL provando modelo, backfill e concorrência.

## Detalhes de Implementação

Tabelas requeridas: `incorporated_properties`, `commercial_offers`, `commercial_policies`, `accommodations`, `commercial_rates`, `offer_validations`, `offer_submissions`, `offer_returns` e `commercial_offer_idempotency_keys`. Reutilizar `business_audit_entries` e `outbox_messages` existentes.

Índices mínimos:

- `commercial_offers(status, target_submission_at)` e `commercial_offers(property_id)`;
- `commercial_policies(property_id, type, status)`;
- `accommodations(property_id, status)`;
- `commercial_rates(accommodation_id, condition_code, policy_id, meal_plan, valid_from, valid_to)`;
- `offer_submissions(property_id, revision)`;
- unicidade da chave idempotente e do identificador de evento.

**Convenções da stack (das skills consultadas):**

- Usar `IEntityTypeConfiguration<T>` e nomes `snake_case` coerentes com o módulo.
- PostgreSQL é o banco real dos testes; não usar provider InMemory.
- Leitura futura usa `AsNoTracking`; escrita carrega o agregado rastreado.
- Migration é versionada; nunca editar migration aplicada em ambiente compartilhado.
- Testes usam WebApplicationFactory/Testcontainers, seed determinístico e limpeza isolada.

## Critérios de Sucesso (Verificáveis)

- [ ] Migration aparece no inventário EF: `dotnet ef migrations list --project ../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory --startup-project ../localizestay-backend/src/LocalizeStay.Api --context InventoryDbContext`
- [ ] Testes PostgreSQL passam: `dotnet test ../localizestay-backend/LocalizeStay.sln --filter "FullyQualifiedName~CommercialOfferPersistenceTests"`
- [ ] Build compila sem erros: `dotnet build ../localizestay-backend/LocalizeStay.sln --no-restore`
- [ ] Formatação válida: `dotnet format ../localizestay-backend/LocalizeStay.sln --verify-no-changes --no-restore`
- [ ] Aplicar migration duas vezes é seguro e o backfill não duplica propriedades.
- [ ] Constraints impedem segunda oferta por propriedade e chaves idempotentes duplicadas.
- [ ] JSONB round-trip preserva camas e snapshots; rollback não deixa auditoria/outbox parcial.
