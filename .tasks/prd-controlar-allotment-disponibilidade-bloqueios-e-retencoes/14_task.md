---
status: pending
parallelizable: false
blocked_by: ["10.0", "12.0", "13.0"]
---

<task_context>
<domain>inventory/infra/persistence</domain>
<type>implementation</type>
<scope>configuration</scope>
<complexity>high</complexity>
<dependencies>database</dependencies>
<unblocks>"17.0, 18.0, 19.0, 21.0, 22.0, 23.0, 24.0, 31.0, 40.0"</unblocks>
<vertical_slice>O schema inventory ganha as cinco tabelas da Onda A, com índices e a constraint de não sobreposição, aplicáveis em PostgreSQL real.</vertical_slice>
</task_context>

# Tarefa 14.0: Criar a migration `AddInventoryControl` e os `DbSet` da Onda A

> ⚠️ **`complexity: high` — exige revisão humana do plano antes de implementar.** A migration combina chave primária composta, índice parcial, extensão `btree_gist` e índice de exclusão sobre `daterange`. Fatiá-la deixaria o schema inconsistente entre passos.

## Relacionada às User Stories

- [US-01] Registrar allotment (suporte)
- [US-02] Calendário de inventário (suporte)
- [US-03] Bloquear datas (suporte)
- [US-05] Fila de solicitações (suporte)

## Visão Geral

Uma única migration cria as cinco tabelas da Onda A — `daily_inventory`, `allotments`, `inventory_blocks`, `inventory_requests`, `property_sellability` — mais `inventory_idempotency_keys`, com todos os índices declarados nas tarefas 10.0, 12.0 e 13.0.

O `InventoryDbContext` ganha os `DbSet` correspondentes, e o snapshot do modelo EF é atualizado.

## Requisitos

- Migration gerada por `dotnet ef migrations add AddInventoryControl`, com o SQL bruto da extensão e do índice de exclusão adicionado manualmente ao `Up` e revertido no `Down`.
- `CREATE EXTENSION IF NOT EXISTS btree_gist` precede a criação da constraint de exclusão.
- Seis `DbSet` novos no `InventoryDbContext`: `DailyInventories`, `Allotments`, `InventoryBlocks`, `InventoryRequests`, `PropertySellabilities`, `InventoryIdempotencyKeys`.
- Snapshot `InventoryDbContextModelSnapshot.cs` atualizado pela ferramenta, não à mão.
- A migration é aplicada por `ModuleDatabaseMigrationService`, já existente. Nenhuma infraestrutura nova.
- Teste de persistência em PostgreSQL real via Testcontainers, provando tabelas, índices e a constraint de exclusão.
- Nenhuma tabela da Onda B é criada aqui — retenção e comprometimento entram na migration incremental da tarefa 40.0.

## Arquivos Envolvidos

- **Criar:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Infrastructure/Migrations/[timestamp]_AddInventoryControl.cs`
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Infrastructure/Migrations/[timestamp]_AddInventoryControl.Designer.cs`
  - `../localizestay-backend/tests/LocalizeStay.IntegrationTests/Inventory/InventoryControlPersistenceTests.cs`
