---
status: pending
parallelizable: false
blocked_by: ["11.0", "37.0"]
---

<task_context>
<domain>inventory/domain/daily-inventory</domain>
<type>implementation</type>
<scope>core_feature</scope>
<complexity>high</complexity>
<dependencies>database</dependencies>
<unblocks>"41.0, 42.0"</unblocks>
<vertical_slice>Reter, liberar e comprometer passam pelo mesmo ponto único de mutação de saldo, com a mesma sequência de lock e a mesma guarda de retenção vencida.</vertical_slice>
</task_context>

# Tarefa 39.0: Estender o `InventoryLedger` com reter, liberar e comprometer

> ⚠️ **`complexity: high` — exige revisão humana do plano antes de implementar.** As três operações compartilham a sequência de lock e a guarda de retenção vencida. Separá-las duplicaria a regra em três lugares — e ADR-001 identifica a duplicação de caminho de escrita como o risco principal do desenho.

## Relacionada às User Stories

- [US-04] Acomodação separada durante o checkout (cobertura direta)

## Visão Geral

Completa o `InventoryLedger` com as três operações da Onda B: `TryHoldAsync`, `ReleaseAsync` e `CommitAsync`.

`TryHoldAsync` é o ponto de maior concorrência do sistema: é onde se decide se a última unidade de uma data pertence a uma jornada de checkout ou a outra. A decisão precisa ser tomada dentro da mesma transação que escreve, sobre linhas travadas em ordem determinística.

## Requisitos

- Mesma regra de lock da Onda A: `SELECT ... FOR UPDATE` com `ORDER BY date` crescente, **sem exceção**.
- `TryHoldAsync` verifica **todas** as noites antes de gravar. Se qualquer noite não tiver saldo, **nenhuma capacidade é separada** e o resultado traz as datas indisponíveis.
- Retenção vencida não conta como retida na avaliação de saldo (guarda de ADR-004), mesmo que a varredura ainda não a tenha processado.
- `ReleaseAsync` é **idempotente**: retenção já expirada, liberada ou invalidada não devolve capacidade uma segunda vez. Retenção `committed` é recusada com `HOLD_ALREADY_COMMITTED`.
- `CommitAsync` com retenção vigente migra capacidade de retida para comprometida **sem alterar o total disponível**.
- `CommitAsync` com retenção expirada ou invalidada **revalida o saldo**: havendo disponibilidade, comprometê-la e marcar `revalidated: true`; não havendo, recusar com `COMMITMENT_WITHOUT_AVAILABILITY` sem comprometer capacidade inexistente.
- Os resultados (`HoldOutcome`, `CommitOutcome`) carregam os motivos de recusa e as datas envolvidas, para que os handlers montem `metadata`.

## Arquivos Envolvidos

- **Criar:**
  - `../localizestay-backend/tests/LocalizeStay.UnitTests/Inventory/InventoryLedgerHoldTests.cs`
- **Modificar:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Domain/DailyInventories/InventoryLedger.cs` (três operações)
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Domain/DailyInventories/InventoryLedgerResults.cs` (`HoldOutcome`, `CommitOutcome`, motivos)
- **Referência:**
  - `.tasks/prd-controlar-allotment-disponibilidade-bloqueios-e-retencoes/adrs/adr-001.md`
  - `.tasks/prd-controlar-allotment-disponibilidade-bloqueios-e-retencoes/adrs/adr-004.md`
  - `.tasks/prd-controlar-allotment-disponibilidade-bloqueios-e-retencoes/api-contract.yaml` (`x-backend-notes` de `createInventoryHold` e `releaseInventoryHold`)
- **Skills para consultar durante implementação:**
  - `dotnet-architecture` — ponto único de mutação, serviço de domínio
  - `dotnet-performance` — SQL bruto para `FOR UPDATE` (desvio aprovado)
  - `dotnet-code-quality` — `CancellationToken`, métodos curtos
  - `dotnet-testing` — mockar apenas `IClock`; nunca o domínio

## Subtarefas

