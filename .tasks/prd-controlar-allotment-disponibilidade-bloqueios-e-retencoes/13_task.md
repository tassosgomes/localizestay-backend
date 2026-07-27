---
status: pending
parallelizable: true
blocked_by: ["8.0", "9.0"]
---

<task_context>
<domain>inventory/infra/persistence</domain>
<type>implementation</type>
<scope>configuration</scope>
<complexity>medium</complexity>
<dependencies>database</dependencies>
<unblocks>"14.0"</unblocks>
<vertical_slice>Fila de solicitações e projeção de vendabilidade têm mapeamento EF com os índices que sustentam a ordenação da fila e a consulta pública.</vertical_slice>
</task_context>

# Tarefa 13.0: Mapear solicitação e vendabilidade no EF Core

## Relacionada às User Stories

- [US-05] Parceiro solicita pelos canais que já usa (suporte — o índice sustenta a ordenação da fila)
- [US-02] Diagnosticar sem alternar telas (suporte)

## Visão Geral

Dois mapeamentos EF de natureza diferente: `inventory_requests` é a fila operacional com SLA, e `property_sellability` é a projeção local dos cinco gates de RN-07, lida por `GET /availability` no caminho quente de D01.

O índice de `inventory_requests` é o que garante que o aviso emergencial de madrugada apareça no topo da fila na abertura da janela. O índice parcial de `property_sellability` é o que permite avaliar RN-07 com uma leitura indexada.

## Requisitos

- `inventory_requests`: índice `(status, priority, received_at)` para a ordenação `priorityThenReceivedAt` e o cálculo de `overdue`.
- Enums (`channel`, `request_type`, `priority`, `status`) persistidos como texto legível.
- Campos derivados de SLA (`received_outside_window`, `sla_starts_at`, `sla_due_at`, `processed_within_sla`) persistidos, porque são apurados no registro e não podem mudar retroativamente se a configuração de janela for editada.
- `property_sellability`: `property_id` como chave primária; índice parcial em `sellable = true` para a consulta pública.
- Os cinco gates persistidos com código, status, detalhe, `owner_domain` e a **origem do valor** (evento de D06 ou configuração).
- Todas as tabelas no schema `inventory`; nenhuma FK atravessa módulos.
- As configurações são aplicadas por `ApplyConfigurationsFromAssembly`; os `DbSet` entram na tarefa 14.0.

## Arquivos Envolvidos

- **Criar:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Infrastructure/Configurations/InventoryRequestConfiguration.cs`
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Infrastructure/Configurations/PropertySellabilityConfiguration.cs`
- **Referência:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Infrastructure/Configurations/CommercialPolicyConfiguration.cs` (padrão de enums e colunas opcionais)
  - `.tasks/prd-controlar-allotment-disponibilidade-bloqueios-e-retencoes/adrs/adr-002.md` (índices exigidos)
- **Skills para consultar durante implementação:**
  - `dotnet-dependency-config` — `IEntityTypeConfiguration<T>`, owned types para os gates
  - `dotnet-performance` — índice parcial e ordenação coberta por índice
  - `dotnet-architecture` — projeção local, sem join entre módulos

## Subtarefas

- [ ] 13.1 Mapear `InventoryRequest` com enums em texto, campos de SLA persistidos e o índice `(status, priority, received_at)`.
- [ ] 13.2 Mapear `PropertySellability` com chave `property_id`, `sellable` persistido e o índice parcial em `sellable = true`.
- [ ] 13.3 Mapear os cinco `SellabilityGate` como coleção owned, preservando código, status, detalhe, `owner_domain` e origem do valor.

## Sequenciamento

- Bloqueado por: 8.0, 9.0
- Desbloqueia: 14.0
- Paralelizável: Sim; cria dois arquivos exclusivos e não toca o `InventoryDbContext`.

## Rastreabilidade

- Esta tarefa cobre: as notas de implementação de ADR-002 e o suporte de índice para a fila exigida por RF-04.
- Evidência esperada: `InventoryControlPersistenceTests` (14.0) prova a existência dos índices em PostgreSQL real.

## Detalhes de Implementação

Índices exigidos:

| Tabela | Índice | Para quê |
|---|---|---|
| `inventory_requests` | `(status, priority, received_at)` | Ordenação `priorityThenReceivedAt` e cálculo de `overdue` |
| `property_sellability` | PK `(property_id)` | Leitura por propriedade |
| `property_sellability` | `(property_id) WHERE sellable = true` | Consulta pública de disponibilidade |

Por que `sellable` é persistido apesar de derivável: `GET /availability` é público e quente, e precisa filtrar por vendabilidade **antes** de tocar `daily_inventory`. Derivar em tempo de consulta exigiria avaliar cinco gates por linha em cada requisição de descoberta. A consistência é garantida porque `Sellable` só muda dentro de `ApplyGate`, no mesmo objeto (tarefa 8.0).

> A origem de cada gate precisa aparecer na resposta de `sellability` e no runbook. Enquanto D06 não publica, dois dos cinco vêm de configuração — e ninguém pode interpretar configuração como decisão de D06.

**Convenções da stack (das skills consultadas):**

- Uma configuração por entidade, em `Infrastructure/Configurations/` (`dotnet-dependency-config`).
- Gates como coleção owned, evitando tabela satélite sem identidade própria.
- Índice parcial declarado com `HasFilter` (`dotnet-performance`).
- Nenhum join ou FK atravessando módulos (`dotnet-architecture`).

## Critérios de Sucesso (Verificáveis)

- [ ] Build compila sem erros: `dotnet build ../localizestay-backend/LocalizeStay.sln --no-restore`
- [ ] Formatação válida: `dotnet format ../localizestay-backend/LocalizeStay.sln --verify-no-changes --no-restore`
- [ ] Testes de arquitetura verdes: `dotnet test ../localizestay-backend/tests/LocalizeStay.ArchitectureTests`
- [ ] As duas configurações são descobertas por `ApplyConfigurationsFromAssembly`.
- [ ] `inventory_requests` declara o índice `(status, priority, received_at)`.
- [ ] `property_sellability` declara índice parcial com `HasFilter` sobre `sellable = true`.
- [ ] Nenhuma configuração declara FK para tabela fora do schema `inventory`.