- **Modificar:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Infrastructure/InventoryDbContext.cs` (seis `DbSet`)
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Infrastructure/Migrations/InventoryDbContextModelSnapshot.cs`
- **Referência:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Infrastructure/Migrations/20260723015655_AddCommercialOffers.cs` (padrão de migration do módulo)
  - `../localizestay-backend/tests/LocalizeStay.IntegrationTests/Infrastructure/LocalizeStayWebApplicationFactory.cs` (Testcontainers PostgreSQL)
  - `../localizestay-backend/src/BuildingBlocks/LocalizeStay.SharedKernel/DependencyInjection/ModuleDatabaseMigrationService.cs`
- **Skills para consultar durante implementação:**
  - `dotnet-dependency-config` — migrations EF Core, PostgreSQL
  - `dotnet-testing` — Testcontainers PostgreSQL como padrão oficial, `WebApplicationFactory`
  - `dotnet-performance` — verificar que os índices declarados chegaram ao banco

## Subtarefas

- [ ] 14.1 Adicionar os seis `DbSet` ao `InventoryDbContext` e gerar a migration com a ferramenta EF.
- [ ] 14.2 Acrescentar ao `Up` o `CREATE EXTENSION IF NOT EXISTS btree_gist` e a constraint de exclusão sobre `daterange(start_date, end_date, '[]')` filtrada por `status = 'active'`; escrever o `Down` correspondente.
- [ ] 14.3 Conferir que o snapshot foi regenerado pela ferramenta e que `available_units` **não** aparece como coluna.
- [ ] 14.4 Testar em PostgreSQL real: migration aplica e reverte; as seis tabelas e todos os índices existem; dois allotments sobrepostos ativos na mesma acomodação são recusados pelo banco.

## Sequenciamento

- Bloqueado por: 10.0, 12.0, 13.0
- Desbloqueia: 17.0, 18.0, 19.0, 21.0, 22.0, 23.0, 24.0, 31.0, 40.0
- Paralelizável: Não; é o ponto de convergência dos mapeamentos e o único lugar que altera `InventoryDbContext` e o snapshot na Onda A.

## Rastreabilidade

- Esta tarefa cobre: a materialização física de ADR-001 e ADR-002, e o requisito de que a não sobreposição de allotment seja garantida pelo banco.
- Evidência esperada: `InventoryControlPersistenceTests` prova, em PostgreSQL real, que a constraint de exclusão recusa a sobreposição.

## Detalhes de Implementação

SQL bruto a acrescentar ao `Up`:

```csharp
migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS btree_gist;");

migrationBuilder.Sql("""
    ALTER TABLE inventory.allotments
      ADD CONSTRAINT ix_allotments_no_overlap
      EXCLUDE USING gist (
        accommodation_id WITH =,
        daterange(start_date, end_date, '[]') WITH &&
      ) WHERE (status = 'active');
    """);
```

E ao `Down`:

```csharp
migrationBuilder.Sql("ALTER TABLE inventory.allotments DROP CONSTRAINT IF EXISTS ix_allotments_no_overlap;");
```

Cenário de teste que prova a decisão:

```
Dado allotment ativo da acomodação A de 2026-09-01 a 2026-11-29
Quando um segundo allotment ativo da mesma acomodação de 2026-10-01 a 2026-12-31 é inserido
Então o PostgreSQL recusa por violação da constraint de exclusão
E o handler (18.0) traduz a violação em 409 ALLOTMENT_PERIOD_OVERLAP
```

> A tradução da violação de constraint em `409` é da tarefa 18.0. Aqui prova-se apenas que o banco recusa — que é o que garante o resultado sob dois `POST` concorrentes.

Nome do arquivo de teste: `InventoryControlPersistenceTests`, e não `InventoryPersistenceTests`, porque o segundo já existe desde a F01.

**Convenções da stack (das skills consultadas):**

- Migration gerada pela ferramenta; ajustes manuais restritos ao SQL que o EF não modela (`dotnet-dependency-config`).
- Testcontainers PostgreSQL é o padrão oficial para toda verificação que dependa de comportamento real do banco (`dotnet-testing`).
- A migration é aplicada antes de qualquer hosted service iniciar, conforme `ModuleDatabaseMigrationService`.

## Critérios de Sucesso (Verificáveis)

- [ ] Build compila sem erros: `dotnet build ../localizestay-backend/LocalizeStay.sln --no-restore`
- [ ] Formatação válida: `dotnet format ../localizestay-backend/LocalizeStay.sln --verify-no-changes --no-restore`
- [ ] Testes passam: `dotnet test ../localizestay-backend/tests/LocalizeStay.IntegrationTests --filter "FullyQualifiedName~InventoryControlPersistenceTests"`
- [ ] `dotnet ef migrations list` mostra `AddInventoryControl` como a última migration do `InventoryDbContext`.
- [ ] O banco de teste contém as seis tabelas no schema `inventory` e todos os índices declarados em 10.0, 12.0 e 13.0.
- [ ] Inserir dois allotments ativos com período sobreposto na mesma acomodação falha por violação de constraint.
- [ ] `daily_inventory` **não** tem coluna `available_units`.
- [ ] O `Down` reverte a migration sem erro.
