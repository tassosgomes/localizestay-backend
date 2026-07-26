---
status: pending
parallelizable: true
blocked_by: ["3.0"]
---

<task_context>
<domain>inventory/domain/commercial-rates</domain>
<type>implementation</type>
<scope>core_feature</scope>
<complexity>high</complexity>
<dependencies>database</dependencies>
<unblocks>"7.0, 8.0, 9.0, 10.0"</unblocks>
</task_context>

# Tarefa 6.0: Implementar tarifas comerciais e períodos

## Relacionada às User Stories

- [US-01] Cadastrar condições comerciais progressivamente (direta)
- [US-03] Conferir preços e condições antes do envio (suporte)

## Visão Geral

Implementar tarifas em rascunho e ativas, exclusivamente em BRL e centavos, com hóspedes incluídos, adicionais de adulto/criança, período inclusivo, mínimo de noites, política, regime alimentar e prevenção de sobreposição por condição comercial.

## Requisitos

- Permitir rascunho progressivo; completude final exige todos os campos comerciais.
- `currency` é sempre `BRL` e `mandatoryFeesIncluded` sempre `true` por invariantes do servidor.
- Regimes permitidos: `roomOnly`, `breakfast`, `halfBoard` e `fullBoard`.
- Detectar sobreposição inclusiva para mesma acomodação, `conditionCode`, política e alimentação.
- Validar período, capacidade de hóspedes e preços não negativos.
- Mínimo de noites de uma estadia é o da tarifa aplicável no check-in; cada diária usa a tarifa de sua data.
- Hard delete somente para tarifa nunca enviada; caso contrário desativar com motivo.

## Arquivos Envolvidos

- **Criar:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Domain/CommercialOffers/CommercialRate.cs`
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/CommercialOffers/CommercialRateCommands.cs`
  - `../localizestay-backend/tests/LocalizeStay.UnitTests/Inventory/CommercialRateTests.cs`
- **Modificar:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Domain/CommercialOffers/CommercialOffer.cs` (operações de tarifa)
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Domain/CommercialOffers/CommercialOfferCompleteness.cs` (pendências e prontidão)
- **Referência:**
  - `tasks/prd-estruturar-acomodacoes-tarifas-e-politicas/api-contract.yaml`
  - `domains/oferta-inventario/domain.md`
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Domain/CommercialOffers/Accommodation.cs`
- **Skills para consultar durante implementação:**
  - `dotnet-architecture` — regras no agregado e CQRS
  - `dotnet-code-quality` — tipos monetários, datas e exceções específicas
  - `dotnet-testing` — testes parametrizados de intervalos e limites
  - `dotnet-performance` — desenho compatível com índice de sobreposição
  - `dotnet-observability` — métrica de overlap e logs sem preços completos

## Subtarefas

- [ ] 6.1 Modelar `CommercialRate` com centavos, `DateOnly`, política, alimentação, status e histórico de envio.
- [ ] 6.2 Implementar `CreateCommercialRateCommandHandler` para rascunho e ativação válida.
- [ ] 6.3 Implementar `UpdateCommercialRateCommandHandler` e invalidação de validação.
- [ ] 6.4 Implementar `DeleteCommercialRateCommandHandler` e desativação histórica.
- [ ] 6.5 Implementar algoritmo de sobreposição inclusiva no agregado.
- [ ] 6.6 Atualizar prontidão/completude da acomodação e oferta.
- [ ] 6.7 Testar adjacência, interseção, contenção, datas iguais, BRL, taxas, hóspedes e delete.

## Sequenciamento

- Bloqueado por: 3.0
- Desbloqueia: 7.0, 8.0, 9.0 e 10.0
- Paralelizável: Sim; pode evoluir junto com 4.0 e 5.0.

## Rastreabilidade

- Esta tarefa cobre: US-01 diretamente, US-03 como suporte; RF-03 e parte do RF-04.
- Evidência esperada: tarifa completa válida torna acomodação elegível; sobreposição equivalente retorna `RATE_PERIOD_OVERLAP`.

## Detalhes de Implementação

Commands previstos: `CreateCommercialRateCommand`, `UpdateCommercialRateCommand` e `DeleteCommercialRateCommand`. A condição de overlap é:

~~~text
existing.ValidFrom <= candidate.ValidTo
AND candidate.ValidFrom <= existing.ValidTo
~~~

e só se aplica quando acomodação, `conditionCode`, `policyId` e `mealPlan` coincidem. Períodos são inclusivos. O domínio não usa `decimal` para persistência monetária; requests são mapeados diretamente para `long` em centavos.

**Convenções da stack (das skills consultadas):**

- Manter regras de período e dinheiro no domínio; FluentValidation cobre shape e limites básicos.
- Não carregar recursos de outra propriedade; handlers validam o escopo completo.
- Propagar `CancellationToken` e usar um único `SaveChangesAsync` por comando.
- Logar overlap com IDs e resultado, nunca valor integral ou snapshot.
- Testes seguem xUnit, AwesomeAssertions, AAA e `Theory` para a matriz temporal.

## Critérios de Sucesso (Verificáveis)

- [ ] Testes passam: `dotnet test ../localizestay-backend/LocalizeStay.sln --filter "FullyQualifiedName~CommercialRateTests"`
- [ ] Build compila sem erros: `dotnet build ../localizestay-backend/LocalizeStay.sln --no-restore`
- [ ] Formatação válida: `dotnet format ../localizestay-backend/LocalizeStay.sln --verify-no-changes --no-restore`
- [ ] Períodos sobrepostos equivalentes retornam `RATE_PERIOD_OVERLAP`; condições diferentes podem coexistir.
- [ ] Tarifas persistem somente `BRL`, `mandatoryFeesIncluded = true` e valores em centavos.
- [ ] O mínimo de noites é selecionado pela tarifa vigente no check-in e diárias atravessadas usam a tarifa da própria data.
- [ ] Tarifa enviada é desativada com histórico; tarifa nunca enviada pode ser excluída.
