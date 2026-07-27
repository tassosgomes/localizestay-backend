---
status: pending
parallelizable: true
blocked_by: []
---

<task_context>
<domain>inventory/domain/allotments</domain>
<type>implementation</type>
<scope>core_feature</scope>
<complexity>medium</complexity>
<dependencies>database</dependencies>
<unblocks>"11.0, 12.0, 18.0"</unblocks>
<vertical_slice>Um allotment representa a quantidade cedida contratualmente a uma acomodação em um período contínuo, com revisão e sinalização de piso comercial.</vertical_slice>
</task_context>

# Tarefa 6.0: Modelar `Allotment` com período, revisão e piso comercial

## Relacionada às User Stories

- [US-01] Registrar o allotment contratado para que a acomodação passe a ter saldo vendável (direta)

## Visão Geral

`Allotment` é a entidade contratual de RN-02: quantidade **uniforme** cedida exclusivamente à LocalizeStay para uma acomodação em um período contínuo. É o contrato, não a operação do dia — por isso reduzir allotment abaixo do comprometido é proibido, e a operação correta nesse caso é registrar bloqueio.

A não sobreposição de períodos por acomodação é garantida por índice de exclusão PostgreSQL (tarefa 12.0), não apenas por checagem no domínio.

## Requisitos

- `Units` inteiro entre 1 e 999, uniforme em todo o período.
- `StartDate` e `EndDate` em `DateOnly`, **ambos inclusivos**; `EndDate` nunca anterior a `StartDate`.
- `Status` com os três valores do contrato: `active`, `expired`, `cancelled`.
- `BelowCommercialFloor` derivado: `true` quando `Units < 2`. Allotment com uma unidade é **aceito** — a categoria é comercializável, apenas não conta para a meta de cobertura do piloto.
- `Revision` como concurrency token, incrementada a cada alteração, sustentando `expectedRevision` e `409 REVISION_MISMATCH`.
- Trilha de auditoria: `CreatedBy`, `UpdatedBy`, `CreatedAt`, `UpdatedAt`; `CancellationReason` preenchido apenas em `cancelled`.
- Campos opcionais: `ContractReference`, `RequestId` (vínculo com a solicitação que originou), `Notes`.
- A entidade **não** valida sobreposição nem redução abaixo do comprometido — isso pertence ao índice de exclusão e ao `InventoryLedger`.

## Arquivos Envolvidos

- **Criar:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Domain/Allotments/Allotment.cs`
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Domain/Allotments/AllotmentValues.cs`
  - `../localizestay-backend/tests/LocalizeStay.UnitTests/Inventory/AllotmentTests.cs`
- **Referência:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Domain/CommercialOffers/CommercialOffer.cs` (padrão de agregado, revisão e invariantes)
  - `.tasks/prd-controlar-allotment-disponibilidade-bloqueios-e-retencoes/api-contract.md` (schema `Allotment`)
- **Skills para consultar durante implementação:**
  - `dotnet-architecture` — aggregate root, factory, invariantes encapsuladas
  - `dotnet-code-quality` — enums em PascalCase, propriedades somente leitura
  - `dotnet-testing` — `[Theory]` para a matriz de `units` e piso comercial

## Subtarefas

- [ ] 6.1 Declarar `AllotmentStatus` e os códigos auxiliares em `AllotmentValues.cs`.
- [ ] 6.2 Modelar o agregado com factory `Create`, período inclusivo validado e `BelowCommercialFloor` derivado.
- [ ] 6.3 Implementar `ChangeUnits` e `Cancel`, ambos incrementando `Revision` e gravando `UpdatedBy`/`UpdatedAt`; `Cancel` exige motivo.
- [ ] 6.4 Testar: RN-01, RN-02, piso comercial com `units = 1`, período invertido recusado, incremento de revisão e imutabilidade após cancelamento.

## Sequenciamento

- Bloqueado por: Nenhum
- Desbloqueia: 11.0, 12.0, 18.0
- Paralelizável: Sim; domínio puro, arquivos exclusivos desta tarefa.

## Rastreabilidade

- Esta tarefa cobre: RF-01 no domínio, RN-01, RN-02 e a sinalização de piso comercial exigida pelo PRD.
- Evidência esperada: `AllotmentTests` prova que `units = 1` é aceito com `belowCommercialFloor = true`, e que período invertido lança `INVALID_DATE_RANGE`.

## Detalhes de Implementação

Schema-alvo, conforme o contrato:

| Campo | Tipo | Obrigatório | Descrição |
|---|---|:--:|---|
| `units` | `int` (1–999) | Sim | Unidades cedidas, uniformes no período |
| `startDate` / `endDate` | `DateOnly` | Sim | Ambos inclusivos |
| `status` | enum | Sim | `active`, `expired`, `cancelled` |
| `belowCommercialFloor` | `bool` | Sim | `true` quando `units < 2` |
| `contractReference` | `string?` | Não | Instrumento contratual |
| `requestId` | `Guid?` | Não | Solicitação que originou a cessão |
| `revision` | `int` | Sim | Incrementada a cada alteração |
| `cancellationReason` | `string?` | Não | Apenas em `cancelled` |

Regra de negócio que **não** vive aqui, e por quê:

| Regra | Onde vive | Motivo |
|---|---|---|
| Sobreposição de período na mesma acomodação | Índice de exclusão PostgreSQL (12.0) + tradução em `409 ALLOTMENT_PERIOD_OVERLAP` (18.0) | Dois `POST` concorrentes precisam produzir `409`, não dois allotments sobrepostos |
| Redução abaixo do comprometido | `InventoryLedger` (11.0) | Depende do estado de todas as datas do período, que o agregado não conhece |
| Cancelamento com datas comprometidas ou retidas | `InventoryLedger` (11.0) | Mesma razão |

```csharp
public bool BelowCommercialFloor => Units < CommercialFloorUnits; // CommercialFloorUnits = 2
```

**Convenções da stack (das skills consultadas):**

- Aggregate root com construtor privado e factory estática (`dotnet-architecture`).
- Invariantes lançam `BusinessRuleViolationException` com `code` do contrato; ausência lança `NotFoundException` no handler (`dotnet-architecture`).
- Constantes nomeadas em vez de magic numbers — `CommercialFloorUnits`, `MaxUnits` (`dotnet-code-quality`).
- Testes AAA com `[Theory]`/`[InlineData]` cobrindo a matriz de `units` (`dotnet-testing`).

## Critérios de Sucesso (Verificáveis)

- [ ] Testes passam: `dotnet test ../localizestay-backend/LocalizeStay.sln --filter "FullyQualifiedName~AllotmentTests"`
- [ ] Build compila sem erros: `dotnet build ../localizestay-backend/LocalizeStay.sln --no-restore`
- [ ] Formatação válida: `dotnet format ../localizestay-backend/LocalizeStay.sln --verify-no-changes --no-restore`
- [ ] `units = 1` é aceito e produz `BelowCommercialFloor = true`.
- [ ] `units = 0` ou `units > 999` é recusado.
- [ ] `endDate` anterior a `startDate` lança erro com `code = INVALID_DATE_RANGE`.
- [ ] `ChangeUnits` incrementa `Revision` exatamente uma vez por chamada.
- [ ] Allotment `cancelled` recusa novas alterações.
