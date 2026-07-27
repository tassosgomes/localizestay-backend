---
status: pending
parallelizable: true
blocked_by: ["37.0"]
---

<task_context>
<domain>inventory/domain/inventory-holds</domain>
<type>implementation</type>
<scope>core_feature</scope>
<complexity>medium</complexity>
<dependencies>database</dependencies>
<unblocks>"40.0"</unblocks>
<vertical_slice>O comprometimento vincula retenção e reserva, e ambas as entidades de retenção ganham mapeamento EF com os índices da varredura e da deduplicação.</vertical_slice>
</task_context>

# Tarefa 38.0: Modelar `InventoryCommitment` e mapear a retenção no EF Core

## Relacionada às User Stories

- [US-04] Acomodação separada durante o checkout (suporte — o comprometimento fecha o ciclo)

## Visão Geral

`InventoryCommitment` registra a conversão de retenção em capacidade comprometida, vinculando `reservationId` **sem absorver dado algum do viajante**. Junto dele vêm os dois mapeamentos EF da Onda B.

Os índices são o que torna a Onda B viável: `(status, expires_at) WHERE status = 'held'` sustenta a varredura de expiração sem tocar retenções encerradas, e `(reservation_intent_id)` deduplica a intenção vinda de D03.

## Requisitos

- `InventoryCommitment` com `HoldId`, `ReservationId`, `AccommodationId`, período, unidades e `CommittedAt`.
- Nenhum dado do viajante: apenas identificadores, período e quantidade.
- `inventory_holds`: índice parcial `(status, expires_at) WHERE status = 'held'` e índice `(reservation_intent_id)`.
- `inventory_commitments`: índice por `(accommodation_id, check_in, check_out)` e unicidade por `reservation_id`.
- Estados persistidos como texto legível; `invalidated_by_block_id` nulo exceto na invalidação.
- Instantes em `DateTimeOffset` UTC; datas de estadia em `DateOnly`.
- Todas as tabelas no schema `inventory`; nenhuma FK atravessa módulos — `reservation_id` é apenas um `Guid`, sem FK para D03.
- As configurações são aplicadas por `ApplyConfigurationsFromAssembly`; os `DbSet` entram na tarefa 40.0.

## Arquivos Envolvidos

- **Criar:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Domain/InventoryHolds/InventoryCommitment.cs`
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Infrastructure/Configurations/InventoryHoldConfiguration.cs`
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Infrastructure/Configurations/InventoryCommitmentConfiguration.cs`
- **Referência:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Domain/InventoryHolds/InventoryHold.cs` (criado em 37.0)
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Infrastructure/Configurations/CommercialRateConfiguration.cs` (padrão do módulo)
  - `.tasks/prd-controlar-allotment-disponibilidade-bloqueios-e-retencoes/adrs/adr-004.md` (índice da varredura)
- **Skills para consultar durante implementação:**
  - `dotnet-dependency-config` — `IEntityTypeConfiguration<T>`, índice parcial
  - `dotnet-performance` — índice que sustenta a varredura sem varrer a tabela
  - `dotnet-architecture` — ausência de FK entre módulos

## Subtarefas

- [ ] 38.1 Modelar `InventoryCommitment` com identificadores, período, unidades e instante, sem nenhum dado do viajante.
- [ ] 38.2 Mapear `InventoryHold` com os cinco estados, `invalidated_by_block_id` opcional e o índice parcial da varredura.
- [ ] 38.3 Declarar o índice `(reservation_intent_id)` para deduplicação de intenção.
- [ ] 38.4 Mapear `InventoryCommitment` com unicidade por `reservation_id` e índice por acomodação e período.

## Sequenciamento

- Bloqueado por: 37.0
- Desbloqueia: 40.0
- Paralelizável: Sim; arquivos exclusivos e não toca o `InventoryDbContext`, modificado apenas por 40.0.

## Rastreabilidade

- Esta tarefa cobre: RF-08 no modelo de dados e as notas de implementação de ADR-004.
- Evidência esperada: `InventoryHoldPersistenceTests` (40.0) prova os índices em PostgreSQL real.

## Detalhes de Implementação

Índices exigidos:

| Tabela | Índice | Para quê |
|---|---|---|
| `inventory_holds` | `(status, expires_at) WHERE status = 'held'` | Varredura de expiração sem tocar retenções encerradas |
| `inventory_holds` | `(reservation_intent_id)` | Deduplicação da intenção vinda de D03 |
| `inventory_commitments` | `(reservation_id)` único | Um comprometimento por reserva |
| `inventory_commitments` | `(accommodation_id, check_in, check_out)` | Drill-down da data e reconciliação |

Consulta que o índice parcial precisa cobrir (ADR-004):

```sql
SELECT id FROM inventory.inventory_holds
 WHERE status = 'held' AND expires_at <= now()
 ORDER BY expires_at
 LIMIT 200
 FOR UPDATE SKIP LOCKED
```

> **Por que índice parcial e não índice completo:** retenções encerradas se acumulam indefinidamente — cada checkout do piloto deixa uma. Um índice completo em `(status, expires_at)` cresceria com o histórico inteiro para servir uma varredura que só olha o subconjunto ativo. O filtro `WHERE status = 'held'` mantém o índice do tamanho da concorrência instantânea, não do volume acumulado.

`reservation_id` é um `Guid` sem FK. D03 é outro módulo; **nenhuma FK atravessa fronteira de módulo**.

**Convenções da stack (das skills consultadas):**

- Índice parcial declarado com `HasFilter` (`dotnet-performance`).
- Uma configuração por entidade, em `Infrastructure/Configurations/` (`dotnet-dependency-config`).
- Nenhum dado do viajante em entidade da F03 (`dotnet-production-readiness`).

## Critérios de Sucesso (Verificáveis)

- [ ] Build compila sem erros: `dotnet build ../localizestay-backend/LocalizeStay.sln --no-restore`
- [ ] Formatação válida: `dotnet format ../localizestay-backend/LocalizeStay.sln --verify-no-changes --no-restore`
- [ ] Testes de arquitetura verdes: `dotnet test ../localizestay-backend/tests/LocalizeStay.ArchitectureTests`
- [ ] As três configurações são descobertas por `ApplyConfigurationsFromAssembly`.
- [ ] `inventory_holds` declara índice parcial com `HasFilter` sobre `status = 'held'`.
- [ ] `inventory_commitments` declara índice único em `reservation_id`.
- [ ] `InventoryCommitment` não tem nenhuma propriedade de dado pessoal.
- [ ] Nenhuma configuração declara FK para tabela fora do schema `inventory`.
