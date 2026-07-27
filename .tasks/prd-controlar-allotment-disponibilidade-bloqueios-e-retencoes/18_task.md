---
status: pending
parallelizable: true
blocked_by: ["1.0", "11.0", "14.0", "15.0", "16.0", "17.0"]
---

<task_context>
<domain>inventory/application/allotments</domain>
<type>implementation</type>
<scope>core_feature</scope>
<complexity>high</complexity>
<dependencies>database</dependencies>
<unblocks>"28.0, 31.0"</unblocks>
<vertical_slice>Ceder allotment materializa saldo em todas as datas do período; alterar e cancelar respeitam o comprometido; consultar devolve a cessão vigente.</vertical_slice>
</task_context>

# Tarefa 18.0: Ceder, alterar, cancelar e consultar allotment

> ⚠️ **`complexity: high` — exige revisão humana do plano antes de implementar.** A recusa de redução abaixo do comprometido exige avaliar todas as datas do período dentro da mesma transação que altera o `Allotment` e rematerializa `daily_inventory`.

## Relacionada às User Stories

- [US-01] Registrar o allotment contratado para que a acomodação passe a ter saldo vendável (cobertura direta)

## Visão Geral

Cinco operações do contrato: `createAllotment`, `updateAllotment`, `cancelAllotment`, `listAllotments` e `getAllotment`. É a fatia que traduz RF-01 em capacidade diária consultável.

O ponto crítico é a distinção conceitual que o PRD faz: **allotment representa o contrato, não a operação do dia.** Reduzir allotment abaixo do comprometido é proibido; a ação corretiva é registrar um bloqueio, e o `detail` do erro precisa dizer isso.

## Requisitos

- `createAllotment` grava o `Allotment` e chama `InventoryLedger.MaterializeAllotmentAsync` na mesma transação, gerando `Inventário Diário` uniforme em todas as datas do período.
- Violação da constraint de exclusão é traduzida em `409 ALLOTMENT_PERIOD_OVERLAP`, com `metadata` contendo o allotment conflitante.
- `updateAllotment` honra `expectedRevision`; divergência produz `409 REVISION_MISMATCH`.
- Redução abaixo do comprometido produz `422 ALLOTMENT_BELOW_COMMITTED` com `metadata.conflictingDates` e `detail` indicando registrar bloqueio.
- `cancelAllotment` zera `allotted_units` das datas e é recusado se alguma data tiver capacidade comprometida ou retida.
- `units < 2` é **aceito**, com `belowCommercialFloor: true`.
- Toda mutação chama `SellabilityRecalculator` para o gate `activeAllotment` e grava trilha de auditoria com autor, horário e motivo.
- Acomodação inexistente ou de outra propriedade produz `404 ACCOMMODATION_NOT_FOUND`.
- Queries usam projeção `AsNoTracking` e paginação `_page`/`_size` com teto de 100.

## Arquivos Envolvidos

- **Criar:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/Allotments/AllotmentCommands.cs`
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/Allotments/AllotmentQueries.cs`
  - `../localizestay-backend/tests/LocalizeStay.UnitTests/Inventory/AllotmentCommandHandlerTests.cs`
- **Referência:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Domain/DailyInventories/InventoryLedger.cs` (criado em 11.0)
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/CommercialOffers/CommercialOfferCommands.cs` (padrão de Command/Handler do módulo)
  - `../localizestay-backend/src/BuildingBlocks/LocalizeStay.SharedKernel/Auditing/BusinessAuditWriter.cs`
  - `.tasks/prd-controlar-allotment-disponibilidade-bloqueios-e-retencoes/api-contract.md` (exemplos de `POST` e `PATCH` de allotment)
- **Skills para consultar durante implementação:**
  - `dotnet-architecture` — CQRS nativo, `InventoryDbContext` como Unit of Work
  - `dotnet-performance` — `AsNoTracking` e projeção EF nas queries
  - `dotnet-code-quality` — `CancellationToken`, métodos curtos, exceções específicas
  - `dotnet-testing` — AAA, mockar apenas ledger e relógio

## Subtarefas

