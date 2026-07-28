---
status: pending
parallelizable: true
blocked_by: ["14.0", "15.0", "16.0", "17.0"]
---

<task_context>
<domain>inventory/application/availability</domain>
<type>implementation</type>
<scope>core_feature</scope>
<complexity>medium</complexity>
<dependencies>database</dependencies>
<unblocks>"27.0"</unblocks>
<vertical_slice>Para uma estadia, o sistema responde se a acomodação é vendável — todas as noites com saldo e os cinco gates satisfeitos — sem expor a composição interna.</vertical_slice>
</task_context>

# Tarefa 23.0: Consultar disponibilidade pública e diagnosticar vendabilidade

## Relacionada às User Stories

- [US-02] Diagnosticar uma data sem alternar telas (direta — via `sellability`)
- [US-04] Acomodação garantida durante o checkout (suporte — a consulta antecede a retenção)

## Visão Geral

Duas operações com públicos opostos: `getAvailability` é **público**, no caminho quente de D01, e diz apenas se a acomodação pode ser vendida; `getPropertySellability` é de backoffice e explica **por que** uma propriedade não vende mesmo com saldo.

Uma acomodação só é `bookable` quando **todas** as noites entre `checkIn` (inclusivo) e `checkOut` (exclusivo) têm saldo suficiente **e** os cinco gates de RN-07 estão satisfeitos.

## Requisitos

- A avaliação de RN-07 acontece **antes** da checagem de saldo: propriedade não vendável nunca é retornada como disponível, independentemente do saldo.
- Os cinco gates são lidos de `property_sellability` com **uma leitura indexada**. Nenhuma chamada síncrona a outro módulo.
- Data sem allotment tem saldo zero, nunca indefinido.
- Retenção vencida não conta como retida (guarda de ADR-004).
- A resposta pública expõe apenas `bookable`, `availableUnits`, `unavailabilityReason` (genérico) e `firstUnavailableDate`. **Nunca** a composição interna do saldo.
- `getPropertySellability` devolve os cinco gates com `code`, `status`, `detail`, `ownerDomain` e `evaluatedAt`, mais `sellable` e `suspendedByCuration`.
- `suspendedByCuration` deriva do gate `propertyApproved` combinado com a existência de bloqueio ativo de origem `curationSuspension`.
- Estadia acima de 30 noites produz `422 DATE_RANGE_TOO_LARGE`; `checkOut` não posterior a `checkIn` produz `422 INVALID_DATE_RANGE`.
- Leituras com `AsNoTracking` e projeção direta. **Sem cache** — cache jamais pode ser fonte de verdade de disponibilidade.

## Arquivos Envolvidos

- **Criar:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/Availability/AvailabilityQueries.cs`
  - `../localizestay-backend/tests/LocalizeStay.UnitTests/Inventory/AvailabilityQueryHandlerTests.cs`
- **Referência:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Domain/Sellability/PropertySellability.cs` (criado em 8.0)
  - `.tasks/prd-controlar-allotment-disponibilidade-bloqueios-e-retencoes/adrs/adr-002.md`
  - `.tasks/prd-controlar-allotment-disponibilidade-bloqueios-e-retencoes/api-contract.md` (exemplos de `getAvailability` e `getPropertySellability`)
- **Skills para consultar durante implementação:**
  - `dotnet-performance` — range scan por `(accommodation_id, date)`, `AsNoTracking`, ausência deliberada de cache
  - `dotnet-architecture` — CQRS, leitura sem cadeia síncrona entre módulos
  - `dotnet-testing` — matriz de gates × saldo

## Subtarefas

- [ ] 23.1 Implementar `GetAvailabilityQueryHandler`: avaliar os cinco gates, depois checar saldo em todas as noites da estadia, com piso zero e guarda de retenção vencida.
- [ ] 23.2 Definir os motivos genéricos de indisponibilidade e `firstUnavailableDate`, sem revelar composição interna nem operação do parceiro.
- [ ] 23.3 Implementar `GetPropertySellabilityQueryHandler`, devolvendo os cinco gates com `ownerDomain` e derivando `suspendedByCuration`.
- [ ] 23.4 Testar: gate isolado derruba a disponibilidade; noite sem saldo derruba a estadia inteira; data sem allotment vale zero; nenhum campo de composição na resposta pública.

## Sequenciamento

- Bloqueado por: 14.0, 15.0, 16.0, 17.0
- Desbloqueia: 27.0
- Paralelizável: Sim; cria arquivos exclusivos.

## Rastreabilidade

- Esta tarefa cobre: RF-03 integralmente na camada de aplicação, RN-07 e o requisito especial do PRD de não expor composição interna.
- Evidência esperada: `AvailabilityQueryHandlerTests` prova os três critérios de aceite de RF-03; 34.0 certifica o não vazamento pela API.

## Detalhes de Implementação

Critérios de aceite de RF-03 mapeados:

| Critério | Verificação |
|---|---|
| Só é disponível se **todas** as noites tiverem saldo suficiente | Uma noite sem saldo derruba a estadia; `firstUnavailableDate` aponta qual |
| Propriedade sem aprovação, tarifa válida ou canal testado não é vendável, independentemente do saldo | Gate avaliado antes do saldo |
| Data sem allotment tem saldo zero, não indefinido | Ausência de linha lida como zero |

Resposta pública — tudo o que pode aparecer:

```json
{
  "bookable": false,
  "availableUnits": 0,
  "unavailabilityReason": "insufficientBalance",
  "firstUnavailableDate": "2026-09-15"
}
```

> `unavailabilityReason` é **deliberadamente genérico**. Dizer ao viajante que a data está *bloqueada* revelaria operação interna do parceiro; dizer que está *retida* revelaria concorrência de checkout. O backoffice tem `sellability` e o calendário para o diagnóstico real.

Conversão de estadia para noites acontece **na borda**: `checkIn` inclusivo e `checkOut` exclusivo viram um intervalo de datas inclusivo antes de consultar `daily_inventory`.

**Convenções da stack (das skills consultadas):**

- Range scan por `(accommodation_id, date)` — sem agregação nem join com tabelas de movimento (`dotnet-performance`).
- `AsNoTracking` e projeção direta para DTO; nenhuma entidade materializada.
- **Sem cache**, por ADR-0002 e porque cache não pode ser fonte de verdade de disponibilidade.
- Nenhuma chamada síncrona a outro módulo (ADR-002, baseline de arquitetura).
- Span `inventory.availability.query` e histograma `inventory.availability.query_duration` entram na tarefa 32.0 (`dotnet-observability`).

## Critérios de Sucesso (Verificáveis)

- [ ] Testes passam: `dotnet test ../localizestay-backend/LocalizeStay.sln --filter "FullyQualifiedName~AvailabilityQueryHandlerTests"`
- [ ] Build compila sem erros: `dotnet build ../localizestay-backend/LocalizeStay.sln --no-restore`
- [ ] Formatação válida: `dotnet format ../localizestay-backend/LocalizeStay.sln --verify-no-changes --no-restore`
- [ ] Propriedade com qualquer gate `blocked` devolve `bookable: false`, mesmo com saldo abundante.
- [ ] Estadia com uma única noite sem saldo devolve `bookable: false` e `firstUnavailableDate` correto.
- [ ] Data sem linha em `daily_inventory` devolve `availableUnits: 0`.
- [ ] Retenção com `expiresAt` no passado não reduz `availableUnits`.
- [ ] A resposta de `getAvailability` não contém `committedUnits`, `heldUnits`, `blockedUnits` nem `blocks`.
- [ ] Estadia de 31 noites produz `DATE_RANGE_TOO_LARGE`.
