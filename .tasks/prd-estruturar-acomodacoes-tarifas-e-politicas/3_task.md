---
status: pending
parallelizable: false
blocked_by: ["2.0"]
---

<task_context>
<domain>inventory/domain/commercial-offers</domain>
<type>implementation</type>
<scope>core_feature</scope>
<complexity>high</complexity>
<dependencies>database</dependencies>
<unblocks>"4.0, 5.0, 6.0, 7.0, 8.0, 9.0"</unblocks>
</task_context>

# Tarefa 3.0: Implementar o agregado CommercialOffer, revisão e completude

## Relacionada às User Stories

- [US-01] Salvar progressivamente e preservar trabalho incompleto (direta)
- [US-03] Conferir dados em uma revisão estável (suporte)
- [US-05] Medir completude e prazo (suporte)

## Visão Geral

Criar o aggregate root `CommercialOffer` e seus tipos centrais. O agregado controla rascunho, revisão otimista, autoria, completude, pendências, invalidação de validação, hard delete versus desativação, bloqueio de oferta publicada e campos-resumo transacionais.

## Requisitos

- Uma oferta por propriedade; nasce em `draft` com revisão inicial.
- Toda mutação comercial incrementa a revisão uma única vez, troca o autor e invalida validação vigente.
- A prontidão exige ao menos uma acomodação completa com tarifa ativa atual ou futura.
- Pendências são recalculadas no domínio; apenas resumos necessários à fila são persistidos.
- Preencher `completeInformationReceivedAt` apenas na primeira completude e calcular o alvo de dois dias úteis.
- Ofertas `published` rejeitam mutações F02 com `PUBLISHED_OFFER_CHANGE_REQUIRES_F04`.
- Evidências de validação, envio e devolução são imutáveis.

## Arquivos Envolvidos

- **Criar:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Domain/CommercialOffers/CommercialOffer.cs`
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Domain/CommercialOffers/CommercialOfferValues.cs`
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Domain/CommercialOffers/CommercialOfferCompleteness.cs`
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Domain/CommercialOffers/OfferValidation.cs`
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Domain/CommercialOffers/OfferSubmission.cs`
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Domain/CommercialOffers/OfferReturn.cs`
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Domain/CommercialOffers/CommercialOfferIdempotencyKey.cs`
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/CommercialOffers/CommercialOfferCommands.cs`
  - `../localizestay-backend/tests/LocalizeStay.UnitTests/Inventory/CommercialOfferTests.cs`
- **Modificar:**
  - Nenhum nesta tarefa.
- **Referência:**
  - `tasks/prd-estruturar-acomodacoes-tarifas-e-politicas/techspec.md`
  - `domains/oferta-inventario/domain.md`
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Domain/PropertyOnboardings/PropertyOnboarding.cs`
  - `../localizestay-backend/src/BuildingBlocks/LocalizeStay.SharedKernel/ErrorHandling/BusinessRuleViolationException.cs`
- **Skills para consultar durante implementação:**
  - `dotnet-architecture` — aggregate root, entidades filhas e exceções
  - `dotnet-code-quality` — encapsulamento, métodos pequenos e tipos imutáveis
  - `dotnet-testing` — cobertura >80% da lógica e testes parametrizados
  - `dotnet-observability` — pontos de instrumentação sem acoplar domínio a telemetria

## Subtarefas

- [ ] 3.1 Criar enums/value objects de estado, atores, pendências, faixa infantil, camas, alimentação e dinheiro em centavos.
- [ ] 3.2 Implementar criação idempotente do rascunho para propriedade incorporada.
- [ ] 3.3 Implementar `revision`, autoria, transição de estado e invalidação de validação em uma única mutação.
- [ ] 3.4 Implementar `CommercialOfferCompleteness` e campos-resumo reconstruíveis.
- [ ] 3.5 Modelar evidências imutáveis de validação, submissão, devolução e idempotência.
- [ ] 3.6 Implementar guard de oferta publicada e regras genéricas de delete/desativação.
- [ ] 3.7 Criar testes de revisão, completude, pendências, prontidão, prazo, invalidação e bloqueio publicado.

## Sequenciamento

- Bloqueado por: 2.0
- Desbloqueia: 4.0, 5.0, 6.0, 7.0, 8.0 e 9.0
- Paralelizável: Não; define as invariantes compartilhadas pelos incrementos comerciais.

## Rastreabilidade

- Esta tarefa cobre: US-01 diretamente; US-03 e US-05 como suporte; RF-02 a RF-06 parcialmente.
- Evidência esperada: testes do agregado provam revisão monotônica, pendências determinísticas, prontidão mínima e invalidação.

## Detalhes de Implementação

O primeiro `GET` criará a oferta `draft` idempotentemente na camada de aplicação. O domínio expõe operações explícitas e nunca setters públicos. Cada operação comercial deve chamar um único mecanismo interno de mutação que:

1. verifica `published` e `expectedRevision` quando aplicável;
2. aplica a regra;
3. incrementa `Revision` exatamente uma vez;
4. registra o autor da revisão;
5. invalida `CurrentValidation`;
6. recalcula resumos e o primeiro instante de completude.

Valores monetários usam `long` em centavos; datas tarifárias serão `DateOnly` e instantes `DateTimeOffset` UTC. `OfferSubmission` armazena snapshot versionado, não DTO HTTP. `CommercialOfferIdempotencyKey` armazena chave, escopo, fingerprint e referência ao resultado.

**Convenções da stack (das skills consultadas):**

- Domínio não referencia EF, HTTP, logging ou OpenTelemetry.
- Código e membros em inglês; enums e records imutáveis; coleções expostas como somente leitura.
- Métodos executam uma ação, evitam flags e mantêm no máximo dois níveis de aninhamento.
- Violações usam exceções de domínio com códigos estáveis; não lançar `Exception` genérica.
- Testes xUnit + AwesomeAssertions seguem AAA, teorias para matrizes e naming `Method_Condition_ExpectedBehavior`.

## Critérios de Sucesso (Verificáveis)

- [ ] Testes passam: `dotnet test ../localizestay-backend/LocalizeStay.sln --filter "FullyQualifiedName~CommercialOfferTests"`
- [ ] Cobertura de lógica de domínio é pelo menos 80% no relatório do projeto de unit tests.
- [ ] Build compila sem erros: `dotnet build ../localizestay-backend/LocalizeStay.sln --no-restore`
- [ ] Formatação válida: `dotnet format ../localizestay-backend/LocalizeStay.sln --verify-no-changes --no-restore`
- [ ] Duas mutações com a mesma revisão concorrente resultam em uma confirmação e um `REVISION_MISMATCH`.
- [ ] Alterar preço, ocupação, política ou período invalida a validação vigente e incrementa a revisão uma vez.
- [ ] Uma oferta publicada rejeita toda mutação F02 com `PUBLISHED_OFFER_CHANGE_REQUIRES_F04`.
