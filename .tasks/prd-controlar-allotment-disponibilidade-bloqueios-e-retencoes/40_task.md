---
status: pending
parallelizable: false
blocked_by: ["14.0", "38.0"]
---

<task_context>
<domain>inventory/infra/persistence</domain>
<type>implementation</type>
<scope>configuration</scope>
<complexity>high</complexity>
<dependencies>database</dependencies>
<unblocks>"41.0, 42.0"</unblocks>
<vertical_slice>O schema inventory ganha as tabelas de retenção e comprometimento, aplicáveis sobre uma base que já tem dados da Onda A.</vertical_slice>
</task_context>

# Tarefa 40.0: Criar a migration incremental das tabelas de retenção

> ⚠️ **`complexity: high` — exige revisão humana do plano antes de implementar.** Migration incremental sobre base com dados da Onda A já em produção. Um erro aqui não é revertido por `git`.

## Relacionada às User Stories

- [US-04] Acomodação separada durante o checkout (suporte)

## Visão Geral

Migration incremental que cria `inventory_holds` e `inventory_commitments`, com os índices declarados na tarefa 38.0, sobre um schema que já pode ter dados reais da Onda A em produção.

Diferente da tarefa 14.0, aqui não há base limpa. A migration precisa ser aditiva e não pode alterar nenhuma tabela existente.

## Requisitos

- Migration gerada por `dotnet ef migrations add AddInventoryHolds`.
- **Estritamente aditiva**: nenhuma coluna, índice ou constraint das tabelas da Onda A é alterada ou removida.
- Dois `DbSet` novos: `InventoryHolds` e `InventoryCommitments`.
- Snapshot atualizado pela ferramenta, não à mão.
- Índice parcial `(status, expires_at) WHERE status = 'held'` presente no banco.
- Teste de persistência prova as duas tabelas, os índices e a aplicação sobre base com dados da Onda A já materializados.
- `Down` reverte sem tocar nas tabelas da Onda A.
- A migration é aplicada **antes** de o serviço de expiração iniciar (tarefa 41.0).

## Arquivos Envolvidos

- **Criar:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Infrastructure/Migrations/[timestamp]_AddInventoryHolds.cs`
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Infrastructure/Migrations/[timestamp]_AddInventoryHolds.Designer.cs`
  - `../localizestay-backend/tests/LocalizeStay.IntegrationTests/Inventory/InventoryHoldPersistenceTests.cs`
- **Modificar:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Infrastructure/InventoryDbContext.cs` (dois `DbSet`)
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Infrastructure/Migrations/InventoryDbContextModelSnapshot.cs`
- **Referência:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Infrastructure/Migrations/[timestamp]_AddInventoryControl.cs` (criada em 14.0)
  - `../localizestay-backend/src/BuildingBlocks/LocalizeStay.SharedKernel/DependencyInjection/ModuleDatabaseMigrationService.cs`
- **Skills para consultar durante implementação:**
  - `dotnet-dependency-config` — migrations incrementais, PostgreSQL
  - `dotnet-testing` — Testcontainers com seed da Onda A antes de aplicar
  - `dotnet-performance` — verificar que o índice parcial chegou ao banco

## Subtarefas

- [ ] 40.1 Adicionar os dois `DbSet` ao `InventoryDbContext` e gerar a migration incremental.
- [ ] 40.2 Revisar o `Up` gerado, garantindo que é estritamente aditivo — nenhuma alteração nas tabelas da Onda A.
- [ ] 40.3 Conferir que o snapshot foi regenerado pela ferramenta e que o índice parcial aparece no SQL emitido.
- [ ] 40.4 Testar: aplicar sobre base com dados da Onda A; as duas tabelas e os índices existem; `Down` reverte sem tocar na Onda A.

## Sequenciamento

- Bloqueado por: 14.0, 38.0
- Desbloqueia: 41.0, 42.0
- Paralelizável: Não; é o único lugar da Onda B que altera `InventoryDbContext` e o snapshot.

## Rastreabilidade

- Esta tarefa cobre: o passo 14 do Build Order da TechSpec e as notas de persistência de ADR-004.
- Evidência esperada: `InventoryHoldPersistenceTests` prova a aplicação incremental sobre base populada.

## Detalhes de Implementação

Cenário de teste que diferencia esta migration da anterior:

```
1. Aplicar AddInventoryControl
2. Popular: 2 propriedades, 4 acomodações, allotment de 90 dias, 3 bloqueios
3. Aplicar AddInventoryHolds
4. Verificar que os dados do passo 2 permanecem íntegros
5. Verificar que inventory_holds e inventory_commitments existem com seus índices
6. Reverter e verificar que os dados do passo 2 continuam íntegros
```

> **Por que testar a reversão com dados:** a Onda A pode estar em produção quando a Onda B for aplicada. Uma migration cujo `Down` toque nas tabelas da Onda A transformaria um rollback de deploy em perda de inventário — e inventário perdido é reserva não reconhecida, que é exatamente o que a F03 existe para evitar.

Índices que o teste precisa encontrar no banco:

```
inventory_holds:        (status, expires_at) WHERE status = 'held'
inventory_holds:        (reservation_intent_id)
inventory_commitments:  (reservation_id) UNIQUE
inventory_commitments:  (accommodation_id, check_in, check_out)
```

**Convenções da stack (das skills consultadas):**

- Migration gerada pela ferramenta; revisão manual do `Up`/`Down` (`dotnet-dependency-config`).
- Testcontainers PostgreSQL, com seed antes de aplicar a migration incremental (`dotnet-testing`).
- A migration é aplicada por `ModuleDatabaseMigrationService` antes de qualquer hosted service iniciar.

## Critérios de Sucesso (Verificáveis)

- [ ] Build compila sem erros: `dotnet build ../localizestay-backend/LocalizeStay.sln --no-restore`
- [ ] Formatação válida: `dotnet format ../localizestay-backend/LocalizeStay.sln --verify-no-changes --no-restore`
- [ ] Testes passam: `dotnet test ../localizestay-backend/tests/LocalizeStay.IntegrationTests --filter "FullyQualifiedName~InventoryHoldPersistenceTests"`
- [ ] `dotnet ef migrations list` mostra `AddInventoryHolds` após `AddInventoryControl`.
- [ ] O `Up` não contém `AlterColumn`, `DropIndex` nem `DropColumn` sobre tabelas da Onda A.
- [ ] Os quatro índices da Onda B existem no banco de teste.
- [ ] Aplicar sobre base populada preserva integralmente os dados da Onda A.
- [ ] `Down` reverte sem alterar nenhuma tabela da Onda A.
- [ ] `InventoryControlPersistenceTests` (14.0) segue verde.
