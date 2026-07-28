---
status: pending
parallelizable: false
blocked_by: ["8.0", "14.0"]
---

<task_context>
<domain>inventory/application/sellability</domain>
<type>implementation</type>
<scope>core_feature</scope>
<complexity>medium</complexity>
<dependencies>database</dependencies>
<unblocks>"18.0, 23.0, 25.0, 26.0, 31.0"</unblocks>
<vertical_slice>Os três gates de D02 são recalculados a cada mutação relevante, e os dois gates de D06 partem de allowlist configurada com default blocked.</vertical_slice>
</task_context>

# Tarefa 17.0: Recalcular os gates de vendabilidade e ativar a allowlist de curadoria

## Relacionada às User Stories

- [US-01] Registrar allotment (suporte — cedê-lo satisfaz o gate `activeAllotment`)
- [US-06] Medir oferta sem allotment (direta — a métrica depende do gate)

## Visão Geral

Três dos cinco gates de RN-07 pertencem a D02 e são recalculados pela própria F03 a partir de tabelas do mesmo schema: `validRate` (de `commercial_rates`), `testedChannel` (do canal operacional da F01) e `activeAllotment` (de `allotments`).

Os dois gates de D06 são alimentados por **allowlist explícita de `propertyId` em configuração, com default `blocked`**, até que D06 publique seus eventos. A ausência de uma propriedade na lista **nunca** significa aprovação.

## Requisitos

- Um único serviço de aplicação, `SellabilityRecalculator`, chamado por todos os handlers que alteram allotment ou tarifa. Nenhum caminho de mutação recalcula gate por conta própria.
- Recálculo acontece na **mesma transação** da mutação que o afeta — os gates de D02 são fortemente consistentes.
- Seção `Inventory:CurationSellability` com allowlist de `propertyId` aprovados e conteúdo aprovado, validada com `ValidateOnStart`: identificadores não vazios e únicos.
- Cada gate escrito registra a **origem** do valor (`configuration` ou `event`), visível na resposta de `sellability`.
- Mutação de tarifa comercial (F02) passa a disparar o recálculo do gate `validRate`, com teste de regressão da F02 garantindo que nada mais mudou.
- Gate `activeAllotment` considera allotment vigente na janela de noventa dias, conforme o `detail` do exemplo do contrato.

## Arquivos Envolvidos

- **Criar:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/Sellability/SellabilityRecalculator.cs`
  - `../localizestay-backend/tests/LocalizeStay.UnitTests/Inventory/SellabilityRecalculatorTests.cs`
- **Modificar:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/InventoryModule.cs` (options da allowlist com `ValidateOnStart` + registro do serviço)
  - `../localizestay-backend/src/LocalizeStay.Api/appsettings.json` (seção `Inventory:CurationSellability`)
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/CommercialOffers/CommercialRateCommands.cs` (disparar recálculo de `validRate`)
- **Referência:**
  - `.tasks/prd-controlar-allotment-disponibilidade-bloqueios-e-retencoes/adrs/adr-002.md`
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Infrastructure/Upstream/ConfiguredEligibilityValidators.cs` (padrão de allowlist com `ValidateOnStart`)
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Domain/Sellability/PropertySellability.cs` (criado em 8.0)
- **Skills para consultar durante implementação:**
  - `dotnet-architecture` — serviço de aplicação com ponto único de recálculo
  - `dotnet-dependency-config` — options com `Bind` + `Validate` + `ValidateOnStart`
  - `dotnet-production-readiness` — negação por padrão como guardrail de segurança

## Subtarefas

- [ ] 17.1 Implementar `SellabilityRecalculator` com um método por gate de D02 e um método `RecalculateAsync` que os avalia todos para uma propriedade.
- [ ] 17.2 Declarar `CurationSellabilityOptions` com as duas allowlists, registrar com `ValidateOnStart` e alimentar os gates de D06 com origem `configuration` e default `blocked`.
- [ ] 17.3 Chamar o recalculador a partir dos handlers de mutação de tarifa em `CommercialRateCommands.cs`, na mesma transação.
- [ ] 17.4 Testar: cada gate de D02 isoladamente; propriedade fora da allowlist fica `blocked`; allowlist inválida falha no startup; regressão da F02 verde.

## Sequenciamento

- Bloqueado por: 8.0, 14.0
- Desbloqueia: 18.0, 23.0, 25.0, 26.0, 31.0
- Paralelizável: Não; é o único ponto de escrita dos gates e altera `InventoryModule.cs` e `appsettings.json`, também tocados por 3.0 e 41.0.

## Rastreabilidade

- Esta tarefa cobre: RN-07 na camada de aplicação e ADR-002.
- Evidência esperada: `SellabilityRecalculatorTests` prova o default `blocked`; a métrica de "oferta sem allotment" (31.0) lê o gate `activeAllotment`; a suíte da F02 segue verde.

## Detalhes de Implementação

Configuração-alvo:

```json
"Inventory": {
  "CurationSellability": {
    "ApprovedPropertyIds": [],
    "ApprovedContentPropertyIds": []
  }
}
```

> **Listas vazias são o estado correto de partida.** Com default `blocked`, o erro possível é omitir uma propriedade aprovada — que produz oferta indisponível e é detectado em minutos pela Operação. O erro inverso, aprovar por omissão, produziria venda sem lastro, que é exatamente o que a F03 existe para impedir.

Pontos de chamada do recalculador:

| Mutação | Gate afetado | Task que chama |
|---|---|---|
| Criar, alterar ou cancelar allotment | `activeAllotment` | 18.0 |
| Criar, alterar ou excluir tarifa comercial | `validRate` | 17.0 (nesta tarefa) |
| Alterar canal operacional | `testedChannel` | 17.0 (recálculo completo) |
| `curadoria-qualidade.propriedade-suspensa` | `propertyApproved` | 25.0 |
| `curadoria-qualidade.propriedade-aprovada` | `propertyApproved` | 26.0 |
| `curadoria-qualidade.conteudo-aprovado` | `contentApproved` | 26.0 |

A troca da allowlist por consumo de evento real **não altera schema nem contrato**: passa a existir um publicador, e a allowlist é esvaziada.

**Convenções da stack (das skills consultadas):**

- Um único serviço de recálculo, chamado por todos os handlers que afetam gate (`dotnet-architecture`).
- Options validadas no startup com mensagem explícita, como `UpstreamEligibilityOptions` (`dotnet-dependency-config`).
- Negação por padrão; nenhum caminho interpreta ausência como aprovação (`dotnet-production-readiness`).
- Métrica `inventory.sellability.gate_changed` por gate e resultado é adicionada na tarefa 32.0 (`dotnet-observability`).

## Critérios de Sucesso (Verificáveis)

- [ ] Testes passam: `dotnet test ../localizestay-backend/LocalizeStay.sln --filter "FullyQualifiedName~SellabilityRecalculatorTests"`
- [ ] Build compila sem erros: `dotnet build ../localizestay-backend/LocalizeStay.sln --no-restore`
- [ ] Formatação válida: `dotnet format ../localizestay-backend/LocalizeStay.sln --verify-no-changes --no-restore`
- [ ] Propriedade ausente da allowlist tem `propertyApproved = blocked` com origem `configuration`.
- [ ] Allowlist com identificador vazio ou duplicado falha no startup.
- [ ] Mutar tarifa comercial recalcula `validRate` na mesma transação.
- [ ] Regressão da F02 verde: `dotnet test ../localizestay-backend/tests/LocalizeStay.IntegrationTests --filter "FullyQualifiedName~CommercialOfferEndpointsTests"`
