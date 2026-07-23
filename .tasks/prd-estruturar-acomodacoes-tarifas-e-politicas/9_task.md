---
status: pending
parallelizable: true
blocked_by: ["7.0"]
---

<task_context>
<domain>inventory/application/commercial-offer-workflow</domain>
<type>integration</type>
<scope>core_feature</scope>
<complexity>high</complexity>
<dependencies>database,external_apis</dependencies>
<unblocks>"10.0, 11.0, 12.0"</unblocks>
</task_context>

# Tarefa 9.0: Implementar validação, submissão, outbox e devolução

## Relacionada às User Stories

- [US-03] Conferir preços, ocupação e políticas antes do envio (direta)
- [US-04] Manter continuidade com os canais atuais (suporte)
- [US-01] Corrigir e reenviar sem perder histórico (direta)

## Visão Geral

Implementar o workflow forte da oferta: segunda validação por operador diferente, submissão idempotente com snapshot/auditoria/outbox atômicos e consumo idempotente de devoluções da Curadoria. Alterações posteriores invalidam evidências e exigem nova validação.

## Requisitos

- Validação exige oferta pronta, `expectedRevision` atual e revisor diferente do autor da revisão.
- Submissão exige validação válida da mesma revisão.
- `Idempotency-Key` repetida com mesmo fingerprint faz replay; payload diferente retorna `IDEMPOTENCY_KEY_REUSED`.
- Submissão persiste oferta, snapshot imutável versionado, auditoria e outbox no mesmo commit.
- Evento produzido: `InventoryCommercialOfferStructuredV1` / `oferta-inventario.oferta-estruturada`.
- Devolução `CurationOfferReturnedV1` é at-least-once, deduplicada por `eventId` e validada contra submissão/propriedade/revisão.
- Correção de oferta devolvida preserva histórico, invalida validação e permite novo envio; publicada permanece bloqueada.

## Arquivos Envolvidos

- **Criar:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/CommercialOffers/CommercialOfferWorkflowCommands.cs`
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/CommercialOffers/CurationOfferReturnedHandler.cs`
  - `../localizestay-backend/src/Modules/Curation/LocalizeStay.Modules.Curation.Contracts/CurationIntegrationEvents.cs`
  - `../localizestay-backend/tests/LocalizeStay.UnitTests/Inventory/CommercialOfferWorkflowTests.cs`
  - `../localizestay-backend/tests/LocalizeStay.UnitTests/Inventory/CommercialOfferCommandHandlerTests.cs`
  - `../localizestay-backend/tests/LocalizeStay.UnitTests/Inventory/CurationOfferReturnedHandlerTests.cs`
- **Modificar:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory.Contracts/InventoryIntegrationEvents.cs`
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/LocalizeStay.Modules.Inventory.csproj`
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Domain/CommercialOffers/CommercialOffer.cs`
- **Referência:**
  - `../localizestay-backend/src/BuildingBlocks/LocalizeStay.SharedKernel/Outbox/OutboxMessageFactory.cs`
  - `../localizestay-backend/src/BuildingBlocks/LocalizeStay.SharedKernel/Auditing/BusinessAuditWriter.cs`
  - `../localizestay-backend/src/BuildingBlocks/LocalizeStay.SharedKernel/Events/IIntegrationEventHandler.cs`
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/PropertyOnboardings/PropertyOnboardingCommands.cs`
- **Skills para consultar durante implementação:**
  - `dotnet-architecture` — CQRS, contracts e handler de evento
  - `dotnet-dependency-config` — outbox, serialização e DI
  - `dotnet-code-quality` — fingerprint, cancellation e exceções específicas
  - `dotnet-testing` — testes unitários de idempotência/concorrência
  - `dotnet-observability` — spans, métricas e logging seguro
  - `dotnet-production-readiness` — sanitização e resiliência

## Subtarefas

- [ ] 9.1 Implementar `CreateOfferValidationCommandHandler` com segregação de função e revisão otimista.
- [ ] 9.2 Definir snapshot comercial versionado e serialização determinística.
- [ ] 9.3 Implementar `SubmitCommercialOfferCommandHandler` com fingerprint/replay.
- [ ] 9.4 Criar `InventoryCommercialOfferStructuredV1` e gravá-lo na outbox.
- [ ] 9.5 Definir/ratificar `CurationOfferReturnedV1` no assembly Contracts.
- [ ] 9.6 Implementar `CurationOfferReturnedHandler` com deduplicação e proteção contra eventos fora de ordem.
- [ ] 9.7 Registrar auditoria funcional e instrumentação de validar/enviar/devolver.
- [ ] 9.8 Testar autovalidação, revisão, idempotência, atomicidade lógica, duplicação, reordenação, correção e reenvio.

## Sequenciamento

- Bloqueado por: 7.0
- Desbloqueia: 10.0, 11.0 e 12.0
- Paralelizável: Sim; pode avançar junto com 8.0.

## Rastreabilidade

- Esta tarefa cobre: US-03 e US-01 diretamente, US-04 como suporte; RF-05 e RF-06.
- Evidência esperada: testes provam dupla validação, snapshot/outbox idempotente e devolução segura.

## Detalhes de Implementação

O consumer deve manter a assinatura:

~~~csharp
internal sealed class CurationOfferReturnedHandler
    : IIntegrationEventHandler<CurationOfferReturnedV1>
{
    public Task HandleAsync(
        CurationOfferReturnedV1 integrationEvent,
        CancellationToken cancellationToken);
}
~~~

O snapshot é independente de DTOs HTTP e inclui `snapshotVersion`. O fingerprint deve ser calculado sobre uma representação canônica do payload relevante. Em replay, retornar a submissão/evento original sem gerar nova auditoria ou outbox. O projeto Inventory referencia somente `Curation.Contracts`, nunca a implementação do módulo.

**Convenções da stack (das skills consultadas):**

- Um único `SaveChangesAsync` grava estado, validação/submissão, snapshot, idempotência, auditoria e outbox.
- Handlers propagam `CancellationToken` até o ponto anterior ao commit; não cancelar efeitos já confirmados.
- Logs usam templates e IDs opacos; não registram preços, comentários, textos jurídicos, PII ou snapshot.
- Spans registram status/exception e tags de baixa cardinalidade.
- Testes seguem AAA; portas externas são mockadas, mas EF/atomicidade final ficam para integração.

## Critérios de Sucesso (Verificáveis)

- [ ] Testes unitários passam: `dotnet test ../localizestay-backend/LocalizeStay.sln --filter "FullyQualifiedName~CommercialOfferWorkflowTests|FullyQualifiedName~CommercialOfferCommandHandlerTests|FullyQualifiedName~CurationOfferReturnedHandlerTests"`
- [ ] Build compila sem erros: `dotnet build ../localizestay-backend/LocalizeStay.sln --no-restore`
- [ ] Formatação válida: `dotnet format ../localizestay-backend/LocalizeStay.sln --verify-no-changes --no-restore`
- [ ] Autor da revisão recebe `SELF_VALIDATION_NOT_ALLOWED` ao validar a própria oferta.
- [ ] Submissão sem validação vigente retorna `VALIDATION_REQUIRED`.
- [ ] Retry idempotente retorna o mesmo `submissionId`; fingerprint diferente retorna `IDEMPOTENCY_KEY_REUSED`.
- [ ] Evento duplicado não cria segunda devolução; evento fora de ordem não regride submissão mais nova.
- [ ] Oferta publicada retorna `PUBLISHED_OFFER_CHANGE_REQUIRES_F04`.
