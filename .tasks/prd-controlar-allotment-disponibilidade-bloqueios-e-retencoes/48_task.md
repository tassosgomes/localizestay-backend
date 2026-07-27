---
status: pending
parallelizable: true
blocked_by: ["36.0", "42.0"]
---

<task_context>
<domain>inventory/testing/consistency</domain>
<type>testing</type>
<scope>core_feature</scope>
<complexity>high</complexity>
<dependencies>database</dependencies>
<unblocks>"50.0"</unblocks>
<vertical_slice>Duas jornadas de checkout concorrentes pela última unidade produzem exatamente uma retenção, e nenhuma capacidade é separada na perdedora.</vertical_slice>
</task_context>

# Tarefa 48.0: Certificar a concorrência pela última unidade

> ⚠️ **`complexity: high` — exige revisão humana do plano antes de implementar.** É o teste que prova a promessa central da F03. Um teste de concorrência mal construído passa por acidente de escalonamento e dá falsa confiança.

## Relacionada às User Stories

- [US-04] Acomodação separada durante o checkout, para não perder a unidade durante o pagamento (cobertura direta)

## Visão Geral

O PRD define o critério de saída da Onda B em uma frase: **"sem venda sem lastro em teste de concorrência"**. Este é esse teste.

Duas intenções concorrentes solicitam retenção da última unidade da data. Exatamente uma é criada; a outra é recusada com `422 INSUFFICIENT_AVAILABILITY` e **nenhuma capacidade é separada** nela.

## Requisitos

- Concorrência **real** com `Task.WhenAll` contra PostgreSQL via Testcontainers. Sem mocks, sem simulação de escalonamento.
- Executar o cenário em repetição (pelo menos 20 iterações) para reduzir a chance de passar por acidente.
- Exatamente uma retenção criada; exatamente um `422` com `metadata.unavailableDates`.
- O saldo após o par concorrente é exatamente o esperado — nunca negativo, nunca com unidade "perdida".
- Cenário de estadia multi-noite: duas intenções que competem por noites parcialmente sobrepostas concluem sem deadlock.
- Estender a reconciliação da tarefa 36.0 para incluir retenções e comprometimentos na reconstrução dos contadores.
- Cenário de expiração concorrente: a varredura e uma liberação explícita atuando sobre a mesma retenção devolvem capacidade uma única vez.

## Arquivos Envolvidos

- **Criar:**
  - `../localizestay-backend/tests/LocalizeStay.IntegrationTests/Inventory/InventoryHoldConcurrencyTests.cs`
- **Modificar:**
  - `../localizestay-backend/tests/LocalizeStay.IntegrationTests/Inventory/InventoryLedgerReconciliationTests.cs` (incluir `held` e `committed` na reconstrução)
- **Referência:**
  - `../localizestay-backend/tests/LocalizeStay.IntegrationTests/Inventory/InventoryConcurrencyTests.cs` (criado em 36.0)
  - `.tasks/prd-controlar-allotment-disponibilidade-bloqueios-e-retencoes/adrs/adr-001.md`
  - `.tasks/prd-controlar-allotment-disponibilidade-bloqueios-e-retencoes/api-contract.yaml` (`x-backend-notes` de `createInventoryHold`)
- **Skills para consultar durante implementação:**
  - `dotnet-testing` — Testcontainers, `Task.WhenAll`, repetição para reduzir flakiness
  - `dotnet-performance` — comportamento real de lock e isolamento `READ COMMITTED`
  - `dotnet-architecture` — o ledger como ponto único de mutação

## Subtarefas

- [ ] 48.1 Implementar o cenário de duas intenções concorrentes pela última unidade, repetido pelo menos 20 vezes, verificando um vencedor e um `422`.
- [ ] 48.2 Verificar que a intenção perdedora **não** separou capacidade alguma e que o saldo final é exato.
- [ ] 48.3 Implementar o cenário multi-noite com sobreposição parcial, verificando ausência de deadlock.
- [ ] 48.4 Estender a reconciliação para incluir `held` e `committed`, e testar a corrida entre varredura e liberação explícita.

## Sequenciamento

- Bloqueado por: 36.0, 42.0
- Desbloqueia: 50.0
- Paralelizável: Sim; roda em paralelo às demais tarefas da Fase 9.

## Rastreabilidade

- Esta tarefa cobre: o terceiro critério de aceite de RF-06 ("duas intenções concorrentes para a última unidade: apenas uma é criada e a outra é recusada") e o critério de saída da Onda B do PRD.
- Evidência esperada: `InventoryHoldConcurrencyTests` verde em repetição, e a reconciliação estendida batendo com o materializado.

## Detalhes de Implementação

Cenário canônico:

```
Setup:  allotment de 1 unidade na data D

var a = PostHoldAsync(intent1, D);
var b = PostHoldAsync(intent2, D);
await Task.WhenAll(a, b);

Assert: exatamente um 201 e exatamente um 422 INSUFFICIENT_AVAILABILITY
Assert: daily_inventory[D].held_units == 1   (nunca 2, nunca 0)
Assert: available == 0
```

Cenário multi-noite com sobreposição parcial:

```
Setup:  allotment de 1 unidade nas datas 14..18

Intenção A: checkIn=14, checkOut=17  (noites 14,15,16)
Intenção B: checkIn=16, checkOut=19  (noites 16,17,18)
Disparadas simultaneamente

Assert: exatamente uma vence; nenhuma falha com deadlock detected
Assert: a perdedora não separou capacidade em NENHUMA noite — nem nas que estavam livres
```

> A segunda asserção do cenário multi-noite é a mais importante do plano inteiro. Se a intenção perdedora separasse capacidade nas noites 17 e 18, que estavam livres, essas unidades ficariam presas por quinze minutos sem que checkout algum as estivesse usando. É a diferença entre "todas as noites são verificadas antes de gravar" e "as noites são gravadas uma a uma até falhar".

Repetir 20 vezes não garante ausência de bug de concorrência, mas um teste de uma iteração garante quase nada. A garantia real vem da ordem de lock (ADR-001) e da verificação completa antes da escrita (39.0); o teste é a rede de segurança.

**Convenções da stack (das skills consultadas):**

- Testcontainers PostgreSQL; concorrência real, nunca simulada (`dotnet-testing`).
- Isolamento `READ COMMITTED` com `FOR UPDATE` ordenado por data.
- Reconstrução independente de `daily_inventory` na reconciliação (ADR-001).

## Critérios de Sucesso (Verificáveis)

- [ ] Testes passam: `dotnet test ../localizestay-backend/tests/LocalizeStay.IntegrationTests --filter "FullyQualifiedName~InventoryHoldConcurrencyTests"`
- [ ] Testes passam: `dotnet test ../localizestay-backend/tests/LocalizeStay.IntegrationTests --filter "FullyQualifiedName~InventoryLedgerReconciliationTests"`
- [ ] Build compila sem erros: `dotnet build ../localizestay-backend/LocalizeStay.sln --no-restore`
- [ ] Em 20 iterações, sempre exatamente um `201` e um `422 INSUFFICIENT_AVAILABILITY`.
- [ ] `held_units` da data nunca ultrapassa o allotment.
- [ ] A intenção perdedora não separa capacidade em nenhuma noite, incluindo as que estavam livres.
- [ ] O cenário multi-noite com sobreposição parcial conclui sem deadlock.
- [ ] A reconciliação com `held` e `committed` bate com o materializado em todas as datas.
- [ ] Varredura e liberação explícita concorrentes devolvem capacidade uma única vez.
