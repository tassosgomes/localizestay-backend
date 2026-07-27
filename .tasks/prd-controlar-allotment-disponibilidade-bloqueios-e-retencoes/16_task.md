---
status: pending
parallelizable: true
blocked_by: []
---

<task_context>
<domain>inventory/application/validation</domain>
<type>implementation</type>
<scope>core_feature</scope>
<complexity>medium</complexity>
<dependencies>http_server</dependencies>
<unblocks>"18.0, 19.0, 21.0, 22.0, 23.0, 24.0"</unblocks>
<vertical_slice>As regras de intervalo e obrigatoriedade compartilhadas por todas as operações do inventário existem em um único lugar e produzem os códigos de erro do contrato.</vertical_slice>
</task_context>

# Tarefa 16.0: Definir as regras de validação compartilhadas do inventário

## Relacionada às User Stories

- [US-02] Calendário de inventário (suporte — o teto de 92 dias protege a grade)
- [US-05] Solicitações por WhatsApp e e-mail (suporte — `receivedAt` não pode ser futuro)

## Visão Geral

Cinco regras aparecem em mais de uma operação do contrato e não pertencem a nenhuma fatia específica: teto de janela do calendário, teto de estadia, coerência de intervalo, `receivedAt` não futuro e `reasonNote` obrigatório.

Concentrá-las aqui evita que cada fatia reimplemente a mesma checagem com mensagem e `code` diferentes — e evita que as tarefas 18.0 a 24.0, que correm em paralelo, colidam num arquivo comum de validators.

> **Desvio declarado em relação à TechSpec:** o Inventário de Artefatos previa um único `InventoryValidators.cs` com todos os validators da F03. Um arquivo assim dependeria dos tipos de Command declarados em doze tarefas diferentes, criando dependência circular e colisão entre fatias paralelas. Aqui ele contém as **regras transversais**; os validators específicos de cada Command/Query ficam no arquivo da própria fatia.

## Requisitos

- `DATE_RANGE_TOO_LARGE` para calendário acima de 92 dias e estadia acima de 30 noites.
- `INVALID_DATE_RANGE` para `endDate` anterior a `startDate` e `checkOut` não posterior a `checkIn`.
- `receivedAt` não pode ser futuro.
- `reason: other` exige `reasonNote` não vazio.
- Regras expostas como extensões reutilizáveis de `IRuleBuilder` do FluentValidation, consumíveis por qualquer validator de fatia.
- Diferença de semântica de período explícita e testada: allotment e bloqueio usam `startDate`/`endDate` **inclusivos**; estadia usa `checkIn` inclusivo e `checkOut` **exclusivo**.

## Arquivos Envolvidos

- **Criar:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/Inventory/InventoryValidators.cs`
  - `../localizestay-backend/tests/LocalizeStay.UnitTests/Inventory/InventoryValidatorsTests.cs`
- **Referência:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/CommercialOffers/CommercialOfferValidators.cs` (padrão do módulo)
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/Validation/InventoryValidators.cs` (arquivo homônimo da F01; **não** confundir)
  - `.tasks/prd-controlar-allotment-disponibilidade-bloqueios-e-retencoes/api-contract.md` (tabela de códigos de erro)
- **Skills para consultar durante implementação:**
  - `dotnet-dependency-config` — FluentValidation, registro por assembly
  - `restful-api` — mapeamento validação → 400/422 e `code` estável
  - `dotnet-testing` — `[Theory]` cobrindo os limites exatos

## Subtarefas

- [ ] 16.1 Declarar as extensões de regra de intervalo: período inclusivo coerente, janela de calendário ≤ 92 dias, estadia ≤ 30 noites com `checkOut` exclusivo.
- [ ] 16.2 Declarar as regras de `receivedAt` não futuro e `reasonNote` obrigatório em `reason: other`.
- [ ] 16.3 Testar os limites exatos: 92 e 93 dias, 30 e 31 noites, `checkOut = checkIn`, `receivedAt` um segundo no futuro.

## Sequenciamento

- Bloqueado por: Nenhum
- Desbloqueia: 18.0, 19.0, 21.0, 22.0, 23.0, 24.0
- Paralelizável: Sim; é a segunda das duas tarefas que fixam contratos internos compartilhados.

## Rastreabilidade

- Esta tarefa cobre: as validações adicionais transversais listadas na TechSpec e os códigos `INVALID_DATE_RANGE`, `DATE_RANGE_TOO_LARGE` e `REASON_NOTE_REQUIRED`.
- Evidência esperada: `InventoryValidatorsTests` prova cada limite; as fatias 18.0 a 24.0 consomem as extensões.

## Detalhes de Implementação

Semântica de período — a fonte mais provável de bug nesta feature:

| Contexto | Início | Fim | Exemplo |
|---|---|---|---|
| Allotment, bloqueio | `startDate` inclusivo | `endDate` **inclusivo** | 01/09 a 03/09 = 3 datas |
| Estadia, retenção | `checkIn` inclusivo | `checkOut` **exclusivo** | 14/09 a 17/09 = 3 noites |

> A conversão de estadia para noites acontece **na borda**, nunca no domínio. O `InventoryLedger` sempre recebe um intervalo de datas inclusivo em ambas as pontas.

Limites do contrato:

| Regra | Limite | `code` |
|---|---:|---|
| Janela do calendário | 92 dias | `DATE_RANGE_TOO_LARGE` |
| Estadia | 30 noites | `DATE_RANGE_TOO_LARGE` |
| Intervalo incoerente | — | `INVALID_DATE_RANGE` |
| `reason: other` sem nota | — | `REASON_NOTE_REQUIRED` |

**Convenções da stack (das skills consultadas):**

- FluentValidation com validators registrados por assembly, como já é feito no módulo (`dotnet-dependency-config`).
- Mensagens de validação em inglês (`dotnet-architecture`).
- Erro sintático vira `400 BAD_REQUEST`; violação de regra de negócio vira `422` com `code` estável (`restful-api`).
- Constantes nomeadas para os limites — `MaxCalendarDays = 92`, `MaxStayNights = 30` (`dotnet-code-quality`).

## Critérios de Sucesso (Verificáveis)

- [ ] Testes passam: `dotnet test ../localizestay-backend/LocalizeStay.sln --filter "FullyQualifiedName~InventoryValidatorsTests"`
- [ ] Build compila sem erros: `dotnet build ../localizestay-backend/LocalizeStay.sln --no-restore`
- [ ] Formatação válida: `dotnet format ../localizestay-backend/LocalizeStay.sln --verify-no-changes --no-restore`
- [ ] Calendário de 92 dias é aceito; de 93, recusado com `DATE_RANGE_TOO_LARGE`.
- [ ] Estadia de 30 noites é aceita; de 31, recusada.
- [ ] `checkOut` igual a `checkIn` é recusado com `INVALID_DATE_RANGE`.
- [ ] `reason: other` sem `reasonNote` é recusado com `REASON_NOTE_REQUIRED`.
- [ ] As regras são extensões reutilizáveis, não validators acoplados a um Command específico.
