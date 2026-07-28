---
status: pending
parallelizable: true
blocked_by: []
---

<task_context>
<domain>inventory/application/timing</domain>
<type>implementation</type>
<scope>configuration</scope>
<complexity>medium</complexity>
<dependencies>temporal</dependencies>
<unblocks>"9.0, 22.0"</unblocks>
<vertical_slice>Dado um instante de recebimento, o servidor responde se está fora da janela, quando a janela reabre e qual o prazo de quatro horas úteis.</vertical_slice>
</task_context>

# Tarefa 3.0: Implementar a janela de atendimento do inventário (seg–sáb 08h–20h)

## Relacionada às User Stories

- [US-05] Parceiro solicita allotment e bloqueios pelos canais que já usa (direta — a janela define o SLA da solicitação)
- [US-06] Gestor mede prazo de processamento (suporte)

## Visão Geral

RN-14 e o PRD exigem uma janela de atendimento de **segunda a sábado, 08h00 às 20h00**, incompatível com a janela seg–sex 08h–18h do `IBusinessCalendar` já existente, que sustenta o SLA certificado da F01 e da F02.

ADR-003 decide criar uma abstração própria, `IInventoryServiceWindow`, com seção de configuração própria. `IBusinessCalendar`, `ConfiguredBusinessCalendar` e a seção `Inventory:BusinessCalendar` permanecem **inalterados**.

## Requisitos

- Expor exatamente as três primitivas exigidas pelo contrato: `IsOutsideWindow`, `NextWindowStart` e `AddBusinessHours`.
- Timezone `America/Fortaleza`, resolvido por `TimeZoneInfo` com identificador IANA — **nunca** aritmética de offset fixo.
- `NextWindowStart` devolve o próprio instante quando ele já está dentro da janela; caso contrário, 08h00 do próximo dia útil.
- `AddBusinessHours` acumula apenas tempo dentro da janela, atravessando dias e pulando domingos e feriados.
- Seção `Inventory:InventoryServiceWindow` com dias úteis, horários, SLA de quatro horas e lista de feriados, validada com `ValidateOnStart`: janela não vazia, início anterior ao fim, SLA igual a quatro, feriados válidos e sem duplicatas.
- Lista inicial de feriados: nacionais de 2026 e 2027. **Atenção:** como a janela inclui sábado, feriado que caia no sábado passa a importar — a lista não é um clone da usada pela F01/F02.

## Arquivos Envolvidos

- **Criar:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/Timing/IInventoryServiceWindow.cs` (interface + `InventoryServiceWindowOptions`)
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Infrastructure/Timing/ConfiguredInventoryServiceWindow.cs`
  - `../localizestay-backend/tests/LocalizeStay.UnitTests/Inventory/InventoryServiceWindowTests.cs`
- **Modificar:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/InventoryModule.cs` (options com `ValidateOnStart` + registro singleton)
  - `../localizestay-backend/src/LocalizeStay.Api/appsettings.json` (seção `Inventory:InventoryServiceWindow`)
- **Referência:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Infrastructure/Timing/ConfiguredBusinessCalendar.cs` (algoritmo de janela e feriados a espelhar)
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/Timing/IBusinessCalendar.cs` (formato de `BusinessCalendarOptions`)
  - `.tasks/prd-controlar-allotment-disponibilidade-bloqueios-e-retencoes/adrs/adr-003.md`
- **Skills para consultar durante implementação:**
  - `dotnet-dependency-config` — `IOptions` com `Bind` + `Validate` + `ValidateOnStart`
  - `dotnet-architecture` — porta em `Application/Timing/`, adaptador em `Infrastructure/Timing/`
  - `dotnet-testing` — `[Theory]` com `[InlineData]` para a matriz de instantes

## Subtarefas

- [ ] 3.1 Declarar `IInventoryServiceWindow` com as três primitivas e `InventoryServiceWindowOptions` com `SectionName = "Inventory:InventoryServiceWindow"`.
- [ ] 3.2 Implementar `ConfiguredInventoryServiceWindow` com conversão IANA, salto de domingos e feriados e acumulação de horas úteis.
- [ ] 3.3 Registrar options com `ValidateOnStart` e o singleton no `InventoryModule`; adicionar a seção ao `appsettings.json` com os feriados nacionais de 2026 e 2027.
- [ ] 3.4 Testar a matriz: dentro/fora da janela, sábado útil, domingo, feriado em sábado, virada de dia, e o caso canônico do contrato.

## Sequenciamento

- Bloqueado por: Nenhum
- Desbloqueia: 9.0, 22.0
- Paralelizável: Sim; nenhum outro arquivo da F03 depende destes até a tarefa 9.0.

## Rastreabilidade

- Esta tarefa cobre: RN-14 e o requisito do PRD de janela seg–sáb 08h–20h com contagem a partir das 08h00 do próximo período útil.
- Evidência esperada: `InventoryServiceWindowTests` reproduz o caso canônico do contrato e a suíte `BusinessCalendarTests` da F01/F02 segue verde sem alteração.

## Detalhes de Implementação

```csharp
internal interface IInventoryServiceWindow
{
    bool IsOutsideWindow(DateTimeOffset instantUtc);

