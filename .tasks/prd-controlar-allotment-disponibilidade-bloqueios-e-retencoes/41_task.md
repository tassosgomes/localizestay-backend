---
status: pending
parallelizable: false
blocked_by: ["39.0", "40.0"]
---

<task_context>
<domain>inventory/application/inventory-holds</domain>
<type>implementation</type>
<scope>core_feature</scope>
<complexity>high</complexity>
<dependencies>temporal</dependencies>
<unblocks>"42.0, 47.0"</unblocks>
<vertical_slice>Uma retenção vencida deixa de ocupar saldo no instante exato de expiresAt e produz seus eventos de forma confiável, mesmo em datas sem tráfego algum.</vertical_slice>
</task_context>

# Tarefa 41.0: Expirar retenções por varredura com guarda na leitura do saldo

> ⚠️ **`complexity: high` — exige revisão humana do plano antes de implementar.** A varredura e a guarda de leitura são as duas metades de uma decisão só (ADR-004). Entregar uma sem a outra produz janela morta de disponibilidade ou evento nunca publicado.

## Relacionada às User Stories

- [US-04] Acomodação separada durante o checkout (cobertura direta — a expiração é o que devolve a unidade quando o checkout não conclui)

## Visão Geral

Diferente da liberação explícita, a expiração **não tem requisitante**: ninguém chama a API quando o prazo vence. Alguma coisa precisa observar a passagem do tempo.

ADR-004 combina duas camadas com responsabilidades distintas: um `IHostedService` que varre retenções vencidas — **única fonte de transição de estado e de publicação de evento** — e uma guarda na leitura que desconsidera retenções vencidas mesmo antes de a varredura passar.

A guarda torna a latência da varredura invisível para a disponibilidade. O serviço torna o evento confiável.

## Requisitos

- `InventoryHoldExpirationService` como `IHostedService`, com intervalo inicial de trinta segundos, configurável em `Inventory:HoldExpiration`.
- Varredura com `FOR UPDATE SKIP LOCKED`, lote limitado a 200, ordenada por `expires_at` — permite mais de uma instância sem processamento duplicado.
- Cada lote muda o status para `expired`, decrementa `held_units` das noites via `InventoryLedger` e grava `retencao-expirada` e `inventario-liberado` na outbox — **tudo na mesma transação**.
- Só a transição de `held` para `expired` produz evento; reprocessamento não duplica.
- Escopo de DI próprio por ciclo, resolvendo `InventoryDbContext` como scoped. **Nunca** capturar um `DbContext` singleton.
- O serviço não inicia antes de a migração ser aplicada, seguindo o padrão de `ModuleDatabaseMigrationService`.
- Métrica `inventory.hold.expiration_backlog` — profundidade da fila de vencidas ainda não processadas — com alerta quando o lote satura em ciclos consecutivos.
- Ordem de aquisição de lock em `daily_inventory` segue a mesma regra de ADR-001: sempre `ORDER BY date` crescente.
- Shutdown gracioso: um ciclo em andamento conclui ou aborta sem deixar transação pela metade.

## Arquivos Envolvidos

- **Criar:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/InventoryHolds/InventoryHoldExpirationService.cs`
  - `../localizestay-backend/tests/LocalizeStay.UnitTests/Inventory/InventoryHoldExpirationServiceTests.cs`
- **Modificar:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/InventoryModule.cs` (options `HoldExpiration` + `AddHostedService`)
  - `../localizestay-backend/src/LocalizeStay.Api/appsettings.json` (seção `Inventory:HoldExpiration`)
