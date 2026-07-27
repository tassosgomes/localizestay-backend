---
status: pending
parallelizable: true
blocked_by: ["4.0", "17.0", "25.0"]
---

<task_context>
<domain>inventory/application/sellability</domain>
<type>integration</type>
<scope>core_feature</scope>
<complexity>medium</complexity>
<dependencies>database</dependencies>
<unblocks>"35.0"</unblocks>
<vertical_slice>Ao receber a aprovação de propriedade ou de conteúdo, o gate correspondente volta a satisfeito e a propriedade pode voltar a vender.</vertical_slice>
</task_context>

# Tarefa 26.0: Restabelecer os gates por aprovação de propriedade e de conteúdo

## Relacionada às User Stories

- [US-03] Restabelecer a venda quando a aprovação volta a vigorar (cobertura direta)

## Visão Geral

Contraparte da tarefa 25.0. `curadoria-qualidade.propriedade-aprovada` satisfaz o gate `propertyApproved` **e encerra o bloqueio de origem `curationSuspension`** — que é o único caminho para encerrá-lo, já que a remoção manual é recusada. `curadoria-qualidade.conteudo-aprovado` satisfaz o gate `contentApproved`.

## Requisitos

- Consumo idempotente por `eventId`; reprocessar não duplica efeito.
- `CurationPropertyApprovedHandler` marca `propertyApproved` como `satisfied` com origem `event` **e** encerra os bloqueios ativos de origem `curationSuspension` da propriedade, devolvendo a capacidade pelo `InventoryLedger`.
- `CurationContentApprovedHandler` marca apenas `contentApproved` como `satisfied` com origem `event`; **não** mexe em bloqueio algum.
- O encerramento do bloqueio de curadoria produz `inventario-liberado`, por acomodação, na mesma transação da devolução.
- Assim como na suspensão, o processamento é **por acomodação, cada uma em sua transação**.
- A ordem dos eventos não pode corromper o estado: aprovação seguida de suspensão deixa a propriedade bloqueada; suspensão seguida de aprovação deixa vendável.
- Aprovar não satisfaz nenhum outro gate. Se `validRate`, `testedChannel` ou `activeAllotment` estiverem `blocked`, `sellable` continua `false`.

## Arquivos Envolvidos

- **Criar:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/Sellability/CurationPropertyApprovedHandler.cs`
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/Sellability/CurationContentApprovedHandler.cs`
  - `../localizestay-backend/tests/LocalizeStay.UnitTests/Inventory/CurationSellabilityHandlerTests.cs`
- **Referência:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/Sellability/CurationPropertySuspendedHandler.cs` (criado em 25.0)
  - `../localizestay-backend/src/Modules/Curation/LocalizeStay.Modules.Curation.Contracts/CurationSellabilityEvents.cs` (criado em 4.0)
  - `.tasks/prd-controlar-allotment-disponibilidade-bloqueios-e-retencoes/adrs/adr-002.md`
- **Skills para consultar durante implementação:**
  - `dotnet-architecture` — consumidores idempotentes, ponto único de recálculo de gate
  - `dotnet-observability` — log com `eventId` e resultado
  - `dotnet-testing` — teste de reordenação e reprocessamento de eventos

## Subtarefas

- [ ] 26.1 Implementar `CurationPropertyApprovedHandler`: gate `satisfied` com origem `event` e encerramento dos bloqueios `curationSuspension` por acomodação.
- [ ] 26.2 Implementar `CurationContentApprovedHandler`: apenas o gate `contentApproved`, sem tocar bloqueios.
- [ ] 26.3 Gravar `inventario-liberado` por acomodação na outbox, na mesma transação da devolução de capacidade.
- [ ] 26.4 Testar: deduplicação por `eventId`, reordenação suspensão↔aprovação, capacidade devolvida, e `sellable` permanecendo `false` quando outro gate está `blocked`.

## Sequenciamento

- Bloqueado por: 4.0, 17.0, 25.0
- Desbloqueia: 35.0
- Paralelizável: Sim; cria arquivos exclusivos. Depende de 25.0 apenas porque encerra o bloqueio que aquela tarefa cria.

## Rastreabilidade

- Esta tarefa cobre: a segunda metade de RF-05 ("restabelecê-la quando a aprovação voltar a vigorar") e o efeito declarado no contrato para `curadoria-qualidade.propriedade-aprovada`.
- Evidência esperada: `CurationSellabilityHandlerTests` prova a deduplicação e a reordenação, exigidas por ADR-002 como mitigação de evento perdido.

## Detalhes de Implementação

Efeito de cada evento, conforme o contrato:

| Evento | Gate | Bloqueio |
|---|---|---|
| `curadoria-qualidade.propriedade-aprovada` | `propertyApproved` → `satisfied` | Encerra o bloqueio `curationSuspension` |
| `curadoria-qualidade.propriedade-suspensa` | `propertyApproved` → `blocked` | Cria o bloqueio `curationSuspension` (tarefa 25.0) |
| `curadoria-qualidade.conteudo-aprovado` | `contentApproved` → `satisfied` | Nenhum |

Matriz de reordenação a testar:

```
suspensa(t1) → aprovada(t2)   ==>  vendável, bloqueio encerrado
aprovada(t1) → suspensa(t2)   ==>  bloqueada, bloqueio ativo
suspensa(t1) → suspensa(t1)   ==>  idêntico a uma única suspensão
aprovada(t1) → aprovada(t1)   ==>  idêntico a uma única aprovação
```

> Este é o único caminho para encerrar um bloqueio de curadoria. A tarefa 20.0 recusa a remoção manual com `CURATION_BLOCK_NOT_REMOVABLE` justamente porque a decisão pertence a D06 — e enquanto D06 não publica, a mudança acontece pela allowlist configurada da tarefa 17.0.

**Convenções da stack (das skills consultadas):**

- Consumidores idempotentes por `eventId`, seguindo `CurationOfferReturnedHandler` (`dotnet-architecture`).
- Devolução de capacidade sempre pelo `InventoryLedger`.
- Uma transação por acomodação, como na suspensão (ADR-001).
- Log estruturado com `eventId`, `propertyId`, `gate`, `result` (`dotnet-observability`).

## Critérios de Sucesso (Verificáveis)

- [ ] Testes passam: `dotnet test ../localizestay-backend/LocalizeStay.sln --filter "FullyQualifiedName~CurationSellabilityHandlerTests"`
- [ ] Build compila sem erros: `dotnet build ../localizestay-backend/LocalizeStay.sln --no-restore`
- [ ] Formatação válida: `dotnet format ../localizestay-backend/LocalizeStay.sln --verify-no-changes --no-restore`
- [ ] Aprovação encerra todos os bloqueios `curationSuspension` ativos da propriedade e devolve a capacidade.
- [ ] Aprovação de conteúdo **não** encerra bloqueio algum.
- [ ] As quatro sequências da matriz de reordenação produzem o estado esperado.
- [ ] Reprocessar o mesmo `eventId` não altera o estado uma segunda vez.
- [ ] `sellable` permanece `false` se `validRate`, `testedChannel` ou `activeAllotment` estiver `blocked`.
- [ ] `inventario-liberado` é gravado por acomodação na outbox.
