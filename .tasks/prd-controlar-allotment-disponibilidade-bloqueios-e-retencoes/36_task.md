---
status: pending
parallelizable: true
blocked_by: ["14.0", "18.0", "19.0"]
---

<task_context>
<domain>inventory/testing/consistency</domain>
<type>testing</type>
<scope>core_feature</scope>
<complexity>high</complexity>
<dependencies>database</dependencies>
<unblocks>"48.0"</unblocks>
<vertical_slice>Os contadores materializados de daily_inventory reconstruídos a partir das fontes batem com o persistido, e operações concorrentes em datas sobrepostas concluem sem deadlock.</vertical_slice>
</task_context>

# Tarefa 36.0: Certificar a reconciliação do ledger e a ausência de deadlock

> ⚠️ **`complexity: high` — exige revisão humana do plano antes de implementar.** Este é o único teste que prova a decisão de persistir saldo derivado. É o contrapeso de ADR-001 e não pode ser fatiado sem perder o sentido.

## Relacionada às User Stories

- [US-01], [US-02], [US-03] (suporte — todo o valor da feature depende de o saldo estar correto)

## Visão Geral

ADR-001 aceita conscientemente um trade-off: `allotted_units`, `committed_units`, `held_units` e `blocked_units` são valores **derivados persistidos**, e podem divergir da fonte de verdade se alguma escrita escapar do caminho canônico.

A mitigação declarada é dupla: escrita exclusiva pelo `InventoryLedger` e um teste que **reconstrói** os contadores a partir de allotments, bloqueios, retenções e comprometimentos, comparando com o materializado. Este é esse teste.

O segundo risco de ADR-001 é deadlock entre operações que tocam conjuntos de datas parcialmente sobrepostos. A mitigação é a ordem `ORDER BY date` obrigatória, e este teste a exercita sob concorrência real.

## Requisitos

- A reconstrução parte exclusivamente das fontes — `allotments`, `inventory_blocks` e, na Onda B, `inventory_holds` e `inventory_commitments` — e não lê `daily_inventory`.
- A comparação cobre todas as datas de todas as acomodações do cenário, não uma amostra.
- Cenário de reconciliação exercita a sequência real: ceder → alterar → bloquear planejado → bloquear emergencial → remover bloqueio → cancelar allotment.
- Teste de deadlock dispara operações concorrentes sobre conjuntos de datas **parcialmente sobrepostos** da mesma acomodação e verifica que todas concluem.
- Teste de corrida de allotment dispara dois `POST` simultâneos com períodos sobrepostos e verifica que exatamente um vence e o outro recebe `409 ALLOTMENT_PERIOD_OVERLAP`.
- Ambos os testes rodam contra PostgreSQL real via Testcontainers. Não há substituto em memória para essa verificação.
- O arquivo de concorrência é estendido pela Onda B na tarefa 48.0.

## Arquivos Envolvidos

- **Criar:**
  - `../localizestay-backend/tests/LocalizeStay.IntegrationTests/Inventory/InventoryLedgerReconciliationTests.cs`
  - `../localizestay-backend/tests/LocalizeStay.IntegrationTests/Inventory/InventoryConcurrencyTests.cs`
- **Referência:**
  - `.tasks/prd-controlar-allotment-disponibilidade-bloqueios-e-retencoes/adrs/adr-001.md` (riscos e mitigações declarados)
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Domain/DailyInventories/InventoryLedger.cs`
  - `../localizestay-backend/tests/LocalizeStay.IntegrationTests/Infrastructure/LocalizeStayWebApplicationFactory.cs`
- **Skills para consultar durante implementação:**
  - `dotnet-testing` — Testcontainers PostgreSQL, testes concorrentes com `Task.WhenAll`
  - `dotnet-performance` — comportamento real de lock e isolamento `READ COMMITTED`
  - `dotnet-architecture` — o ledger como ponto único de mutação

## Subtarefas

- [ ] 36.1 Implementar o reconstrutor de contadores a partir das fontes, independente de `daily_inventory`.
- [ ] 36.2 Montar o cenário de reconciliação com a sequência completa de mutações e comparar linha a linha.
- [ ] 36.3 Implementar o teste de deadlock com operações concorrentes sobre datas parcialmente sobrepostas.
- [ ] 36.4 Implementar o teste de corrida de allotment sobreposto, verificando um vencedor e um `409`.

## Sequenciamento

- Bloqueado por: 14.0, 18.0, 19.0
- Desbloqueia: 48.0
- Paralelizável: Sim; roda em paralelo às demais tarefas da Fase 6.

## Rastreabilidade

- Esta tarefa cobre: as duas mitigações declaradas em ADR-001 e a seção Testes de Integração da TechSpec.
- Evidência esperada: reconstrução idêntica ao materializado e ausência de deadlock sob concorrência real.

## Detalhes de Implementação

Fórmula da reconstrução, por `(accommodation_id, date)`:

```
allotted  = units do allotment ativo que cobre a data (0 se não houver)
blocked   = soma de units dos bloqueios ativos que cobrem a data
held      = soma de units das retenções com status='held' e expires_at > now()   (Onda B)
committed = soma de units dos comprometimentos que cobrem a data                  (Onda B)
```

Cenário de reconciliação:

```
1. Ceder allotment de 3 unidades, 30 dias
2. Alterar para 2 unidades
3. Aplicar bloqueio planejado de 1 unidade em 5 datas
4. Aplicar bloqueio emergencial em 3 datas
5. Remover o bloqueio planejado
6. Cancelar o allotment
==> reconstruir e comparar todas as 30 datas em cada passo
```

Cenário de deadlock:

```
Operação A: bloqueio nas datas 10..20
Operação B: bloqueio nas datas 15..25
Disparadas simultaneamente com Task.WhenAll
==> ambas concluem; nenhuma falha com deadlock detected
```

> Sem `ORDER BY date`, A adquire lock em 15 esperando 16 enquanto B adquire 16 esperando 15. Com a ordem crescente obrigatória em **todos** os caminhos, o ciclo é impossível por construção. É por isso que ADR-001 chama a ordem de obrigatória e não de recomendada.

**Convenções da stack (das skills consultadas):**

- Testcontainers PostgreSQL — o comportamento de lock não é reproduzível em provider em memória (`dotnet-testing`).
- Concorrência real com `Task.WhenAll`, não simulada com mocks.
- Isolamento `READ COMMITTED`, o padrão; o `FOR UPDATE` é o que garante a serialização.

## Critérios de Sucesso (Verificáveis)

- [ ] Testes passam: `dotnet test ../localizestay-backend/tests/LocalizeStay.IntegrationTests --filter "FullyQualifiedName~InventoryLedgerReconciliationTests"`
- [ ] Testes passam: `dotnet test ../localizestay-backend/tests/LocalizeStay.IntegrationTests --filter "FullyQualifiedName~InventoryConcurrencyTests"`
- [ ] Build compila sem erros: `dotnet build ../localizestay-backend/LocalizeStay.sln --no-restore`
- [ ] A reconstrução bate com o materializado em **todas** as datas, após cada um dos seis passos do cenário.
- [ ] Operações concorrentes sobre datas parcialmente sobrepostas concluem sem deadlock.
- [ ] Dois `POST` simultâneos de allotment sobreposto produzem exatamente um `201` e um `409 ALLOTMENT_PERIOD_OVERLAP`.
- [ ] O reconstrutor não lê `daily_inventory` em nenhum ponto.
