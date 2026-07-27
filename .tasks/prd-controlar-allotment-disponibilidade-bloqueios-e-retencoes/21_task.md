---
status: pending
parallelizable: true
blocked_by: ["1.0", "11.0", "14.0", "15.0", "16.0"]
---

<task_context>
<domain>inventory/application/inventory-blocks</domain>
<type>implementation</type>
<scope>core_feature</scope>
<complexity>medium</complexity>
<dependencies>database</dependencies>
<unblocks>"29.0"</unblocks>
<vertical_slice>Antes de confirmar um bloqueio emergencial, o operador vê quantas reservas e retenções serão afetadas — sem que a prévia altere nada.</vertical_slice>
</task_context>

# Tarefa 21.0: Consultar bloqueios e simular impacto

## Relacionada às User Stories

- [US-03] Bloquear datas imediatamente (cobertura direta — a confirmação explícita do emergencial depende da prévia)
- [US-02] Diagnosticar a data (suporte)

## Visão Geral

Três operações de leitura: `listInventoryBlocks`, `getInventoryBlock` e `previewInventoryBlockImpact`.

A prévia de impacto é o que alimenta a confirmação explícita exigida pelo PRD: antes de concluir um bloqueio emergencial, a tela apresenta quantas reservas confirmadas e retenções serão afetadas. **A simulação lê e não escreve — nenhum evento, nenhuma mutação, nenhum lock retido.**

## Requisitos

- `previewInventoryBlockImpact` devolve `wouldBeAccepted`, `rejectionCode`, `affectedReservationCount`, `invalidatedHoldCount`, `freeBalanceByDate`, `affectedReservations` e `invalidatedHolds`.
- A simulação **não** produz evento, **não** escreve em `daily_inventory` e **não** grava auditoria de mutação.
- A prévia é indicativa: entre a prévia e a confirmação o saldo pode mudar. Um `422` no POST subsequente é resposta legítima, não falha da prévia — isso precisa estar no XML doc do handler.
- Reservas e retenções aparecem **apenas** por identificador, período e quantidade. Nenhum dado do viajante.
- Listagem paginada com `_page`/`_size` (teto 100) e filtros do contrato; arrays vazios como `[]`.
- Leituras usam `AsNoTracking` e projeção direta para DTO.
- Retenção vencida não conta como retida (guarda de ADR-004), mesmo antes de a varredura processá-la.

## Arquivos Envolvidos

- **Criar:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/InventoryBlocks/InventoryBlockQueries.cs`
  - `../localizestay-backend/tests/LocalizeStay.UnitTests/Inventory/InventoryBlockImpactPreviewTests.cs`
- **Referência:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Domain/DailyInventories/InventoryLedger.cs` (`BlockImpact`, criado em 11.0)
  - `.tasks/prd-controlar-allotment-disponibilidade-bloqueios-e-retencoes/api-contract.md` (exemplo de resposta de `impact-preview`)
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/CommercialOffers/CommercialOfferQueries.cs` (padrão de query paginada do módulo)
- **Skills para consultar durante implementação:**
  - `dotnet-performance` — `AsNoTracking`, projeção, paginação eficiente
  - `restful-api` — paginação `_page`/`_size`, arrays vazios como `[]`
  - `dotnet-testing` — AAA cobrindo aceitação e recusa simuladas

## Subtarefas

- [ ] 21.1 Implementar `ListInventoryBlocksQueryHandler` com paginação e os filtros do contrato.
- [ ] 21.2 Implementar `GetInventoryBlockQueryHandler` com projeção direta e `404 BLOCK_NOT_FOUND`.
- [ ] 21.3 Implementar `PreviewInventoryBlockImpactQueryHandler` reutilizando a apuração de impacto do ledger, sem escrita nem evento.
- [ ] 21.4 Testar: prévia de planejado aceito e recusado com `rejectionCode`, prévia de emergencial com reservas e retenções alcançadas, retenção vencida ignorada, e ausência de qualquer escrita.

## Sequenciamento

- Bloqueado por: 1.0, 11.0, 14.0, 15.0, 16.0
- Desbloqueia: 29.0
- Paralelizável: Sim; cria arquivos exclusivos, disjuntos de 19.0 e 20.0.

## Rastreabilidade

- Esta tarefa cobre: RF-02 na parte de leitura e a exigência do PRD de que o bloqueio emergencial "apresente, antes de concluir, quantas reservas confirmadas e retenções serão afetadas".
- Evidência esperada: `InventoryBlockImpactPreviewTests` prova a simulação sem efeito colateral.

## Detalhes de Implementação

Resposta-alvo da prévia, conforme o contrato:

```json
{
  "wouldBeAccepted": true,
  "rejectionCode": null,
  "affectedReservationCount": 1,
  "invalidatedHoldCount": 2,
  "freeBalanceByDate": [
    { "date": "2026-09-14", "freeUnits": 0 },
    { "date": "2026-09-15", "freeUnits": 2 }
  ],
  "affectedReservations": [
    { "reservationId": "...", "checkIn": "2026-09-14", "checkOut": "2026-09-17", "units": 1 }
  ],
  "invalidatedHolds": [
    { "holdId": "...", "reservationIntentId": "...", "units": 1, "expiresAt": "..." }
  ]
}
```

Para `type: planned` que excede o saldo livre, a prévia devolve `wouldBeAccepted: false` e `rejectionCode: "INSUFFICIENT_FREE_BALANCE"` — a mesma informação que o POST devolveria, antecipada.

> **A prévia não pode adquirir lock.** Reutilizar o caminho de escrita do ledger "só para simular" seguraria linhas de `daily_inventory` durante uma leitura de tela. A apuração de impacto precisa ter um caminho de leitura próprio, sem `FOR UPDATE`.

Antes da Onda B, `invalidatedHoldCount` é sempre `0` e `invalidatedHolds` é `[]` — não há retenções ainda.

**Convenções da stack (das skills consultadas):**

- Queries com `AsNoTracking` e projeção direta para DTO, sem materializar entidade (`dotnet-performance`).
- Paginação `_page`/`_size` com teto de 100, padrão REST do projeto (`restful-api`).
- Nenhum dado do viajante em resposta ou log (`dotnet-production-readiness`).
- Testes AAA cobrindo aceitação, recusa e ausência de escrita (`dotnet-testing`).

## Critérios de Sucesso (Verificáveis)

- [ ] Testes passam: `dotnet test ../localizestay-backend/LocalizeStay.sln --filter "FullyQualifiedName~InventoryBlockImpactPreviewTests"`
- [ ] Build compila sem erros: `dotnet build ../localizestay-backend/LocalizeStay.sln --no-restore`
- [ ] Formatação válida: `dotnet format ../localizestay-backend/LocalizeStay.sln --verify-no-changes --no-restore`
- [ ] A prévia não altera nenhuma linha de `daily_inventory` e não grava nada na outbox.
- [ ] Prévia de planejado acima do saldo livre devolve `wouldBeAccepted: false` e `rejectionCode: "INSUFFICIENT_FREE_BALANCE"`.
- [ ] Prévia de emergencial devolve `wouldBeAccepted: true` mesmo sem saldo livre.
- [ ] Retenção com `expiresAt` no passado **não** aparece em `invalidatedHolds`.
- [ ] Listagem vazia devolve `[]`, nunca `null`.
- [ ] Nenhuma consulta desta tarefa emite `FOR UPDATE`.
