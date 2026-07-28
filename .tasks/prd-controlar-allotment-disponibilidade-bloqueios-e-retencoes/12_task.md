---
status: pending
parallelizable: true
blocked_by: ["5.0", "6.0", "7.0"]
---

<task_context>
<domain>inventory/infra/persistence</domain>
<type>implementation</type>
<scope>configuration</scope>
<complexity>medium</complexity>
<dependencies>database</dependencies>
<unblocks>"14.0"</unblocks>
<vertical_slice>Saldo, allotment e bloqueio têm mapeamento EF com chaves, índices e a constraint de não sobreposição de período.</vertical_slice>
</task_context>

# Tarefa 12.0: Mapear saldo, allotment e bloqueio no EF Core

## Relacionada às User Stories

- [US-01] Registrar allotment (suporte)
- [US-02] Diagnosticar a data no calendário (suporte — os índices sustentam a leitura da grade)
- [US-03] Bloquear datas (suporte)

## Visão Geral

Três mapeamentos EF que traduzem as entidades da Fase 2 em tabelas do schema `inventory`, com os índices que sustentam o caminho quente da F03: a checagem "todas as noites têm saldo", o calendário de até 92 dias e a consulta pública de disponibilidade.

O ponto mais delicado é o **índice de exclusão PostgreSQL** que impede sobreposição de allotment na mesma acomodação. Dois `POST` concorrentes precisam produzir `409`, não dois allotments sobrepostos — por isso a garantia é do banco, não do handler.

## Requisitos

- `daily_inventory`: chave primária composta `(accommodation_id, date)`; `available_units` **não é coluna**; índice adicional `(date) WHERE allotted_units > 0` para a métrica de cobertura.
- `allotments`: `Revision` como concurrency token do EF Core; índice `(accommodation_id, start_date, end_date)`; **índice de exclusão** sobre `daterange(start_date, end_date, '[]')` filtrado por `status = 'active'`.
- `inventory_blocks`: índice `(accommodation_id, start_date, end_date, status)`; colunas de remoção preservadas (`removed_at`, `removal_reason`, `removed_by`).
- Períodos em `DateOnly` inclusivo nas duas pontas; instantes de auditoria em `DateTimeOffset` UTC.
- Todas as tabelas no schema `inventory`. **Nenhuma FK atravessa módulos**: `accommodation_id` referencia `inventory.accommodations`, do mesmo schema.
- As configurações são aplicadas por `ApplyConfigurationsFromAssembly`, já presente no `InventoryDbContext`; os `DbSet` entram na tarefa 14.0.

## Arquivos Envolvidos

- **Criar:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Infrastructure/Configurations/DailyInventoryConfiguration.cs`
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Infrastructure/Configurations/AllotmentConfiguration.cs`
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Infrastructure/Configurations/InventoryBlockConfiguration.cs`
- **Referência:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Infrastructure/Configurations/CommercialOfferConfiguration.cs` (padrão de mapeamento do módulo)
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Infrastructure/InventoryDbContext.cs`
  - `.tasks/prd-controlar-allotment-disponibilidade-bloqueios-e-retencoes/adrs/adr-001.md` (índices exigidos)
- **Skills para consultar durante implementação:**
  - `dotnet-dependency-config` — `IEntityTypeConfiguration<T>`, PostgreSQL, concurrency token
  - `dotnet-performance` — índices que cobrem o caminho quente, ausência de coluna derivada
  - `dotnet-architecture` — schema por módulo, ausência de FK entre módulos

## Subtarefas

- [ ] 12.1 Mapear `DailyInventory` com chave composta, colunas dos quatro contadores, `allotment_id` opcional e o índice parcial de cobertura.
- [ ] 12.2 Mapear `Allotment` com `Revision` como concurrency token, enums convertidos para texto e os índices de período.
- [ ] 12.3 Declarar o índice de exclusão de sobreposição sobre `daterange(start_date, end_date, '[]')` filtrado por `status = 'active'`, com `btree_gist` habilitado.
- [ ] 12.4 Mapear `InventoryBlock` com enums, período, colunas de remoção e o índice de consulta por acomodação e período.

## Sequenciamento

- Bloqueado por: 5.0, 6.0, 7.0
- Desbloqueia: 14.0
- Paralelizável: Sim; cria três arquivos exclusivos e **não** toca o `InventoryDbContext`, que é modificado apenas pela tarefa 14.0.

## Rastreabilidade

- Esta tarefa cobre: as decisões de persistência de ADR-001 e o requisito de que a não sobreposição seja garantida pelo banco.
- Evidência esperada: a tarefa 14.0 gera a migration a partir destes mapeamentos e o `InventoryControlPersistenceTests` prova a constraint em PostgreSQL real.

## Detalhes de Implementação

Índices exigidos por ADR-001 e pela TechSpec:

| Tabela | Índice | Para quê |
|---|---|---|
| `daily_inventory` | PK `(accommodation_id, date)` | Estadia, calendário e consulta pública |
| `daily_inventory` | `(date) WHERE allotted_units > 0` | Métrica de cobertura sem varrer a tabela |
| `allotments` | `(accommodation_id, start_date, end_date)` | Busca de allotment vigente por data |
| `allotments` | **Exclusão** sobre `daterange(...)` `WHERE status = 'active'` | `409 ALLOTMENT_PERIOD_OVERLAP` sob concorrência |
| `inventory_blocks` | `(accommodation_id, start_date, end_date, status)` | Bloqueios ativos de uma data |

O índice de exclusão exige a extensão `btree_gist`, porque combina igualdade em `accommodation_id` com sobreposição de `daterange`:

```sql
CREATE EXTENSION IF NOT EXISTS btree_gist;

ALTER TABLE inventory.allotments
  ADD CONSTRAINT ix_allotments_no_overlap
  EXCLUDE USING gist (
    accommodation_id WITH =,
    daterange(start_date, end_date, '[]') WITH &&
  ) WHERE (status = 'active');
```

> O EF Core não gera esse tipo de constraint a partir do modelo. Ela é declarada aqui como intenção e materializada por SQL bruto na migration da tarefa 14.0 — e é lá que o `CREATE EXTENSION` precisa aparecer.

**Convenções da stack (das skills consultadas):**

- Uma classe `IEntityTypeConfiguration<T>` por entidade, em `Infrastructure/Configurations/` (`dotnet-dependency-config`).
- Enums persistidos como texto legível, seguindo o padrão do módulo.
- `available_units` **jamais** vira coluna — é derivado na leitura (`dotnet-performance`).
- Nomes de tabela e coluna em snake_case; nomes de classe e propriedade em PascalCase (`dotnet-code-quality`).

## Critérios de Sucesso (Verificáveis)

- [ ] Build compila sem erros: `dotnet build ../localizestay-backend/LocalizeStay.sln --no-restore`
- [ ] Formatação válida: `dotnet format ../localizestay-backend/LocalizeStay.sln --verify-no-changes --no-restore`
- [ ] Testes de arquitetura verdes: `dotnet test ../localizestay-backend/tests/LocalizeStay.ArchitectureTests`
- [ ] As três configurações são descobertas por `ApplyConfigurationsFromAssembly` sem `DbSet` explícito.
- [ ] `DailyInventory` não tem propriedade mapeada chamada `AvailableUnits`.
- [ ] `Allotment.Revision` está declarado com `IsConcurrencyToken()`.
- [ ] Nenhuma configuração declara FK para tabela fora do schema `inventory`.