    DateTimeOffset NextWindowStart(DateTimeOffset instantUtc);

    DateTimeOffset AddBusinessHours(DateTimeOffset startUtc, int hours);
}
```

**Caso de teste canônico, retirado do contrato:**

| Entrada | Saída esperada |
|---|---|
| `receivedAt = 2026-07-26T03:40:00Z` (00h40 local, domingo) | `IsOutsideWindow = true` |
| | `NextWindowStart = 2026-07-27T11:00:00Z` (08h00 local de segunda) |
| | `AddBusinessHours(NextWindowStart, 4) = 2026-07-27T15:00:00Z` |

> O exemplo do contrato usa `2026-07-27T11:00:00Z` como `slaStartsAt` e `2026-07-27T15:00:00Z` como `slaDueAt`; a implementação deve reproduzir exatamente esses três instantes.

Configuração-alvo:

```json
"Inventory": {
  "InventoryServiceWindow": {
    "Version": "2026.1",
    "TimeZone": "America/Fortaleza",
    "WorkingDays": ["Monday","Tuesday","Wednesday","Thursday","Friday","Saturday"],
    "StartTime": "08:00",
    "EndTime": "20:00",
    "ProcessingSlaBusinessHours": 4,
    "Holidays": ["2026-01-01", "..."]
  }
}
```

**Convenções da stack (das skills consultadas):**

- Options validadas no startup com mensagem explícita, como `BusinessCalendarOptions` e `LegalPolicyOptions` (`dotnet-dependency-config`).
- Porta em `Application/`, adaptador em `Infrastructure/`, registro no `InventoryModule` (`dotnet-architecture`).
- Sem aritmética de offset fixo; `TimeZoneInfo.FindSystemTimeZoneById("America/Fortaleza")`.
- Métodos com verbo, ≤ 50 linhas, ≤ 2 níveis de aninhamento (`dotnet-code-quality`).
- Testes parametrizados com `[Theory]`/`[InlineData]` (`dotnet-testing`).

## Critérios de Sucesso (Verificáveis)

- [ ] Testes passam: `dotnet test ../localizestay-backend/LocalizeStay.sln --filter "FullyQualifiedName~InventoryServiceWindowTests"`
- [ ] Build compila sem erros: `dotnet build ../localizestay-backend/LocalizeStay.sln --no-restore`
- [ ] Formatação válida: `dotnet format ../localizestay-backend/LocalizeStay.sln --verify-no-changes --no-restore`
- [ ] O caso canônico do contrato produz `slaStartsAt = 2026-07-27T11:00:00Z` e `slaDueAt = 2026-07-27T15:00:00Z`.
- [ ] Configuração inválida (fim antes do início, feriado duplicado, SLA ≠ 4) falha no startup com mensagem explícita.
- [ ] O SLA da F01/F02 não muda: `dotnet test ../localizestay-backend/LocalizeStay.sln --filter "FullyQualifiedName~BusinessCalendarTests"`
