---
status: pending
parallelizable: true
blocked_by: ["14.0", "15.0", "16.0"]
---

<task_context>
<domain>inventory/application/availability</domain>
<type>implementation</type>
<scope>core_feature</scope>
<complexity>medium</complexity>
<dependencies>database</dependencies>
<unblocks>"27.0"</unblocks>
<vertical_slice>A Operação vê uma grade de datas por acomodação com cedido, comprometido, retido, bloqueado e disponível, e abre o detalhe de uma data sem saldo.</vertical_slice>
</task_context>

# Tarefa 24.0: Consultar o calendário de inventário e o detalhe da data

## Relacionada às User Stories

- [US-02] Enxergar num calendário o que foi cedido, comprometido, retido, bloqueado e o que restou, para diagnosticar uma data sem alternar telas (cobertura direta)

## Visão Geral

Duas operações de leitura que formam a tela de trabalho da Operação: `getInventoryCalendar` devolve a grade de até 92 dias por acomodação, e `getDailyInventoryDetail` faz o drill-down de uma célula sem saldo, mostrando qual parcela é comprometida, retida ou bloqueada — e o motivo de cada bloqueio.

Aqui a composição interna do saldo é **legítima e necessária**: é backoffice autenticado com `inventory:read`, não a vitrine pública.

## Requisitos

- `getInventoryCalendar` aceita `from`/`to` obrigatórios com teto de 92 dias, e os filtros `accommodationId` e `onlyUnavailable`.
- Cada dia da grade traz `allottedUnits`, `committedUnits`, `heldUnits`, `blockedUnits`, `availableUnits` e a lista de bloqueios ativos com tipo, motivo, nota e unidades.
- **Datas sem allotment aparecem na grade** com `allottedUnits: 0` — a grade nunca tem buraco.
- Arrays vazios são sempre `[]`.
- `belowCommercialFloor` por acomodação e `sellable` da propriedade aparecem na resposta.
- `getDailyInventoryDetail` traz também `allotmentId`, `commitments` e `holds`, cada um **apenas por identificador, período e quantidade**.
- Retenção vencida não conta como retida nem aparece em `holds` (guarda de ADR-004).
- Leituras com `AsNoTracking`, projeção direta e range scan por `(accommodation_id, date)`.

## Arquivos Envolvidos

- **Criar:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/Availability/InventoryCalendarQueries.cs`
  - `../localizestay-backend/tests/LocalizeStay.UnitTests/Inventory/InventoryCalendarQueryHandlerTests.cs`
- **Referência:**
  - `.tasks/prd-controlar-allotment-disponibilidade-bloqueios-e-retencoes/api-contract.md` (exemplos de `inventory-calendar` e `daily-inventory/{date}`)
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/Inventory/InventoryDtos.cs` (criado em 15.0)
  - `.tasks/prd-controlar-allotment-disponibilidade-bloqueios-e-retencoes/adrs/adr-001.md` (o range scan que a grade usa)
- **Skills para consultar durante implementação:**
  - `dotnet-performance` — range scan, `AsNoTracking`, ausência de N+1 ao carregar bloqueios por data
  - `restful-api` — arrays vazios como `[]`, validação de janela
  - `dotnet-testing` — AAA cobrindo grade com buracos e drill-down

## Subtarefas

- [ ] 24.1 Implementar `GetInventoryCalendarQueryHandler` com a grade completa por acomodação, preenchendo datas sem allotment com zeros.
- [ ] 24.2 Anexar os bloqueios ativos de cada data em uma única consulta, sem N+1, e aplicar o filtro `onlyUnavailable`.
- [ ] 24.3 Implementar `GetDailyInventoryDetailQueryHandler` com `commitments` e `holds` por identificador e quantidade.
- [ ] 24.4 Testar: grade sem buracos, teto de 92 dias, `onlyUnavailable`, drill-down com composição e retenção vencida ausente.

## Sequenciamento

- Bloqueado por: 14.0, 15.0, 16.0
- Desbloqueia: 27.0
- Paralelizável: Sim; cria arquivos exclusivos.

## Rastreabilidade

- Esta tarefa cobre: RF-04 na parte de calendário, com o primeiro critério de aceite ("enxergar qual parcela é comprometida, retida ou bloqueada e o motivo de cada bloqueio").
- Evidência esperada: `InventoryCalendarQueryHandlerTests` prova a grade sem buracos e o drill-down.

## Detalhes de Implementação

Grade-alvo, conforme o contrato:

```json
{
  "from": "2026-09-14", "to": "2026-09-16", "sellable": true,
  "accommodations": [{
    "accommodationId": "...", "accommodationName": "Chalé Vista Mar",
    "belowCommercialFloor": false,
    "days": [
      { "date": "2026-09-14", "allottedUnits": 3, "committedUnits": 1, "heldUnits": 1,
        "blockedUnits": 1, "availableUnits": 0,
        "blocks": [{ "blockId": "...", "type": "planned", "reason": "maintenance",
                     "reasonNote": "Troca do ar-condicionado.", "units": 1 }] },
      { "date": "2026-09-15", "allottedUnits": 3, "committedUnits": 1, "heldUnits": 0,
        "blockedUnits": 0, "availableUnits": 2, "blocks": [] }
    ]
  }]
}
```

Diferença deliberada em relação a `getAvailability`:

| | `getAvailability` | Calendário e detalhe |
|---|---|---|
| Acesso | Público | `inventory:read` |
| Composição do saldo | **Nunca** | Sim — é o propósito |
| Motivo do bloqueio | **Nunca** | Sim |
| Identificador de reserva | Não | Sim, apenas o `id` |

> Dados do viajante pertencem a D03 e **não** aparecem nem aqui. Reservas e retenções entram por identificador, período e quantidade — nada mais.

Antes da Onda B, `heldUnits` é sempre `0` e `holds` é `[]`.

**Convenções da stack (das skills consultadas):**

- Uma consulta para a grade e outra para os bloqueios do período, unidas em memória — nunca uma consulta por data (`dotnet-performance`).
- `AsNoTracking` e projeção direta para DTO.
- Arrays vazios serializados como `[]` (`restful-api`).
- Nenhum dado do viajante em resposta ou log (`dotnet-production-readiness`).

## Critérios de Sucesso (Verificáveis)

- [ ] Testes passam: `dotnet test ../localizestay-backend/LocalizeStay.sln --filter "FullyQualifiedName~InventoryCalendarQueryHandlerTests"`
- [ ] Build compila sem erros: `dotnet build ../localizestay-backend/LocalizeStay.sln --no-restore`
- [ ] Formatação válida: `dotnet format ../localizestay-backend/LocalizeStay.sln --verify-no-changes --no-restore`
- [ ] Uma grade de 30 dias sobre uma acomodação com allotment em apenas 10 devolve 30 dias, sendo 20 com `allottedUnits: 0`.
- [ ] Janela de 93 dias produz `DATE_RANGE_TOO_LARGE`.
- [ ] `onlyUnavailable: true` devolve apenas dias com `availableUnits: 0`.
- [ ] O detalhe de uma data sem saldo mostra a composição e o motivo de cada bloqueio ativo.
- [ ] Retenção com `expiresAt` no passado não aparece em `holds` nem soma em `heldUnits`.
- [ ] O número de consultas ao banco não cresce com a quantidade de dias da grade.
