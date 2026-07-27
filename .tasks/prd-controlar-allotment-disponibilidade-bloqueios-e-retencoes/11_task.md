---
status: pending
parallelizable: false
blocked_by: ["5.0", "6.0", "7.0"]
---

<task_context>
<domain>inventory/domain/daily-inventory</domain>
<type>implementation</type>
<scope>core_feature</scope>
<complexity>high</complexity>
<dependencies>database</dependencies>
<unblocks>"18.0, 19.0, 20.0, 21.0, 25.0, 39.0"</unblocks>
<vertical_slice>Toda mutação de saldo passa por um único serviço que carrega as noites com lock ordenado, valida as invariantes de RN-03 e aplica o delta na mesma transação.</vertical_slice>
</task_context>

# Tarefa 11.0: Implementar o `InventoryLedger` com bloqueio pessimista ordenado

> ⚠️ **`complexity: high` — exige revisão humana do plano antes de implementar.** Este é o ponto de acoplamento irredutível da F03: carregar, validar e escrever precisam viver na mesma unidade transacional, sob pena de reintroduzir a corrida que a feature existe para eliminar.

## Relacionada às User Stories

- [US-01] Registrar allotment para gerar saldo vendável (direta)
- [US-03] Bloquear datas imediatamente (direta)
- [US-04] Manter a acomodação separada durante o checkout (suporte — as operações de retenção entram em 39.0)

## Visão Geral

O `InventoryLedger` é o serviço de domínio que concentra **toda** mutação de saldo. Nenhum handler altera contadores diretamente. Ele carrega as noites envolvidas com bloqueio pessimista em ordem determinística, valida as invariantes de RN-03 e aplica o delta.

Esta tarefa entrega o escopo da Onda A: carregar noites, materializar allotment e aplicar bloqueio. As operações de retenção (`TryHoldAsync`, `ReleaseAsync`, `CommitAsync`) entram na tarefa 39.0, sobre o mesmo arquivo.

## Requisitos

- `LoadForUpdateAsync` executa `SELECT ... FOR UPDATE` **sempre com `ORDER BY date` crescente**, sem exceção. É a única mitigação de deadlock do plano.
- O bloqueio pessimista incide apenas sobre as linhas das noites da operação, nunca sobre a acomodação inteira. Duas operações em datas disjuntas da mesma acomodação não se serializam.
- Datas sem linha em `daily_inventory` são tratadas como saldo zero; a materialização de allotment cria as linhas faltantes.
- `MaterializeAllotmentAsync` insere ou atualiza uma linha por data do período, na mesma transação que grava o `Allotment`.
- Recusa de redução de allotment abaixo do comprometido, com `ALLOTMENT_BELOW_COMMITTED` e `metadata.conflictingDates`.
- Recusa de cancelamento de allotment com datas comprometidas ou retidas.
- `ApplyBlockAsync` distingue os dois tipos: `planned` recusa quando excede o saldo livre de qualquer data (`INSUFFICIENT_FREE_BALANCE` + `metadata.freeBalanceByDate`); `emergency` é sempre aceito e devolve o impacto apurado.
- Nível de isolamento permanece o padrão `READ COMMITTED`; o `FOR UPDATE` é suficiente porque toda leitura de decisão acontece dentro da transação que escreve.
- Toda leitura de saldo desconsidera retenções vencidas (`status = 'held' AND expires_at > now()`) — a guarda é implementada aqui desde já, em um único método, para que a Onda B não precise espalhá-la.

## Arquivos Envolvidos

- **Criar:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Domain/DailyInventories/InventoryLedger.cs`
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Domain/DailyInventories/InventoryLedgerResults.cs`
  - `../localizestay-backend/tests/LocalizeStay.UnitTests/Inventory/InventoryLedgerTests.cs`
- **Referência:**
  - `.tasks/prd-controlar-allotment-disponibilidade-bloqueios-e-retencoes/adrs/adr-001.md` (decisão completa, riscos e notas de implementação)
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Infrastructure/InventoryDbContext.cs`
  - `.tasks/prd-controlar-allotment-disponibilidade-bloqueios-e-retencoes/api-contract.yaml` (`x-backend-notes` de `createInventoryBlock`)
- **Skills para consultar durante implementação:**
  - `dotnet-architecture` — serviço de domínio, ponto único de mutação, exceções de domínio
  - `dotnet-performance` — SQL bruto para `FOR UPDATE` (desvio aprovado); EF não expõe bloqueio pessimista
  - `dotnet-code-quality` — `CancellationToken` propagado, métodos ≤ 50 linhas, ≤ 3 parâmetros
  - `dotnet-testing` — mockar apenas relógio e portas externas; nunca o próprio domínio

## Subtarefas

- [ ] 11.1 Declarar `IInventoryLedger` com as seis operações do contrato interno e implementar `LoadForUpdateAsync` com `FOR UPDATE` ordenado por data, materializando linhas ausentes como zero.
- [ ] 11.2 Implementar `MaterializeAllotmentAsync` (criar, alterar e cancelar), com as duas recusas de invariante e o preenchimento de `metadata.conflictingDates`.
- [ ] 11.3 Implementar `ApplyBlockAsync` para `planned` e `emergency`, devolvendo `BlockImpact` com reservas alcançadas, retenções a invalidar e saldo livre por data.
- [ ] 11.4 Declarar `HoldOutcome`, `CommitOutcome`, `BlockImpact` e os motivos de recusa em `InventoryLedgerResults.cs`; testar deltas, invariantes, recusas e ordem de aquisição.

## Sequenciamento

- Bloqueado por: 5.0, 6.0, 7.0
- Desbloqueia: 18.0, 19.0, 20.0, 21.0, 25.0, 39.0
- Paralelizável: Não; é o ponto de convergência de três entidades e a fonte única de mutação. Fragmentá-lo por operação duplicaria a sequência de lock em três lugares — exatamente o risco que ADR-001 pede para evitar.

## Rastreabilidade

- Esta tarefa cobre: RN-03 integralmente, RF-01 e RF-02 na camada de domínio, e ADR-001.
- Evidência esperada: `InventoryLedgerTests` prova as recusas e os deltas; a tarefa 36.0 prova por reconstrução que nenhuma escrita escapou do ledger; a tarefa 48.0 prova a serialização sob concorrência real.

## Detalhes de Implementação

Interface-alvo, conforme a TechSpec (as três últimas são implementadas em 39.0):

```csharp
internal interface IInventoryLedger
{
    Task<IReadOnlyList<DailyInventory>> LoadForUpdateAsync(
        Guid accommodationId, DateOnly from, DateOnly toInclusive, CancellationToken cancellationToken);