- [ ] 39.1 Implementar `TryHoldAsync`: carregar as noites com lock ordenado, avaliar todas antes de gravar, e devolver `HoldOutcome` com as datas indisponíveis em caso de recusa.
- [ ] 39.2 Implementar `ReleaseAsync` idempotente, comparando o estado antes de devolver capacidade e recusando retenção já comprometida.
- [ ] 39.3 Implementar `CommitAsync` nos dois caminhos: retenção vigente (migração sem alterar o disponível) e retenção expirada (revalidação com `revalidated: true` ou recusa).
- [ ] 39.4 Testar: recusa sem separar capacidade, idempotência da liberação, migração sem alterar o total, revalidação com e sem saldo, e ordem de aquisição de lock.

## Sequenciamento

- Bloqueado por: 11.0, 37.0
- Desbloqueia: 41.0, 42.0
- Paralelizável: Não; modifica o `InventoryLedger`, ponto único de mutação de saldo.

## Rastreabilidade

- Esta tarefa cobre: RF-06, RF-07 e RF-08 na camada de domínio; RN-04, RN-05 e RN-06.
- Evidência esperada: `InventoryLedgerHoldTests` prova as recusas e a migração; 48.0 prova a serialização sob concorrência real.

## Detalhes de Implementação

Comportamento de `CommitAsync`, conforme o contrato:

| Estado da retenção | Efeito | Resposta |
|---|---|---|
| `held` e vigente | Capacidade migra de retida para comprometida; **total disponível não muda** | `201`, `revalidated: false` |
| `expired` ou `invalidated`, com saldo | Revalida e compromete | `201`, `revalidated: true` |
| `expired` ou `invalidated`, sem saldo | Não compromete nada | `422 COMMITMENT_WITHOUT_AVAILABILITY` |
| `committed` | Idempotente — devolve o comprometimento existente | `201` (réplica) |

Idempotência de `ReleaseAsync` — a garantia mais importante desta tarefa:

```
Dado uma retenção já expirada (capacidade já devolvida pela varredura)
Quando reserva.nao-concluida chega para ela
Então nenhuma capacidade é devolvida uma segunda vez
E a operação responde 204
```

> Este é o terceiro critério de aceite de RF-07 e a razão pela qual a transição de estado precisa acontecer em um único lugar. Se a varredura devolvesse a capacidade e a liberação explícita devolvesse de novo, o saldo cresceria sozinho — e o sistema passaria a vender unidades que não existem.

Migração sem alterar o total:

```
antes:  allotted=3  committed=0  held=1  blocked=0  available=2
depois: allotted=3  committed=1  held=0  blocked=0  available=2   ← inalterado
```

**Convenções da stack (das skills consultadas):**

- Mesma sequência de lock da Onda A: `ORDER BY date` crescente, obrigatória (ADR-001).
- SQL bruto para `FOR UPDATE`; desvio aprovado de `dotnet-performance`.
- A guarda `status = 'held' AND expires_at > now()` vive em um único método de consulta (ADR-004).
- `CancellationToken` propagado; nunca cancelar após persistir (`dotnet-code-quality`).
- Spans `inventory.hold.acquire` e `inventory.hold.expire` entram na tarefa 41.0 (`dotnet-observability`).

## Critérios de Sucesso (Verificáveis)

- [ ] Testes passam: `dotnet test ../localizestay-backend/LocalizeStay.sln --filter "FullyQualifiedName~InventoryLedgerHoldTests"`
- [ ] Build compila sem erros: `dotnet build ../localizestay-backend/LocalizeStay.sln --no-restore`
- [ ] Formatação válida: `dotnet format ../localizestay-backend/LocalizeStay.sln --verify-no-changes --no-restore`
- [ ] Retenção recusada por saldo **não** altera nenhum contador em nenhuma data.
- [ ] `HoldOutcome` de recusa traz as datas indisponíveis.
- [ ] Liberar retenção já expirada não devolve capacidade uma segunda vez.
- [ ] Liberar retenção `committed` é recusado com `HOLD_ALREADY_COMMITTED`.
- [ ] Comprometer retenção vigente mantém `availableUnits` inalterado.
- [ ] Comprometer retenção expirada sem saldo produz `COMMITMENT_WITHOUT_AVAILABILITY` e não compromete nada.
- [ ] Retenção vencida não reduz o saldo antes de a varredura passar.
- [ ] Toda consulta com `FOR UPDATE` no arquivo contém `ORDER BY date`.