- [ ] 18.1 Implementar `CreateAllotmentCommandHandler`, incluindo a tradução da violação de constraint em `409 ALLOTMENT_PERIOD_OVERLAP`.
- [ ] 18.2 Implementar `UpdateAllotmentCommandHandler` e `CancelAllotmentCommandHandler`, com `expectedRevision`, as recusas do ledger e a auditoria.
- [ ] 18.3 Implementar `ListAllotmentsQueryHandler` e `GetAllotmentQueryHandler` com projeção `AsNoTracking` e paginação.
- [ ] 18.4 Testar: materialização uniforme, sobreposição, `REVISION_MISMATCH`, `ALLOTMENT_BELOW_COMMITTED` com `metadata`, cancelamento recusado e `units = 1` aceito com sinalização.

## Sequenciamento

- Bloqueado por: 1.0, 11.0, 14.0, 15.0, 16.0, 17.0
- Desbloqueia: 28.0, 31.0
- Paralelizável: Sim; cria arquivos exclusivos, disjuntos das demais fatias de aplicação.

## Rastreabilidade

- Esta tarefa cobre: RF-01 integralmente na camada de aplicação, com os quatro critérios de aceite do PRD.
- Evidência esperada: `AllotmentCommandHandlerTests` prova os quatro critérios; 28.0 os expõe por HTTP; 36.0 prova a materialização por reconstrução.

## Detalhes de Implementação

Critérios de aceite do PRD mapeados para teste:

| Critério (RF-01) | Verificação |
|---|---|
| Cada data do período passa a ter total cedido igual à quantidade | Materialização uniforme em `daily_inventory` |
| Períodos sobrepostos na mesma acomodação são bloqueados | `409 ALLOTMENT_PERIOD_OVERLAP` |
| Redução abaixo do comprometido é bloqueada e o sistema indica registrar bloqueio | `422 ALLOTMENT_BELOW_COMMITTED` com `detail` orientativo |
| `units < 2` é aceito, sinalizado e fora da meta de cobertura | `belowCommercialFloor: true` |

Corpo do erro de redução, conforme o contrato:

```json
{
  "status": 422,
  "detail": "Existem datas com capacidade comprometida acima da nova quantidade. Registre um bloqueio para reduzir a venda sem alterar o contrato.",
  "code": "ALLOTMENT_BELOW_COMMITTED",
  "metadata": {
    "conflictingDates": [{ "date": "2026-09-14", "committedUnits": 3 }]
  }
}
```

> **Escrita amplificada é esperada:** um allotment de noventa dias materializa noventa linhas. Aceitável na escala do piloto (oito propriedades, janela de noventa dias), e monitorado antes de ampliar a janela.

O ator (`createdBy`/`updatedBy`) vem do JWT no endpoint e é passado ao Command. **Atores nunca vêm no corpo da requisição.**

**Convenções da stack (das skills consultadas):**

- Commands e Queries com handlers CQRS nativos, registrados por assembly (`dotnet-architecture`).
- Handlers usam `InventoryDbContext` diretamente, sem repositório — desvio aprovado.
- Nenhum handler altera `daily_inventory` diretamente; toda mutação passa pelo `InventoryLedger`.
- Auditoria de negócio via `BusinessAuditWriter<InventoryDbContext>`, na mesma `SaveChangesAsync`.
- Logs estruturados com `propertyId`, `accommodationId`, `allotmentId`, `operation`, `result` (`dotnet-observability`).
- Queries com `AsNoTracking` e projeção direta para DTO (`dotnet-performance`).

## Critérios de Sucesso (Verificáveis)

- [ ] Testes passam: `dotnet test ../localizestay-backend/LocalizeStay.sln --filter "FullyQualifiedName~AllotmentCommandHandlerTests"`
- [ ] Build compila sem erros: `dotnet build ../localizestay-backend/LocalizeStay.sln --no-restore`
- [ ] Formatação válida: `dotnet format ../localizestay-backend/LocalizeStay.sln --verify-no-changes --no-restore`
- [ ] Ceder allotment de 90 dias materializa 90 linhas com `allotted_units` igual à quantidade informada.
- [ ] Período sobreposto produz `ALLOTMENT_PERIOD_OVERLAP` com o allotment conflitante em `metadata`.
- [ ] `expectedRevision` obsoleto produz `REVISION_MISMATCH`.
- [ ] Redução abaixo do comprometido produz `ALLOTMENT_BELOW_COMMITTED` com `conflictingDates` preenchido.
- [ ] Cancelar allotment com data comprometida é recusado.
- [ ] `units = 1` é aceito e devolve `belowCommercialFloor: true`.
