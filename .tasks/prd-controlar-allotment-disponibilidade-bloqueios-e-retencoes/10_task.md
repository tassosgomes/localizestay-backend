---
status: pending
parallelizable: true
blocked_by: []
---

<task_context>
<domain>inventory/domain/idempotency</domain>
<type>implementation</type>
<scope>core_feature</scope>
<complexity>low</complexity>
<dependencies>database</dependencies>
<unblocks>"14.0, 19.0, 42.0"</unblocks>
<vertical_slice>A mesma Idempotency-Key com o mesmo payload replica o resultado; com payload diferente, produz 409.</vertical_slice>
</task_context>

# Tarefa 10.0: Modelar a idempotência de escrita do controle de inventário

## Relacionada às User Stories

- [US-03] Bloquear datas imediatamente (suporte — o retry do painel não pode aplicar dois bloqueios)
- [US-04] Manter a acomodação separada durante o checkout (suporte — o retry de D03 não pode criar duas retenções)

## Visão Geral

O contrato exige header `Idempotency-Key` obrigatório em três operações concorrentes ou críticas: `createInventoryBlock`, `createInventoryHold` e `commitInventoryHold`. A mesma chave com corpo diferente produz `409 IDEMPOTENCY_KEY_REUSED`.

O padrão já existe no módulo em `CommercialOfferIdempotencyKey` (F02). Esta tarefa replica a forma sem generalizá-la prematuramente, mantendo escopos próprios do controle de inventário.

## Requisitos

- Entidade com escopo, chave, fingerprint do payload e referência ao recurso produzido.
- Escopos: `inventoryBlockCreation`, `inventoryHoldCreation` e `inventoryHoldCommitment`.
- Unicidade por `(scope, key)`; a corrida entre dois requests simultâneos com a mesma chave vira violação de constraint, traduzida em réplica ou `409`.
- Fingerprint é hash estável do payload normalizado; mesma chave + mesmo fingerprint devolve o recurso original, mesma chave + fingerprint diferente lança `IDEMPOTENCY_KEY_REUSED`.
- Mapeamento EF na mesma tarefa, para que a entidade nasça persistível.

## Arquivos Envolvidos

- **Criar:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Domain/InventoryIdempotencyKey.cs`
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Infrastructure/Configurations/InventoryIdempotencyKeyConfiguration.cs`
  - `../localizestay-backend/tests/LocalizeStay.UnitTests/Inventory/InventoryIdempotencyKeyTests.cs`
- **Referência:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Domain/CommercialOffers/CommercialOfferIdempotencyKey.cs` (padrão a replicar)
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Infrastructure/Configurations/CommercialOfferIdempotencyKeyConfiguration.cs`
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Domain/PropertyOnboardings/IdempotentReplayException.cs`
- **Skills para consultar durante implementação:**
  - `dotnet-architecture` — entidade de suporte, exceção de replay
  - `dotnet-dependency-config` — configuração EF com índice único
  - `dotnet-testing` — AAA para as três combinações de chave × fingerprint

## Subtarefas

- [ ] 10.1 Modelar `InventoryIdempotencyKey` com escopo, chave, fingerprint, recurso produzido e instante de registro.
- [ ] 10.2 Configurar o mapeamento EF na tabela `inventory_idempotency_keys` com índice único em `(scope, key)`.
- [ ] 10.3 Testar: mesma chave + mesmo fingerprint devolve réplica; mesma chave + fingerprint diferente lança `IDEMPOTENCY_KEY_REUSED`; chaves em escopos distintos não colidem.

## Sequenciamento

- Bloqueado por: Nenhum
- Desbloqueia: 14.0, 19.0, 42.0
- Paralelizável: Sim; arquivos exclusivos desta tarefa. A configuração EF criada aqui é aplicada por `ApplyConfigurationsFromAssembly`, e o `DbSet` correspondente entra em 14.0.

## Rastreabilidade

- Esta tarefa cobre: o requisito de `Idempotency-Key` obrigatório nas três operações críticas do contrato e o `code` `IDEMPOTENCY_KEY_REUSED`.
- Evidência esperada: `InventoryIdempotencyKeyTests` prova as três combinações; 19.0 e 42.0 consomem o mecanismo; 14.0 cria a tabela.

## Detalhes de Implementação

Operações protegidas:

| Operação | Escopo | Onda |
|---|---|:--:|
| `createInventoryBlock` | `inventoryBlockCreation` | A |
| `createInventoryHold` | `inventoryHoldCreation` | B |
| `commitInventoryHold` | `inventoryHoldCommitment` | B |

Semântica de resposta:

| Situação | Resultado |
|---|---|
| Chave nova | Executa e registra |
| Chave repetida, fingerprint igual | Replica a resposta original, **sem** reexecutar a mutação |
| Chave repetida, fingerprint diferente | `409 IDEMPOTENCY_KEY_REUSED` |

> A replicação **não pode** reaplicar o efeito no saldo. Um retry de bloqueio que decrementasse a capacidade duas vezes é exatamente a classe de bug que a tarefa 36.0 detecta por reconciliação.

**Convenções da stack (das skills consultadas):**

- Fingerprint por hash estável do payload normalizado, como na F02 (`dotnet-architecture`).
- Índice único declarado na configuração EF, não apenas no código (`dotnet-dependency-config`).
- Nomes em inglês, entidade `internal` no assembly do módulo (`dotnet-code-quality`).

## Critérios de Sucesso (Verificáveis)

- [ ] Testes passam: `dotnet test ../localizestay-backend/LocalizeStay.sln --filter "FullyQualifiedName~InventoryIdempotencyKeyTests"`
- [ ] Build compila sem erros: `dotnet build ../localizestay-backend/LocalizeStay.sln --no-restore`
- [ ] Formatação válida: `dotnet format ../localizestay-backend/LocalizeStay.sln --verify-no-changes --no-restore`
- [ ] A configuração EF declara índice único em `(scope, key)`.
- [ ] Mesma chave com payload diferente produz erro com `code = IDEMPOTENCY_KEY_REUSED`.
- [ ] Chaves iguais em escopos diferentes coexistem sem conflito.