- **Referência:**
  - `.tasks/prd-controlar-allotment-disponibilidade-bloqueios-e-retencoes/adrs/adr-004.md` (decisão completa)
  - `../localizestay-backend/src/BuildingBlocks/LocalizeStay.SharedKernel/DependencyInjection/ModuleDatabaseMigrationService.cs` (padrão de hosted service do projeto)
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Domain/DailyInventories/InventoryLedger.cs`
- **Skills para consultar durante implementação:**
  - `dotnet-architecture` — `IHostedService` com escopo de DI por ciclo
  - `dotnet-observability` — métrica de backlog, span `inventory.hold.expire`
  - `dotnet-code-quality` — `CancellationToken` em loop e batch, sem bloqueio
  - `dotnet-testing` — mockar `IClock`, testar cancelamento

## Subtarefas

- [ ] 41.1 Implementar o `IHostedService` com escopo de DI por ciclo, lote de 200 e `FOR UPDATE SKIP LOCKED` ordenado por `expires_at`.
- [ ] 41.2 Em cada lote: transicionar para `expired`, devolver capacidade pelo `InventoryLedger` e gravar os dois eventos na outbox, na mesma transação.
- [ ] 41.3 Registrar options com `ValidateOnStart` e o hosted service no `InventoryModule`, garantindo início após a migração; instrumentar `inventory.hold.expiration_backlog` e o span `inventory.hold.expire`.
- [ ] 41.4 Testar: retenção vencida não reduz saldo antes da varredura; a varredura publica os dois eventos exatamente uma vez; reprocessamento não duplica; cancelamento não deixa transação pela metade.

## Sequenciamento

- Bloqueado por: 39.0, 40.0
- Desbloqueia: 42.0, 47.0
- Paralelizável: Não; altera `InventoryModule.cs` e `appsettings.json`, também tocados por 3.0 e 17.0.

## Rastreabilidade

- Esta tarefa cobre: RF-07 integralmente e ADR-004; RN-04 e RN-05.
- Evidência esperada: `InventoryHoldExpirationServiceTests` prova a publicação única e a guarda de leitura; 47.0 prova o ciclo completo fim a fim.

## Detalhes de Implementação

Consulta da varredura, conforme ADR-004:

```sql
SELECT id FROM inventory.inventory_holds
 WHERE status = 'held' AND expires_at <= now()
 ORDER BY expires_at
 LIMIT 200
 FOR UPDATE SKIP LOCKED
```

Configuração-alvo:

```json
"Inventory": {
  "HoldExpiration": {
    "IntervalSeconds": 30,
    "BatchSize": 200
  }
}
```

As duas camadas e o que cada uma resolve:

| Camada | Garante | Se faltasse |
|---|---|---|
| Varredura (`IHostedService`) | Publicação confiável de `retencao-expirada` e `inventario-liberado`, inclusive para datas sem tráfego | D03 não encerraria a jornada; a métrica de expiração do PRD ficaria subnotificada |
| Guarda na leitura (`status='held' AND expires_at > now()`) | Capacidade vendável no instante exato de `expiresAt` | O intervalo da varredura viraria latência real de disponibilidade — venda perdida no ponto mais sensível do funil |

> **Trinta segundos de atraso na publicação do evento é irrelevante** para D03 e D09, porque a retenção dura quinze minutos e a guarda de leitura já eliminou o impacto na disponibilidade. O que não é aceitável é trinta segundos de unidade indevidamente indisponível.

Risco declarado a mitigar: esquecer a guarda em algum caminho de leitura produz saldo errado **silenciosamente**. Por isso o filtro vive em um único método do ledger (39.0), e nenhum query handler consulta `DbSet<InventoryHold>` diretamente.

**Convenções da stack (das skills consultadas):**

- `IHostedService` com `IServiceScopeFactory`; `DbContext` sempre scoped (`dotnet-architecture`).
- `CancellationToken` verificado no loop e entre lotes; nunca cancelar após persistir (`dotnet-code-quality`).
- Métrica de backlog com alerta quando o lote satura em ciclos consecutivos (`dotnet-observability`).
- Nunca logar em loop — uma linha agregada por ciclo (`dotnet-production-readiness`).

## Critérios de Sucesso (Verificáveis)

- [ ] Testes passam: `dotnet test ../localizestay-backend/LocalizeStay.sln --filter "FullyQualifiedName~InventoryHoldExpirationServiceTests"`
- [ ] Build compila sem erros: `dotnet build ../localizestay-backend/LocalizeStay.sln --no-restore`
- [ ] Formatação válida: `dotnet format ../localizestay-backend/LocalizeStay.sln --verify-no-changes --no-restore`
- [ ] Retenção com `expiresAt` no passado **não** reduz `availableUnits`, mesmo antes de a varredura passar.
- [ ] A varredura publica `retencao-expirada` e `inventario-liberado` exatamente uma vez por retenção.
- [ ] Reprocessar o mesmo lote não gera evento duplicado.
- [ ] A consulta da varredura usa `FOR UPDATE SKIP LOCKED` e respeita `BatchSize`.
- [ ] O serviço resolve `InventoryDbContext` por escopo, nunca como singleton.
- [ ] Cancelamento durante um ciclo não deixa transação parcialmente aplicada.
- [ ] `inventory.hold.expiration_backlog` é emitida a cada ciclo.
