---
status: pending
parallelizable: true
blocked_by: []
---

<task_context>
<domain>inventory/domain/daily-inventory</domain>
<type>implementation</type>
<scope>core_feature</scope>
<complexity>medium</complexity>
<dependencies>database</dependencies>
<unblocks>"11.0, 12.0, 37.0"</unblocks>
<vertical_slice>Uma noite de uma acomodação sabe quanto foi cedido, comprometido, retido e bloqueado, e deriva o saldo vendável com piso zero.</vertical_slice>
</task_context>

# Tarefa 5.0: Modelar `DailyInventory` com saldo derivado e piso zero

## Relacionada às User Stories

- [US-02] Enxergar no calendário o que foi cedido, comprometido, retido, bloqueado e o que restou (direta)
- [US-01] Registrar allotment para gerar saldo vendável (suporte)

## Visão Geral

`DailyInventory` é a entidade central da F03: uma linha por `(accommodation_id, date)` que traduz RN-03 em uma linha por noite vendável. É a peça sobre a qual o `InventoryLedger` opera e da qual todas as consultas de saldo derivam.

O saldo disponível **não é persistido**: é derivado como `GREATEST(allotted − committed − held − blocked, 0)`, satisfazendo o piso zero por construção. Data sem allotment vale zero, nunca indefinido.

## Requisitos

- Chave de identidade composta por `AccommodationId` e `Date` (`DateOnly`).
- Contadores `AllottedUnits`, `CommittedUnits`, `HeldUnits` e `BlockedUnits`, todos inteiros não negativos.
- `AvailableUnits` é propriedade **calculada**, com piso zero — nunca coluna persistida, nunca negativa.
- Vínculo opcional com o `AllotmentId` que materializou a linha.
- Mutação apenas por métodos de comportamento com nome de verbo, todos validando invariante de não negatividade. Nenhum setter público.
- A entidade **não** decide se uma operação é permitida — essa decisão é do `InventoryLedger` (11.0). Aqui vivem apenas as invariantes de integridade do contador.

## Arquivos Envolvidos

- **Criar:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Domain/DailyInventories/DailyInventory.cs`
  - `../localizestay-backend/tests/LocalizeStay.UnitTests/Inventory/DailyInventoryTests.cs`
- **Referência:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Domain/CommercialOffers/CommercialOffer.cs` (padrão de agregado, construtor privado, invariantes)
  - `.tasks/prd-controlar-allotment-disponibilidade-bloqueios-e-retencoes/adrs/adr-001.md`
  - `.tasks/prd-controlar-allotment-disponibilidade-bloqueios-e-retencoes/api-contract.md` (schema `DailyInventory`)
- **Skills para consultar durante implementação:**
  - `dotnet-architecture` — entidade com comportamento, invariantes encapsuladas
  - `dotnet-code-quality` — nomes de método com verbo, sem flag params, ≤ 2 níveis de aninhamento
  - `dotnet-testing` — AAA, `[Theory]` para a matriz de contadores

## Subtarefas

- [ ] 5.1 Modelar a entidade com identidade composta, os quatro contadores e `AvailableUnits` derivado com piso zero.
- [ ] 5.2 Implementar as mutações de contador (`SetAllotted`, `AddCommitted`, `ReleaseCommitted`, `AddHeld`, `ReleaseHeld`, `AddBlocked`, `ReleaseBlocked`), cada uma recusando resultado negativo com `BusinessRuleViolationException`.
- [ ] 5.3 Implementar a factory que cria a linha para uma data sem allotment com todos os contadores em zero.
- [ ] 5.4 Testar: RN-03 completo, piso zero, data sem allotment igual a zero, e recusa de contador negativo.

## Sequenciamento

- Bloqueado por: Nenhum
- Desbloqueia: 11.0, 12.0, 37.0
- Paralelizável: Sim; domínio puro, sem dependência de outras entidades da F03.

## Rastreabilidade

- Esta tarefa cobre: RN-03 integralmente no domínio, e o schema `DailyInventory` do contrato.
- Evidência esperada: `DailyInventoryTests` prova o piso zero, a derivação e a recusa de estado inválido.

## Detalhes de Implementação

Schema-alvo, conforme o contrato:

| Campo | Tipo | Descrição |
|---|---|---|
| `date` | `DateOnly` | Data da diária |
| `allottedUnits` | `int ≥ 0` | Total cedido; 0 quando não há allotment |
| `committedUnits` | `int ≥ 0` | Reservas confirmadas |
| `heldUnits` | `int ≥ 0` | Retenções vigentes; 0 antes da Onda B |
| `blockedUnits` | `int ≥ 0` | Bloqueios ativos |
| `availableUnits` | `int ≥ 0` | **Derivado**, com piso zero |

```csharp
public int AvailableUnits => Math.Max(AllottedUnits - CommittedUnits - HeldUnits - BlockedUnits, 0);
```

`FreeUnits` (saldo livre, usado pelo bloqueio planejado) é o mesmo cálculo — bloqueio planejado só alcança o que está livre, jamais retenção vigente ou reserva confirmada.

> **Atenção:** o piso zero na leitura não pode mascarar estado inconsistente. Se `allotted − committed − held − blocked` for negativo, o objeto está corrompido: as mutações devem impedir que isso aconteça, e a tarefa 36.0 prova por reconstrução que não aconteceu.

**Convenções da stack (das skills consultadas):**

- Construtor privado + factory estática; EF materializa por construtor privado, seguindo o padrão de `CommercialOffer` (`dotnet-architecture`).
- Invariantes lançam `BusinessRuleViolationException` com o `code` estável do contrato (`dotnet-architecture`).
- Métodos começam com verbo, sem efeito colateral misto de consulta e mutação (`dotnet-code-quality`).
- Testes xUnit + AwesomeAssertions em AAA, naming `Metodo_Condicao_ComportamentoEsperado` (`dotnet-testing`).

## Critérios de Sucesso (Verificáveis)

- [ ] Testes passam: `dotnet test ../localizestay-backend/LocalizeStay.sln --filter "FullyQualifiedName~DailyInventoryTests"`
- [ ] Build compila sem erros: `dotnet build ../localizestay-backend/LocalizeStay.sln --no-restore`
- [ ] Formatação válida: `dotnet format ../localizestay-backend/LocalizeStay.sln --verify-no-changes --no-restore`
- [ ] `AvailableUnits` nunca é negativo, para qualquer combinação de contadores.
- [ ] Linha criada sem allotment tem `AllottedUnits = 0` e `AvailableUnits = 0`.
- [ ] Tentar liberar mais do que está retido, comprometido ou bloqueado lança `BusinessRuleViolationException`.
- [ ] Nenhuma propriedade tem setter público.
