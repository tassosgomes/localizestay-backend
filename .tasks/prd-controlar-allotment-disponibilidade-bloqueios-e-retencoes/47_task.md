---
status: pending
parallelizable: true
blocked_by: ["41.0", "42.0", "43.0"]
---

<task_context>
<domain>inventory/testing/holds</domain>
<type>testing</type>
<scope>core_feature</scope>
<complexity>medium</complexity>
<dependencies>database</dependencies>
<unblocks>"50.0"</unblocks>
<vertical_slice>O ciclo completo da retenção — criar, expirar, liberar, comprometer e revalidar — é provado de ponta a ponta contra PostgreSQL real.</vertical_slice>
</task_context>

# Tarefa 47.0: Certificar o ciclo de vida completo da retenção

## Relacionada às User Stories

- [US-04] Acomodação separada durante o checkout (cobertura direta)

## Visão Geral

Teste de integração que percorre os cinco desfechos possíveis de uma retenção, contra PostgreSQL real via Testcontainers, exercitando a interação entre a varredura de expiração, a guarda de leitura e os handlers.

É o critério de saída da Onda B declarado no PRD: "criação, expiração, liberação e comprometimento testados de ponta a ponta".

## Requisitos

- Os cinco desfechos exercitados: `held` → `expired`, `released`, `committed`, `invalidated`, e o replay idempotente.
- A expiração é provada pela **varredura real**, não por manipulação direta de estado.
- A guarda de leitura é provada: retenção vencida não reduz saldo **antes** de a varredura passar.
- Comprometimento de retenção expirada com e sem saldo.
- Invalidação por bloqueio emergencial, com `invalidatedByBlockId` preenchido.
- Liberação idempotente: a mesma retenção liberada duas vezes devolve capacidade uma única vez.
- Os três eventos da Onda B verificados na outbox, com nome, versão e payload conformes ao contrato.
- Saldo verificado após cada transição, comparando com o esperado.

## Arquivos Envolvidos

- **Criar:**
  - `../localizestay-backend/tests/LocalizeStay.IntegrationTests/Inventory/InventoryHoldLifecycleTests.cs`
- **Referência:**
  - `../localizestay-backend/tests/LocalizeStay.IntegrationTests/Infrastructure/LocalizeStayWebApplicationFactory.cs`
  - `.tasks/prd-controlar-allotment-disponibilidade-bloqueios-e-retencoes/adrs/adr-004.md`
  - `.tasks/prd-controlar-allotment-disponibilidade-bloqueios-e-retencoes/prd.md` (critério para avançar ao piloto)
- **Skills para consultar durante implementação:**
  - `dotnet-testing` — Testcontainers PostgreSQL, controle de tempo em teste de integração
  - `dotnet-observability` — verificar os eventos na outbox
  - `dotnet-architecture` — hosted service exercitado de verdade, não mockado

## Subtarefas

- [ ] 47.1 Exercitar criação e expiração pela varredura real, verificando os dois eventos e a devolução de capacidade.
- [ ] 47.2 Exercitar a guarda de leitura: retenção vencida não reduz saldo antes de a varredura passar.
- [ ] 47.3 Exercitar liberação (idempotente duas vezes), invalidação por bloqueio emergencial e comprometimento de retenção vigente.
- [ ] 47.4 Exercitar comprometimento de retenção expirada com saldo (`revalidated: true`) e sem saldo (`422`), verificando o saldo após cada transição.

## Sequenciamento

- Bloqueado por: 41.0, 42.0, 43.0
- Desbloqueia: 50.0
- Paralelizável: Sim; roda em paralelo às demais tarefas da Fase 9.

## Rastreabilidade

- Esta tarefa cobre: o critério de saída da Onda B declarado no PRD e a seção Testes de Integração da TechSpec.
- Evidência esperada: `InventoryHoldLifecycleTests` verde, com a varredura exercitada de verdade.

## Detalhes de Implementação

Cenário completo:

```
Setup:  allotment de 2 unidades numa data

1. POST /inventory-holds (1 unidade)      ==> 201, available=1, evento inventario-retido
2. Avançar o relógio além de expiresAt    ==> available=2 IMEDIATAMENTE (guarda de leitura)
3. Rodar um ciclo da varredura            ==> status=expired, eventos retencao-expirada + inventario-liberado
4. POST /inventory-holds                  ==> 201
5. DELETE /inventory-holds/{id}           ==> 204, available volta, evento inventario-liberado
6. DELETE /inventory-holds/{id} de novo   ==> 204, available NÃO muda, NENHUM evento novo
7. POST /inventory-holds                  ==> 201
8. POST .../commitment                    ==> 201, revalidated=false, available INALTERADO
9. POST /inventory-holds                  ==> 201
10. Bloqueio emergencial na data          ==> retenção invalidated, invalidatedByBlockId preenchido
```

> O passo 2 é o que prova ADR-004. Se `available` só voltasse a 2 depois do passo 3, o intervalo entre `expiresAt` e a varredura seria latência real de disponibilidade — venda perdida no ponto mais sensível do funil.
>
> O passo 6 é o que prova a idempotência. Se `available` mudasse ali, o saldo cresceria além do allotment.

O controle de tempo precisa usar o `IClock` substituível já registrado no `WebApplicationFactory`, não `Thread.Sleep` de quinze minutos.

**Convenções da stack (das skills consultadas):**

- Testcontainers PostgreSQL — a interação varredura × guarda não é reproduzível em memória (`dotnet-testing`).
- O `IHostedService` é exercitado de verdade, com um ciclo disparado explicitamente.
- Eventos verificados na outbox contra os schemas normativos do contrato (`restful-api`).

## Critérios de Sucesso (Verificáveis)

- [ ] Testes passam: `dotnet test ../localizestay-backend/tests/LocalizeStay.IntegrationTests --filter "FullyQualifiedName~InventoryHoldLifecycleTests"`
- [ ] Build compila sem erros: `dotnet build ../localizestay-backend/LocalizeStay.sln --no-restore`
- [ ] Os dez passos do cenário produzem exatamente o saldo esperado em cada ponto.
- [ ] `available` volta ao valor cheio **no passo 2**, antes da varredura.
- [ ] A varredura publica `retencao-expirada` e `inventario-liberado` exatamente uma vez.
- [ ] A segunda liberação (passo 6) não altera o saldo nem produz evento.
- [ ] Comprometer retenção vigente mantém `available` inalterado.
- [ ] Comprometer retenção expirada sem saldo responde 422 `COMMITMENT_WITHOUT_AVAILABILITY`.
- [ ] Retenção invalidada por bloqueio emergencial tem `invalidatedByBlockId` preenchido e status `invalidated`, nunca `expired`.
