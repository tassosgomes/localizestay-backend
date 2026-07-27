---
status: pending
parallelizable: true
blocked_by: ["5.0", "6.0", "7.0", "8.0", "9.0"]
---

<task_context>
<domain>inventory/application/contracts</domain>
<type>implementation</type>
<scope>core_feature</scope>
<complexity>medium</complexity>
<dependencies>database</dependencies>
<unblocks>"18.0, 19.0, 21.0, 22.0, 23.0, 24.0, 31.0"</unblocks>
<vertical_slice>Os tipos de resposta internos correspondentes aos schemas do contrato existem e sabem se produzir a partir do domínio.</vertical_slice>
</task_context>

# Tarefa 15.0: Definir os DTOs internos e o mapeamento manual da Onda A

## Relacionada às User Stories

- [US-02] Enxergar a composição do saldo no calendário (direta — o DTO decide o que a Operação vê)
- [US-06] Medir indicadores (suporte)

## Visão Geral

Esta tarefa fixa os **tipos compartilhados** entre todas as fatias de aplicação da Onda A, para que as tarefas 18.0 a 24.0 possam correr em paralelo sem colidir em arquivos de contrato interno.

O ponto mais sensível é a separação entre o que `GET /availability` devolve e o que o backoffice devolve. **A composição interna do saldo — comprometido, retido, bloqueado e motivo — nunca aparece na resposta pública.** O viajante vê apenas `bookable`, `availableUnits` e um motivo deliberadamente genérico.

## Requisitos

- Records internos correspondentes aos schemas do contrato: `AllotmentResponse`, `InventoryBlockResponse`, `InventoryRequestResponse`, `DailyInventoryResponse`, `InventoryCalendarResponse`, `SellabilityResponse`, `AvailabilityResponse` e os itens de coleção.
- `AvailabilityResponse` expõe **apenas** `bookable`, `availableUnits`, `unavailabilityReason` e `firstUnavailableDate`. Nenhum campo de composição.
- Reservas e retenções, no detalhe da data, aparecem **apenas por identificador e quantidade**. Dados do viajante pertencem a D03 e não transitam por aqui.
- Arrays vazios são sempre `[]`, nunca `null`.
- Mapeamento **manual e explícito**, sem Mapster — desvio aprovado e já adotado no módulo.
- `StaffActor` (`id` + `displayName`) vem do JWT, nunca do corpo da requisição.
- Os DTOs da Onda B (`InventoryHoldResponse`, `CommitmentResponse`) entram na tarefa 42.0, no mesmo arquivo.

## Arquivos Envolvidos

- **Criar:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/Inventory/InventoryDtos.cs`
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/Inventory/InventoryMapper.cs`
  - `../localizestay-backend/tests/LocalizeStay.UnitTests/Inventory/InventoryMapperTests.cs`
- **Referência:**
  - `.tasks/prd-controlar-allotment-disponibilidade-bloqueios-e-retencoes/api-contract.yaml` (`components/schemas` — fonte soberana)
  - `.tasks/prd-controlar-allotment-disponibilidade-bloqueios-e-retencoes/api-contract.md` (exemplos de resposta de cada operação)
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/CommercialOffers/CommercialOfferDtos.cs` (padrão do módulo)
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/CommercialOffers/CommercialOfferMapper.cs`
- **Skills para consultar durante implementação:**
  - `restful-api` — camelCase em JSON, arrays vazios como `[]`, nomes dos campos conforme o contrato
  - `dotnet-code-quality` — records imutáveis, mapeamento explícito, sem reflexão
  - `dotnet-testing` — AAA cobrindo a ausência de campos sensíveis

## Subtarefas

- [ ] 15.1 Declarar os records de resposta da Onda A, um por schema do contrato, com os nomes de campo exatamente como declarados no YAML.
- [ ] 15.2 Implementar `InventoryMapper` com um método por conversão domínio → DTO, explícito e sem reflexão.
- [ ] 15.3 Garantir que `AvailabilityResponse` não tenha nenhum campo de composição interna e que o detalhe da data exponha reserva e retenção apenas por `id` e `units`.
- [ ] 15.4 Testar: cada mapeamento produz os campos esperados; coleções vazias viram `[]`; nenhum campo de composição vaza para o DTO público.

## Sequenciamento

- Bloqueado por: 5.0, 6.0, 7.0, 8.0, 9.0
- Desbloqueia: 18.0, 19.0, 21.0, 22.0, 23.0, 24.0, 31.0
- Paralelizável: Sim; é uma das duas tarefas que fixam contratos internos compartilhados, justamente para que as fatias seguintes não colidam.

## Rastreabilidade

- Esta tarefa cobre: os schemas de resposta do contrato e o requisito especial do PRD de que `GET /availability` não exponha composição interna do saldo.
- Evidência esperada: `InventoryMapperTests` prova a ausência de campos sensíveis; a tarefa 34.0 certifica isso fim a fim pela API.

## Detalhes de Implementação

**Resposta pública** (`GET /availability`) — o que pode aparecer:

```json
{
  "propertyId": "...",
  "propertyName": "Pousada Mar do Sol",
  "accommodationId": "...",
  "accommodationName": "Chalé Vista Mar",
  "bookable": false,
  "availableUnits": 0,
  "unavailabilityReason": "insufficientBalance",
  "firstUnavailableDate": "2026-09-15"
}
```

**Resposta de backoffice** (`GET /daily-inventory/{date}`) — onde a composição é legítima:

```json
{
  "allottedUnits": 3, "committedUnits": 1, "heldUnits": 1, "blockedUnits": 1, "availableUnits": 0,
  "blocks": [],
  "commitments": [{ "reservationId": "...", "checkIn": "...", "checkOut": "...", "units": 1 }],
  "holds": [{ "holdId": "...", "reservationIntentId": "...", "units": 1, "expiresAt": "..." }]
}
```

> A diferença entre os dois é uma decisão de segurança, não de conveniência. `unavailabilityReason` é deliberadamente genérico: dizer ao viajante que a data está *bloqueada* revelaria operação interna do parceiro.

**Convenções da stack (das skills consultadas):**

- Records internos ao assembly do módulo, um arquivo por área (`dotnet-architecture`).
- Mapeamento manual, alternativa explicitamente permitida e já adotada (`dotnet-dependency-config`).
- JSON em camelCase, `date` para diárias e `date-time` UTC para instantes (`restful-api`).
- Nenhum dado do viajante em log ou DTO desta feature (`dotnet-production-readiness`).

## Critérios de Sucesso (Verificáveis)

- [ ] Testes passam: `dotnet test ../localizestay-backend/LocalizeStay.sln --filter "FullyQualifiedName~InventoryMapperTests"`
- [ ] Build compila sem erros: `dotnet build ../localizestay-backend/LocalizeStay.sln --no-restore`
- [ ] Formatação válida: `dotnet format ../localizestay-backend/LocalizeStay.sln --verify-no-changes --no-restore`
- [ ] `AvailabilityResponse` não declara `committedUnits`, `heldUnits`, `blockedUnits` nem `blocks`.
- [ ] Coleções vazias são serializadas como `[]`.
- [ ] Nenhum DTO carrega nome, documento, e-mail ou telefone de viajante.
- [ ] Os nomes de campo batem com `components/schemas` do `api-contract.yaml`.
