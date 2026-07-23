---
status: pending
parallelizable: true
blocked_by: ["3.0"]
---

<task_context>
<domain>inventory/domain/accommodations</domain>
<type>implementation</type>
<scope>core_feature</scope>
<complexity>high</complexity>
<dependencies>database</dependencies>
<unblocks>"7.0, 8.0, 10.0"</unblocks>
</task_context>

# Tarefa 5.0: Implementar acomodações, ocupação e heranças

## Relacionada às User Stories

- [US-01] Cadastrar acomodações progressivamente (direta)
- [US-02] Herdar política padrão e reduzir inconsistências (suporte)

## Visão Geral

Implementar acomodações em rascunho com nome comercial mínimo, ocupação, camas, características estruturais, política associada e faixa etária infantil herdada ou sobrescrita. Conteúdo editorial de D06 não participa da completude F02.

## Requisitos

- Criação progressiva exige apenas `commercialName`.
- Herdar política padrão e faixa etária da propriedade como referências/origem explícitas.
- Validar `maxAdults + maxChildren <= totalCapacity` e coerência entre camas e capacidade.
- Omissão de faixa mantém herança; `null` remove override; objeto define override.
- Desativação exige motivo; hard delete somente antes de envio e remove tarifas ainda não enviadas.
- Fotos, descrição e comodidades editoriais não bloqueiam a acomodação.

## Arquivos Envolvidos

- **Criar:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Domain/CommercialOffers/Accommodation.cs`
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/CommercialOffers/AccommodationCommands.cs`
  - `../localizestay-backend/tests/LocalizeStay.UnitTests/Inventory/AccommodationTests.cs`
- **Modificar:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Domain/CommercialOffers/CommercialOffer.cs` (operações de acomodação)
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Domain/CommercialOffers/CommercialOfferCompleteness.cs` (pendências comerciais)
- **Referência:**
  - `tasks/prd-estruturar-acomodacoes-tarifas-e-politicas/api-contract.yaml`
  - `domains/oferta-inventario/domain.md`
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Domain/CommercialOffers/CommercialPolicy.cs`
- **Skills para consultar durante implementação:**
  - `dotnet-architecture` — entidade filha e Commands/Handlers
  - `dotnet-code-quality` — value objects, métodos explícitos e null handling
  - `dotnet-testing` — teorias de ocupação, camas e herança
  - `dotnet-observability` — logs sem dados editoriais ou pessoais

## Subtarefas

- [ ] 5.1 Modelar `Accommodation`, configuração de camas, características e origem da faixa infantil.
- [ ] 5.2 Implementar `CreateAccommodationCommandHandler` com heranças explícitas.
- [ ] 5.3 Implementar `UpdateAccommodationCommandHandler` com PATCH sem ambiguidade entre omitido e `null`.
- [ ] 5.4 Implementar desativação e `DeleteAccommodationCommandHandler` com proteção por envio.
- [ ] 5.5 Recalcular completude e pendências somente com os campos comerciais da F02.
- [ ] 5.6 Invalidar validação e incrementar uma revisão por mutação.
- [ ] 5.7 Testar matrizes de capacidade, camas, faixa infantil, política, rascunho, desativação e delete.

## Sequenciamento

- Bloqueado por: 3.0
- Desbloqueia: 7.0, 8.0 e 10.0
- Paralelizável: Sim; pode evoluir junto com 4.0 e 6.0.

## Rastreabilidade

- Esta tarefa cobre: US-01 diretamente, US-02 como suporte; RF-02 e parte do RF-04.
- Evidência esperada: uma acomodação incompleta é salva com pendências; uma acomodação comercial completa avança sem conteúdo editorial.

## Detalhes de Implementação

Commands previstos: `CreateAccommodationCommand`, `UpdateAccommodationCommand` e `DeleteAccommodationCommand`. Para PATCH, preservar presença de propriedades com `JsonElement` ou wrapper equivalente no endpoint e converter para intenção explícita no Command.

`ChildAgeRangeSource` deve ser `propertyDefault`, `accommodationOverride` ou `none`. A validação de ocupação deve produzir `INVALID_OCCUPANCY_CONFIGURATION`. `BedConfiguration` é value object/coleção semântica e será persistida em JSONB pela tarefa 7.0.

**Convenções da stack (das skills consultadas):**

- Entidade mantém setters privados e expõe coleções somente leitura.
- Não usar flag parameter para diferenciar herança/override; modelar intenção no Command.
- Validação sintática fica no FluentValidation; coerência cruzada fica no domínio.
- `CancellationToken` obrigatório em handlers e chamadas EF.
- Testes seguem AAA, AwesomeAssertions e teorias para limites 0/1/20/30.

## Critérios de Sucesso (Verificáveis)

- [ ] Testes passam: `dotnet test ../localizestay-backend/LocalizeStay.sln --filter "FullyQualifiedName~AccommodationTests"`
- [ ] Build compila sem erros: `dotnet build ../localizestay-backend/LocalizeStay.sln --no-restore`
- [ ] Formatação válida: `dotnet format ../localizestay-backend/LocalizeStay.sln --verify-no-changes --no-restore`
- [ ] `maxAdults + maxChildren > totalCapacity` retorna `INVALID_OCCUPANCY_CONFIGURATION`.
- [ ] Omissão, `null` e objeto de `childAgeRange` produzem, respectivamente, manter, remover e definir override.
- [ ] Acomodação sem fotos/descrição/comodidades, mas comercialmente completa, é marcada como completa.
- [ ] Item já enviado é desativado com histórico; item nunca enviado pode ser excluído.

