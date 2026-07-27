---
status: pending
parallelizable: true
blocked_by: []
---

<task_context>
<domain>inventory/domain/sellability</domain>
<type>implementation</type>
<scope>core_feature</scope>
<complexity>medium</complexity>
<dependencies>database</dependencies>
<unblocks>"13.0, 17.0, 23.0"</unblocks>
<vertical_slice>Uma propriedade sabe o estado corrente dos cinco gates de RN-07 e se, no conjunto, é vendável.</vertical_slice>
</task_context>

# Tarefa 8.0: Modelar `PropertySellability` e os cinco gates de RN-07

## Relacionada às User Stories

- [US-02] Diagnosticar uma data sem alternar telas (direta — o diagnóstico de vendabilidade explica por que não vende mesmo com saldo)
- [US-03] Não vender o que o parceiro não pode honrar (suporte)

## Visão Geral

RN-07 estabelece que somente oferta com propriedade aprovada, conteúdo aprovado, tarifa válida, canal testado e allotment vigente pode ser vendida. ADR-002 decide espelhar esses gates em projeção local, para que `GET /availability` — público e no caminho quente de D01 — resolva os cinco com **uma leitura indexada**, sem chamada síncrona entre módulos.

Esta tarefa modela apenas o estado. A alimentação dos gates é das tarefas 17.0 (D02), 25.0 e 26.0 (D06).

## Requisitos

- Uma linha por propriedade, com os cinco gates nomeados: `propertyApproved`, `contentApproved`, `validRate`, `testedChannel` e `activeAllotment`.
- Cada gate carrega `Code`, `Status` (`satisfied` ou `blocked`), `Detail` e `OwnerDomain` (`D06` para os dois primeiros, `D02` para os três últimos).
- `Sellable` é **derivado**: verdadeiro somente quando todos os cinco gates estão `satisfied`.
- `SuspendedByCuration` deriva do gate `propertyApproved` combinado com a existência de bloqueio ativo de origem `curationSuspension`.
- `EvaluatedAt` registra o instante da última avaliação.
- Cada gate registra a **origem** do valor (evento de D06 ou configuração), para que a resposta de `sellability` e o runbook nunca façam parecer que configuração é decisão de D06.
- Default de gate desconhecido é **`blocked`**. Ausência nunca significa aprovação.

## Arquivos Envolvidos

- **Criar:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Domain/Sellability/PropertySellability.cs`
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Domain/Sellability/SellabilityGate.cs`
  - `../localizestay-backend/tests/LocalizeStay.UnitTests/Inventory/PropertySellabilityTests.cs`
- **Referência:**
  - `.tasks/prd-controlar-allotment-disponibilidade-bloqueios-e-retencoes/adrs/adr-002.md` (tabela gate → owner → fonte de escrita)
  - `.tasks/prd-controlar-allotment-disponibilidade-bloqueios-e-retencoes/api-contract.md` (exemplo de resposta de `getPropertySellability`)
- **Skills para consultar durante implementação:**
  - `dotnet-architecture` — entidade de projeção, value object para o gate
  - `dotnet-code-quality` — value object imutável, enums em PascalCase
  - `dotnet-testing` — `[Theory]` para as 32 combinações relevantes de gates

## Subtarefas

- [ ] 8.1 Modelar `SellabilityGate` como value object imutável com `Code`, `Status`, `Detail`, `OwnerDomain` e origem do valor.
- [ ] 8.2 Modelar `PropertySellability` com os cinco gates, `Sellable` derivado e `EvaluatedAt`.
- [ ] 8.3 Implementar as mutações por gate (`ApplyGate`), que atualizam um gate e reavaliam `Sellable` e `EvaluatedAt` na mesma operação.
- [ ] 8.4 Testar: falha de qualquer um dos cinco torna a propriedade não vendável; default `blocked`; `suspendedByCuration` derivado corretamente.

## Sequenciamento

- Bloqueado por: Nenhum
- Desbloqueia: 13.0, 17.0, 23.0
- Paralelizável: Sim; domínio puro, arquivos exclusivos desta tarefa.

## Rastreabilidade

- Esta tarefa cobre: RN-07 no domínio e o contrato de `getPropertySellability`.
- Evidência esperada: `PropertySellabilityTests` prova que a falha de cada gate individualmente derruba `Sellable`.

## Detalhes de Implementação

Mapa de gates, conforme ADR-002:

| Gate | `ownerDomain` | Como é atualizado | Task |
|---|---|---|---|
| `propertyApproved` | D06 | Consumidor de `propriedade-aprovada` / `propriedade-suspensa` | 25.0, 26.0 |
| `contentApproved` | D06 | Consumidor de `conteudo-aprovado` | 26.0 |
| `validRate` | D02 | Recalculado ao mutar `inventory.commercial_rates` | 17.0 |
| `testedChannel` | D02 | Recalculado a partir do canal operacional registrado pela F01 | 17.0 |
| `activeAllotment` | D02 | Recalculado a cada mutação de allotment | 17.0, 18.0 |

Resposta-alvo do contrato:

```json
{
  "sellable": false,
  "suspendedByCuration": false,
  "gates": [
    { "code": "propertyApproved", "status": "satisfied", "detail": null, "ownerDomain": "D06" },
    { "code": "activeAllotment", "status": "blocked", "detail": "Nenhum allotment vigente nos próximos 90 dias.", "ownerDomain": "D02" }
  ],
  "evaluatedAt": "2026-07-26T13:40:00Z"
}
```

> `ownerDomain` não é texto decorativo: reflete a origem real de cada valor. Enquanto D06 não publicar, os dois gates de curadoria vêm de allowlist configurada e isso **precisa** ficar visível.

**Convenções da stack (das skills consultadas):**

- Value object imutável para o gate; entidade de projeção com mutação controlada (`dotnet-architecture`).
- Negação por padrão como guardrail explícito de segurança (`dotnet-production-readiness`).
- Testes AAA parametrizados cobrindo cada gate isoladamente (`dotnet-testing`).

## Critérios de Sucesso (Verificáveis)

- [ ] Testes passam: `dotnet test ../localizestay-backend/LocalizeStay.sln --filter "FullyQualifiedName~PropertySellabilityTests"`
- [ ] Build compila sem erros: `dotnet build ../localizestay-backend/LocalizeStay.sln --no-restore`
- [ ] Formatação válida: `dotnet format ../localizestay-backend/LocalizeStay.sln --verify-no-changes --no-restore`
- [ ] Propriedade recém-criada tem os cinco gates em `blocked` e `Sellable = false`.
- [ ] Bloquear qualquer um dos cinco gates torna `Sellable = false`, independentemente dos outros quatro.
- [ ] `Sellable = true` exige exatamente os cinco gates `satisfied`.
- [ ] `ApplyGate` atualiza `EvaluatedAt` a cada chamada.