    Task MaterializeAllotmentAsync(Allotment allotment, CancellationToken cancellationToken);

    Task ApplyBlockAsync(InventoryBlock block, CancellationToken cancellationToken);

    Task<HoldOutcome> TryHoldAsync(HoldRequest request, CancellationToken cancellationToken);        // 39.0

    Task ReleaseAsync(InventoryHold hold, HoldReleaseReason reason, CancellationToken cancellationToken); // 39.0

    Task<CommitOutcome> CommitAsync(InventoryHold hold, Guid reservationId, CancellationToken cancellationToken); // 39.0
}
```

Consulta de carga com lock — **a ordenação não é opcional**:

```sql
SELECT * FROM inventory.daily_inventory
 WHERE accommodation_id = @accommodationId
   AND date BETWEEN @from AND @toInclusive
 ORDER BY date
 FOR UPDATE
```

Invariantes que este serviço protege:

| Operação | Recusa | `code` | `metadata` |
|---|---|---|---|
| Alterar allotment | Nova quantidade abaixo do comprometido em alguma data | `ALLOTMENT_BELOW_COMMITTED` | `conflictingDates` |
| Cancelar allotment | Alguma data com capacidade comprometida ou retida | `ALLOTMENT_BELOW_COMMITTED` | `conflictingDates` |
| Bloqueio `planned` | Quantidade acima do saldo livre de alguma data | `INSUFFICIENT_FREE_BALANCE` | `freeBalanceByDate` |
| Bloqueio `emergency` | Nenhuma — é sempre aceito | — | — |

> **Bloqueio planejado só alcança saldo livre.** Jamais retenção vigente, jamais reserva confirmada. Bloqueio emergencial invalida retenções, mas **não cancela nem altera reserva alguma** (RN-16) — apenas produz `bloqueio-afeta-reserva` para D05.

**Convenções da stack (das skills consultadas):**

- SQL bruto para `FOR UPDATE` é desvio deliberado e aprovado de `dotnet-performance`: o EF Core não expõe bloqueio pessimista e a invariante de concorrência exige o comando explícito.
- `InventoryDbContext` é o Unit of Work; nenhum repositório wrapper (`dotnet-architecture`).
- `CancellationToken` propagado em toda operação assíncrona; nunca cancelar após persistir (`dotnet-code-quality`).
- Spans `inventory.ledger.load`, `inventory.allotment.materialize` e `inventory.block.apply` são adicionados na tarefa 32.0 (`dotnet-observability`).
- Testes cobrem o caminho de `CancellationToken` (`dotnet-testing`).

## Critérios de Sucesso (Verificáveis)

- [ ] Testes passam: `dotnet test ../localizestay-backend/LocalizeStay.sln --filter "FullyQualifiedName~InventoryLedgerTests"`
- [ ] Build compila sem erros: `dotnet build ../localizestay-backend/LocalizeStay.sln --no-restore`
- [ ] Formatação válida: `dotnet format ../localizestay-backend/LocalizeStay.sln --verify-no-changes --no-restore`
- [ ] Toda consulta com `FOR UPDATE` no arquivo contém `ORDER BY date` — verificável por inspeção e por teste que casa o SQL emitido.
- [ ] Redução abaixo do comprometido lança `BusinessRuleViolationException` com `code = ALLOTMENT_BELOW_COMMITTED` e `metadata.conflictingDates` preenchido.
- [ ] Bloqueio `planned` acima do saldo livre lança `INSUFFICIENT_FREE_BALANCE` com `metadata.freeBalanceByDate`.
- [ ] Bloqueio `emergency` é aceito mesmo sem saldo livre e devolve `BlockImpact` com as reservas alcançadas.
- [ ] Data sem linha em `daily_inventory` é tratada como zero, nunca como indefinida.
- [ ] Nenhum outro arquivo do módulo executa `UPDATE` em `daily_inventory` — verificável por `grep` e certificado pela tarefa 36.0.
