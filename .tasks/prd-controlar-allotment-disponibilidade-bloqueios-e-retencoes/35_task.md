---
status: pending
parallelizable: true
blocked_by: ["18.0", "19.0", "20.0", "25.0", "26.0"]
---

<task_context>
<domain>inventory/testing/outbox-audit</domain>
<type>testing</type>
<scope>core_feature</scope>
<complexity>medium</complexity>
<dependencies>database</dependencies>
<unblocks>"50.0"</unblocks>
<vertical_slice>Cada mutação de capacidade grava saldo, auditoria e evento na mesma transação — ou não grava nada.</vertical_slice>
</task_context>

# Tarefa 35.0: Certificar outbox, auditoria e atomicidade da mutação de saldo

## Relacionada às User Stories

- [US-01], [US-03] (suporte — trilha de auditoria em toda alteração de capacidade é requisito do PRD)
- [US-06] (suporte — os eventos alimentam D07 e D09)

## Visão Geral

O PRD exige trilha de auditoria com autor, horário e motivo em **toda** alteração de capacidade. A TechSpec exige que saldo, auditoria e outbox compartilhem uma única transação.

Este teste prova as duas coisas ao mesmo tempo, e prova o inverso: quando a transação falha, nada é gravado — nem saldo, nem auditoria, nem evento.

## Requisitos

- Para cada mutação da Onda A — ceder, alterar, cancelar allotment; aplicar e remover bloqueio; suspender e reaprovar por curadoria — verificar que as três escritas acontecem na mesma `SaveChangesAsync`.
- Falha simulada após a mutação de saldo não deixa evento órfão na outbox nem entrada de auditoria sem contrapartida.
- Os três eventos da Onda A aparecem na outbox com o nome, a versão e o payload do contrato: `inventario-bloqueado`, `inventario-liberado` e `bloqueio-afeta-reserva`.
- A trilha de auditoria registra autor, horário e motivo, e é **distinta** dos logs de diagnóstico.
- Suspensão de curadoria grava uma transação **por acomodação**: falhar a segunda não desfaz a primeira.
- Replay idempotente de bloqueio não grava evento nem auditoria uma segunda vez.

## Arquivos Envolvidos

- **Criar:**
  - `../localizestay-backend/tests/LocalizeStay.IntegrationTests/Inventory/InventoryOutboxAndAuditTests.cs`
- **Referência:**
  - `../localizestay-backend/tests/LocalizeStay.IntegrationTests/Inventory/CommercialOfferOutboxAndAuditTests.cs` (padrão da F02)
  - `../localizestay-backend/src/BuildingBlocks/LocalizeStay.SharedKernel/Outbox/OutboxMessageFactory.cs`
  - `../localizestay-backend/src/BuildingBlocks/LocalizeStay.SharedKernel/Auditing/BusinessAuditWriter.cs`
  - `.tasks/prd-controlar-allotment-disponibilidade-bloqueios-e-retencoes/api-contract.yaml` (`x-domain-events` — schemas normativos)
- **Skills para consultar durante implementação:**
  - `dotnet-testing` — Testcontainers PostgreSQL, verificação transacional
  - `dotnet-observability` — auditoria de negócio distinta de log de diagnóstico
  - `dotnet-architecture` — outbox transacional in-process

## Subtarefas

- [ ] 35.1 Para cada mutação da Onda A, verificar que saldo, auditoria e outbox são gravados na mesma transação.
- [ ] 35.2 Simular falha após a mutação de saldo e verificar que nada foi persistido — nem linha, nem auditoria, nem evento.
- [ ] 35.3 Verificar que os três eventos da Onda A têm nome, versão e payload conformes ao `x-domain-events` do contrato.
- [ ] 35.4 Verificar a granularidade transacional da suspensão de curadoria: falha na segunda acomodação preserva a primeira.

## Sequenciamento

- Bloqueado por: 18.0, 19.0, 20.0, 25.0, 26.0
- Desbloqueia: 50.0
- Paralelizável: Sim; roda em paralelo às demais tarefas da Fase 6.

## Rastreabilidade

- Esta tarefa cobre: o requisito de trilha de auditoria do PRD, a atomicidade exigida pela TechSpec e os três eventos produzidos na Onda A.
- Evidência esperada: `InventoryOutboxAndAuditTests` verde, incluindo os cenários de falha.

## Detalhes de Implementação

Matriz de mutação × escritas esperadas:

| Mutação | Saldo | Auditoria | Eventos |
|---|:--:|:--:|---|
| Ceder allotment | ✅ | ✅ | — |
| Alterar allotment | ✅ | ✅ | — |
| Cancelar allotment | ✅ | ✅ | — |
| Bloqueio `planned` | ✅ | ✅ | `inventario-bloqueado` |
| Bloqueio `emergency` | ✅ | ✅ | `inventario-bloqueado` (+ `inventario-liberado` e `bloqueio-afeta-reserva` quando aplicável) |
| Remover bloqueio | ✅ | ✅ | `inventario-liberado` |
| Suspensão de curadoria | ✅ por acomodação | ✅ | `inventario-bloqueado` por acomodação |
| Aprovação de curadoria | ✅ por acomodação | ✅ | `inventario-liberado` por acomodação |

> **O cenário de falha é o que prova a decisão.** Se `inventario-bloqueado` for gravado e o saldo não, D01 remove a oferta de uma data que continua vendável. Se o saldo for gravado e o evento não, D01 continua ofertando uma data já bloqueada — e a promessa do PRD de cortar vendas em um minuto vira ficção.

Verificação da granularidade transacional da suspensão:

```
Dado uma propriedade com 3 acomodações
Quando a suspensão falha ao processar a segunda
Então a primeira permanece bloqueada com seu evento na outbox
E a terceira não foi processada
E o reprocessamento do mesmo eventId conclui as pendentes sem duplicar a primeira
```

**Convenções da stack (das skills consultadas):**

- Outbox transacional in-process, gravada na mesma `SaveChangesAsync` (ADR-0002).
- `BusinessAuditWriter<InventoryDbContext>` rastreia entradas no próprio DbContext sem commitar (`dotnet-observability`).
- Testcontainers PostgreSQL para exercitar transação real (`dotnet-testing`).
- Schemas de payload de evento são **normativos** — divergir do contrato falha o teste (`restful-api`).

## Critérios de Sucesso (Verificáveis)

- [ ] Testes passam: `dotnet test ../localizestay-backend/tests/LocalizeStay.IntegrationTests --filter "FullyQualifiedName~InventoryOutboxAndAuditTests"`
- [ ] Build compila sem erros: `dotnet build ../localizestay-backend/LocalizeStay.sln --no-restore`
- [ ] Toda mutação da matriz grava saldo, auditoria e evento na mesma transação.
- [ ] Falha após a mutação de saldo não deixa evento nem auditoria persistidos.
- [ ] Os três eventos da Onda A batem com o `x-domain-events` do contrato em nome, versão e payload.
- [ ] A auditoria registra autor, horário e motivo de cada alteração de capacidade.
- [ ] Suspensão que falha na segunda acomodação preserva a primeira já aplicada.
- [ ] Replay idempotente de bloqueio não grava evento nem auditoria adicional.
