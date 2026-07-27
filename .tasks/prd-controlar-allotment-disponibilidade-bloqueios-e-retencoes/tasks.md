# Resumo de Tarefas de Implementação — F03: Controlar Allotment, Disponibilidade, Bloqueios e Retenções

## Visão Geral

Este plano implementa a F03 como terceira capacidade vertical do módulo `Inventory`. O trabalho cria o saldo vendável materializado por noite (`daily_inventory`), o allotment contratual, os bloqueios planejado e emergencial, a fila de solicitações com SLA, a projeção de vendabilidade de RN-07, as 23 operações HTTP do contrato, os seis eventos de domínio, o ciclo completo de retenção e a certificação automatizada.

O plano tem **51 tarefas em nove fases**, separadas pelas duas ondas do PRD: a Onda A (fases 1 a 6, RF-01 a RF-05) entrega inventário e consulta de disponibilidade; a Onda B (fases 7 a 9, RF-06 a RF-08) entrega o ciclo de retenção. Nenhuma tarefa da Onda B entra em produção antes da validação ponta a ponta com `D03-C01`.

A fragmentação é fina por decisão: cada tarefa é uma fatia vertical de um recurso ou de uma regra, com no máximo 3 arquivos criados, 3 modificados e 4 subtarefas, para que o gate determinístico consiga provar cada uma isoladamente por filtro de teste próprio.

O `api-contract.yaml` permanece a fonte soberana da superfície HTTP e não é alterado por nenhuma tarefa.

## Skills de Stack Consultadas

| Skill | Caminho | Influência |
|---|---|---|
| `dotnet-architecture` | `/home/tsgomes/.claude/skills/dotnet-architecture/SKILL.md` | CQRS nativo sem MediatR, domínio com invariantes encapsuladas, `IExceptionHandler` global, DI por construtor, limites de camada |
| `dotnet-testing` | `/home/tsgomes/.claude/skills/dotnet-testing/SKILL.md` | xUnit + AwesomeAssertions + Moq, padrão AAA, naming `Metodo_Condicao_ComportamentoEsperado`, `WebApplicationFactory` + Testcontainers PostgreSQL, cobertura > 80% em lógica de negócio, `CancellationToken` testado |
| `dotnet-code-quality` | `/home/tsgomes/.claude/skills/dotnet-code-quality/SKILL.md` | Código em inglês, PascalCase/camelCase, métodos ≤ 50 linhas e ≤ 3 parâmetros, classes ≤ 300 linhas, ≤ 2 níveis de aninhamento, `async`/`await` sem bloqueio, `CancellationToken` propagado |
| `dotnet-dependency-config` | `/home/tsgomes/.claude/skills/dotnet-dependency-config/SKILL.md` | EF Core + PostgreSQL, migrations, `IOptions` com `ValidateOnStart`, FluentValidation, outbox transacional |
| `dotnet-observability` | `/home/tsgomes/.claude/skills/dotnet-observability/SKILL.md` | Logs estruturados com scopes de correlação, spans OpenTelemetry por operação, métricas de baixa cardinalidade, health checks |
| `dotnet-production-readiness` | `/home/tsgomes/.claude/skills/dotnet-production-readiness/SKILL.md` | Templates de log estruturado obrigatórios, níveis de log por severidade, proibição de dado sensível em log, checklist de deploy |
| `dotnet-performance` | `/home/tsgomes/.claude/skills/dotnet-performance/SKILL.md` | `AsNoTracking`, projeções EF, índices dedicados, paginação, ausência deliberada de cache |
| `restful-api` | `/home/tsgomes/.claude/skills/restful-api/SKILL.md` | OpenAPI 3.1 design-first, versionamento em path, paginação `_page`/`_size`, RFC 9457 em `application/problem+json`, `Location` em 201, corpo ausente em 204 |
| `common-roles-naming` | `/home/tsgomes/.claude/skills/common-roles-naming/SKILL.md` | Nomes das cinco permissões `inventory:*` no padrão `<recurso-kebab>:<ação>` |

## Decisões e Gates de Execução

- Executar os comandos de verificação a partir da raiz do repositório; os caminhos usam o prefixo `../localizestay-backend/`, que resolve corretamente tanto da raiz do backend quanto do repositório meta.
- Preservar os desvios aprovados na TechSpec: um assembly por módulo com tipos `internal`, handlers usando `InventoryDbContext` diretamente sem repositório, mapeamento manual sem Mapster, SQL bruto para `SELECT ... FOR UPDATE` e `SKIP LOCKED`, sem cache e sem projeção assíncrona nas métricas.
- **Toda mutação de saldo passa obrigatoriamente pelo `InventoryLedger`.** Nenhum handler executa `UPDATE` direto em `daily_inventory`. A tarefa 36.0 existe para provar isso por reconstrução.
- **Ordem de aquisição de lock sempre `ORDER BY date` crescente**, em todos os caminhos de escrita sem exceção (ADR-001). É a única mitigação de deadlock do plano.
- **Retenção vencida nunca conta como retida na leitura** — o filtro `status = 'held' AND expires_at > now()` precisa aparecer em toda consulta de saldo (ADR-004), concentrado em um único método do ledger.
- Permissões `inventory:*` **não têm hierarquia embutida**: `inventory:write` não concede `inventory:read`. Não replicar o caso especial de `commercial-offers` em `PermissionRequirement.cs`.
- Os gates de curadoria partem de **allowlist explícita com default `blocked`** (ADR-002). Ausência nunca significa aprovação.
- O rate limiting do endpoint público é **entrega de infraestrutura em `localizestay-deploy`** (ADR-005). No backend, a única mudança é `.AllowAnonymous()` + `.DisableRateLimiting()` em `getAvailability`. `Program.cs`, `UseForwardedHeaders` e `RateLimitOptions` ficam fora do escopo.
- O `429` de `getAvailability` é produzido pela borda e **não é exercitável em teste** — o teste de contrato precisa registrá-lo como exceção conhecida, não falhar.
- A duração da retenção é parâmetro global fixo de quinze minutos, derivado no servidor, nunca recebido do cliente.
- Ratificar os payloads de `Curation.Contracts` e `Booking.Contracts` não bloqueia: declarar `V1` com payload mínimo e seguir. Apenas `reservationIntentId` e `reservationId` devem ser travados com D03 antes da Onda B, e ambos já constam do contrato HTTP público.
- Onda B não é exposta a viajantes reais antes da validação ponta a ponta com `D03-C01` (tarefa 50.0).

## Fases de Implementação

### Fase 1 — Fundações transversais (tarefas 1.0 a 4.0)

Habilita metadados de erro estruturados, as cinco permissões, a janela de atendimento seg–sáb 08h–20h e os contratos de evento de curadoria. Nenhuma depende de outra; todas podem começar imediatamente.

### Fase 2 — Domínio do inventário (tarefas 5.0 a 11.0)

Modela `DailyInventory`, `Allotment`, `InventoryBlock`, `PropertySellability`, `InventoryRequest` e a idempotência, fechando no `InventoryLedger` — o único ponto de mutação de saldo.

### Fase 3 — Persistência da Onda A (tarefas 12.0 a 14.0)

Mapeamentos EF, migration com índice de exclusão de sobreposição e os cinco `DbSet` da Onda A.

### Fase 4 — Aplicação da Onda A (tarefas 15.0 a 26.0)

DTOs, regras de validação compartilhadas, recálculo de gates e as fatias de aplicação de allotment, bloqueio, fila, disponibilidade, calendário e curadoria.

### Fase 5 — API e métricas da Onda A (tarefas 27.0 a 31.0)

As 19 operações Minimal API da Onda A, fragmentadas por recurso em arquivos disjuntos, e os sete indicadores do PRD.

### Fase 6 — Certificação da Onda A (tarefas 32.0 a 36.0)

Telemetria, contrato, segurança, outbox/auditoria e a prova de que os contadores materializados não divergem.

### Fase 7 — Domínio e persistência da Onda B (tarefas 37.0 a 40.0)

`InventoryHold`, `InventoryCommitment`, as operações de retenção no ledger e a migration incremental.

### Fase 8 — Aplicação e API da Onda B (tarefas 41.0 a 46.0)

Varredura de expiração com guarda de leitura, commands de retenção, as quatro operações restantes e os três consumidores de reserva.

### Fase 9 — Certificação da Onda B e operação (tarefas 47.0 a 51.0)

Ciclo de vida, concorrência pela última unidade, contrato e segurança da Onda B, fluxo ponta a ponta e runbook.

## Tarefas

### Fase 1 — Fundações transversais

- [ ] [1.0 Propagar metadados estruturados de erro para Problem Details](1_task.md)
- [ ] [2.0 Declarar as cinco permissões `inventory:*` e suas policies](2_task.md)
- [ ] [3.0 Implementar a janela de atendimento do inventário (seg–sáb 08h–20h)](3_task.md)
- [ ] [4.0 Declarar os contratos de eventos de curadoria](4_task.md)

### Fase 2 — Domínio do inventário

- [ ] [5.0 Modelar `DailyInventory` com saldo derivado e piso zero](5_task.md)
- [ ] [6.0 Modelar `Allotment` com período, revisão e piso comercial](6_task.md)
- [ ] [7.0 Modelar `InventoryBlock` com tipo, origem e remoção com histórico](7_task.md)
- [ ] [8.0 Modelar `PropertySellability` e os cinco gates de RN-07](8_task.md)
- [ ] [9.0 Modelar `InventoryRequest` com prioridade e SLA derivado](9_task.md)
- [ ] [10.0 Modelar a idempotência de escrita do controle de inventário](10_task.md)
- [ ] [11.0 Implementar o `InventoryLedger` com bloqueio pessimista ordenado](11_task.md)

### Fase 3 — Persistência da Onda A

- [ ] [12.0 Mapear saldo, allotment e bloqueio no EF Core](12_task.md)
- [ ] [13.0 Mapear solicitação e vendabilidade no EF Core](13_task.md)
- [ ] [14.0 Criar a migration `AddInventoryControl` e os `DbSet` da Onda A](14_task.md)

### Fase 4 — Aplicação da Onda A

- [ ] [15.0 Definir os DTOs internos e o mapeamento manual da Onda A](15_task.md)
- [ ] [16.0 Definir as regras de validação compartilhadas do inventário](16_task.md)
- [ ] [17.0 Recalcular os gates de vendabilidade e ativar a allowlist de curadoria](17_task.md)
- [ ] [18.0 Ceder, alterar, cancelar e consultar allotment](18_task.md)
- [ ] [19.0 Aplicar bloqueio planejado e emergencial](19_task.md)
- [ ] [20.0 Remover bloqueio e devolver a capacidade](20_task.md)
- [ ] [21.0 Consultar bloqueios e simular impacto](21_task.md)
- [ ] [22.0 Registrar, atualizar e ordenar a fila de solicitações](22_task.md)
- [ ] [23.0 Consultar disponibilidade pública e diagnosticar vendabilidade](23_task.md)
- [ ] [24.0 Consultar o calendário de inventário e o detalhe da data](24_task.md)
- [ ] [25.0 Interromper a venda da propriedade por suspensão de curadoria](25_task.md)
- [ ] [26.0 Restabelecer os gates por aprovação de propriedade e de conteúdo](26_task.md)

### Fase 5 — API e métricas da Onda A

- [ ] [27.0 Expor os endpoints de disponibilidade, vendabilidade e calendário](27_task.md)
- [ ] [28.0 Expor os cinco endpoints de allotment](28_task.md)
- [ ] [29.0 Expor os cinco endpoints de bloqueio](29_task.md)
- [ ] [30.0 Expor os quatro endpoints da fila de solicitações](30_task.md)
- [ ] [31.0 Apurar e expor as métricas de inventário](31_task.md)

### Fase 6 — Certificação da Onda A

- [ ] [32.0 Instrumentar a telemetria da Onda A](32_task.md)
- [ ] [33.0 Certificar o contrato das 19 operações da Onda A](33_task.md)
- [ ] [34.0 Certificar permissões, endpoint anônimo e não vazamento do saldo](34_task.md)
- [ ] [35.0 Certificar outbox, auditoria e atomicidade da mutação de saldo](35_task.md)
- [ ] [36.0 Certificar a reconciliação do ledger e a ausência de deadlock](36_task.md)

### Fase 7 — Domínio e persistência da Onda B

- [ ] [37.0 Modelar `InventoryHold` com prazo e cinco estados terminais](37_task.md)
- [ ] [38.0 Modelar `InventoryCommitment` e mapear a retenção no EF Core](38_task.md)
- [ ] [39.0 Estender o `InventoryLedger` com reter, liberar e comprometer](39_task.md)
- [ ] [40.0 Criar a migration incremental das tabelas de retenção](40_task.md)

### Fase 8 — Aplicação e API da Onda B

- [ ] [41.0 Expirar retenções por varredura com guarda na leitura do saldo](41_task.md)
- [ ] [42.0 Reter, liberar, comprometer e consultar retenção](42_task.md)
- [ ] [43.0 Expor os quatro endpoints de retenção](43_task.md)
- [ ] [44.0 Declarar os contratos de eventos de reserva](44_task.md)
- [ ] [45.0 Consumir intenção iniciada e reserva não concluída](45_task.md)
- [ ] [46.0 Consumir reserva confirmada](46_task.md)

### Fase 9 — Certificação da Onda B e operação

- [ ] [47.0 Certificar o ciclo de vida completo da retenção](47_task.md)
- [ ] [48.0 Certificar a concorrência pela última unidade](48_task.md)
- [ ] [49.0 Certificar o contrato e a segurança da Onda B](49_task.md)
- [ ] [50.0 Certificar o fluxo ponta a ponta da F03](50_task.md)
- [ ] [51.0 Documentar o runbook e o README do controle de inventário](51_task.md)

## Catálogo de User Stories

| ID | User Story |
|---|---|
| US-01 | Como operador, quero registrar o allotment contratado para que a acomodação passe a ter saldo vendável. |
| US-02 | Como operador, quero enxergar num calendário o que foi cedido, comprometido, retido, bloqueado e o que restou, para diagnosticar uma data sem alternar telas. |
| US-03 | Como operador, quero bloquear datas imediatamente ao receber um aviso de indisponibilidade, para não vender o que o parceiro não pode honrar. |
| US-04 | Como viajante, quero que a acomodação escolhida fique separada enquanto concluo o checkout, para não perder a unidade durante o pagamento. |
| US-05 | Como parceiro, quero solicitar allotment e bloqueios pelos canais que já uso. |
| US-06 | Como gestor, quero medir vendas sem lastro e prazo de processamento para decidir sobre a exposição do piloto. |

## Rastreabilidade US → Tasks

| User Story | Tasks Relacionadas | Tipo de Cobertura |
|---|---|---|
| US-01 | 6.0, 11.0, 12.0, 14.0, 18.0, 28.0, 33.0 | Direta |
| US-02 | 5.0, 15.0, 21.0, 24.0, 27.0, 33.0 | Direta |
| US-03 | 7.0, 11.0, 19.0, 20.0, 21.0, 25.0, 29.0, 32.0 | Direta |
| US-04 | 37.0, 39.0, 41.0, 42.0, 43.0, 45.0, 46.0, 47.0, 48.0, 50.0 | Direta |
| US-05 | 3.0, 9.0, 22.0, 30.0 | Suporte; nenhum canal humano altera o inventário automaticamente |
| US-06 | 17.0, 31.0, 32.0, 35.0, 51.0 | Direta |

## Cobertura das Operações do Contrato

| Operação | Onda | Aplicação | Endpoint | Certificação |
|---|:--:|---|---|---|
| `getAvailability` | A | 23.0 | 27.0 | 33.0, 34.0 |
| `getPropertySellability` | A | 17.0, 23.0 | 27.0 | 33.0 |
| `getInventoryCalendar` | A | 24.0 | 27.0 | 33.0 |
| `getDailyInventoryDetail` | A | 24.0 | 27.0 | 33.0 |
| `listAllotments` | A | 18.0 | 28.0 | 33.0 |
| `createAllotment` | A | 18.0 | 28.0 | 33.0, 36.0 |
| `getAllotment` | A | 18.0 | 28.0 | 33.0 |
| `updateAllotment` | A | 18.0 | 28.0 | 33.0 |
| `cancelAllotment` | A | 18.0 | 28.0 | 33.0 |
| `listInventoryBlocks` | A | 21.0 | 29.0 | 33.0 |
| `createInventoryBlock` | A | 19.0 | 29.0 | 33.0, 35.0 |
| `previewInventoryBlockImpact` | A | 21.0 | 29.0 | 33.0 |
| `getInventoryBlock` | A | 21.0 | 29.0 | 33.0 |
| `removeInventoryBlock` | A | 20.0 | 29.0 | 33.0 |
| `listInventoryRequests` | A | 22.0 | 30.0 | 33.0 |
| `createInventoryRequest` | A | 22.0 | 30.0 | 33.0 |
| `getInventoryRequest` | A | 22.0 | 30.0 | 33.0 |
| `updateInventoryRequest` | A | 22.0 | 30.0 | 33.0 |
| `getInventoryMetrics` | A | 31.0 | 31.0 | 33.0 |
| `createInventoryHold` | B | 42.0 | 43.0 | 47.0, 48.0, 49.0 |
| `getInventoryHold` | B | 42.0 | 43.0 | 49.0 |
| `releaseInventoryHold` | B | 42.0 | 43.0 | 47.0, 49.0 |
| `commitInventoryHold` | B | 42.0 | 43.0 | 47.0, 49.0 |

## Cobertura dos Eventos de Domínio

| Evento | Direção | Onda | Task |
|---|---|:--:|---|
| `oferta-inventario.inventario-bloqueado` | Produz | A | 19.0, 25.0 |
| `oferta-inventario.bloqueio-afeta-reserva` | Produz | A | 19.0, 25.0 |
| `oferta-inventario.inventario-liberado` | Produz | A/B | 20.0, 41.0, 42.0 |
| `oferta-inventario.inventario-retido` | Produz | B | 42.0 |
| `oferta-inventario.retencao-expirada` | Produz | B | 41.0 |
| `oferta-inventario.inventario-comprometido` | Produz | B | 42.0 |
| `curadoria-qualidade.propriedade-suspensa` | Consome | A | 4.0, 25.0 |
| `curadoria-qualidade.propriedade-aprovada` | Consome | A | 4.0, 26.0 |
| `curadoria-qualidade.conteudo-aprovado` | Consome | A | 4.0, 26.0 |
| `reserva.intencao-iniciada` | Consome | B | 44.0, 45.0 |
| `reserva.nao-concluida` | Consome | B | 44.0, 45.0 |
| `reserva.confirmada` | Consome | B | 44.0, 46.0 |

## Validação de Cobertura

### A) Requisitos Funcionais

| Requisito | Task(s) | Status |
|---|---|---|
| RF-01 — Ceder allotment por acomodação e período | 6.0, 11.0, 12.0, 14.0, 18.0, 28.0, 33.0, 36.0 | ✅ Coberto |
| RF-02 — Aplicar e remover bloqueios | 7.0, 11.0, 19.0, 20.0, 21.0, 29.0, 32.0, 35.0 | ✅ Coberto |
| RF-03 — Calcular e consultar o saldo vendável | 5.0, 11.0, 17.0, 23.0, 27.0, 34.0 | ✅ Coberto |
| RF-04 — Operar o calendário de inventário | 3.0, 9.0, 22.0, 24.0, 27.0, 30.0 | ✅ Coberto |
| RF-05 — Interromper vendas por decisão de curadoria | 4.0, 8.0, 17.0, 25.0, 26.0 | ✅ Coberto |
| RF-06 — Reter inventário no início do checkout | 37.0, 39.0, 42.0, 43.0, 45.0, 48.0 | ✅ Coberto |
| RF-07 — Expirar e liberar retenções | 37.0, 39.0, 41.0, 42.0, 45.0, 47.0 | ✅ Coberto |
| RF-08 — Comprometer inventário na confirmação | 38.0, 39.0, 42.0, 46.0, 47.0 | ✅ Coberto |

### B) Artefatos da TechSpec

**Arquivos a criar**

| Artefato | Task | Status |
|---|---|---|
| `.tasks/prd-controlar-.../api-contract.yaml` | — | ✅ Já presente no repositório; nenhuma cópia adicional é necessária |
| `Domain/DailyInventories/DailyInventory.cs` | 5.0 | ✅ |
| `Domain/DailyInventories/InventoryLedger.cs` | 11.0, 39.0 | ✅ |
| `Domain/DailyInventories/InventoryLedgerResults.cs` | 11.0, 39.0 | ✅ |
| `Domain/Allotments/Allotment.cs` | 6.0 | ✅ |
| `Domain/Allotments/AllotmentValues.cs` | 6.0 | ✅ |
| `Domain/InventoryBlocks/InventoryBlock.cs` | 7.0 | ✅ |
| `Domain/InventoryBlocks/InventoryBlockValues.cs` | 7.0 | ✅ |
| `Domain/InventoryHolds/InventoryHold.cs` | 37.0 | ✅ |
| `Domain/InventoryHolds/InventoryCommitment.cs` | 38.0 | ✅ |
| `Domain/InventoryHolds/InventoryHoldValues.cs` | 37.0 | ✅ |
| `Domain/InventoryRequests/InventoryRequest.cs` | 9.0 | ✅ |
| `Domain/InventoryRequests/InventoryRequestValues.cs` | 9.0 | ✅ |
| `Domain/Sellability/PropertySellability.cs` | 8.0 | ✅ |
| `Domain/Sellability/SellabilityGate.cs` | 8.0 | ✅ |
| `Domain/InventoryIdempotencyKey.cs` | 10.0 | ✅ |
| `Application/Timing/IInventoryServiceWindow.cs` | 3.0 | ✅ |
| `Application/Availability/AvailabilityQueries.cs` | 23.0 | ✅ |
| `Application/Availability/InventoryCalendarQueries.cs` | 24.0 | ✅ |
| `Application/Allotments/AllotmentCommands.cs` | 18.0 | ✅ |
| `Application/Allotments/AllotmentQueries.cs` | 18.0 | ✅ |
| `Application/InventoryBlocks/InventoryBlockCommands.cs` | 19.0, 20.0 | ✅ |
| `Application/InventoryBlocks/InventoryBlockQueries.cs` | 21.0 | ✅ |
| `Application/InventoryRequests/InventoryRequestCommands.cs` | 22.0 | ✅ |
| `Application/InventoryRequests/InventoryRequestQueries.cs` | 22.0 | ✅ |
| `Application/InventoryHolds/InventoryHoldCommands.cs` | 42.0 | ✅ |
| `Application/InventoryHolds/InventoryHoldQueries.cs` | 42.0 | ✅ |
| `Application/InventoryHolds/InventoryHoldExpirationService.cs` | 41.0 | ✅ |
| `Application/Metrics/InventoryMetricsQueries.cs` | 31.0 | ✅ |
| `Application/Sellability/CurationPropertyApprovedHandler.cs` | 26.0 | ✅ |
| `Application/Sellability/CurationPropertySuspendedHandler.cs` | 25.0 | ✅ |
| `Application/Sellability/CurationContentApprovedHandler.cs` | 26.0 | ✅ |
| `Application/Sellability/SellabilityRecalculator.cs` | 17.0 | ✅ |
| `Application/Reservations/ReservationIntentStartedHandler.cs` | 45.0 | ✅ |
| `Application/Reservations/ReservationConfirmedHandler.cs` | 46.0 | ✅ |
| `Application/Reservations/ReservationNotCompletedHandler.cs` | 45.0 | ✅ |
| `Application/Inventory/InventoryDtos.cs` | 15.0, 42.0 | ✅ |
| `Application/Inventory/InventoryMapper.cs` | 15.0, 42.0 | ✅ |
| `Application/Inventory/InventoryValidators.cs` | 16.0, 42.0 | ⚠️ Coberto com desvio declarado (ver abaixo) |
| `Infrastructure/Timing/ConfiguredInventoryServiceWindow.cs` | 3.0 | ✅ |
| `Infrastructure/Configurations/DailyInventoryConfiguration.cs` | 12.0 | ✅ |
| `Infrastructure/Configurations/AllotmentConfiguration.cs` | 12.0 | ✅ |
| `Infrastructure/Configurations/InventoryBlockConfiguration.cs` | 12.0 | ✅ |
| `Infrastructure/Configurations/InventoryHoldConfiguration.cs` | 38.0 | ✅ |
| `Infrastructure/Configurations/InventoryCommitmentConfiguration.cs` | 38.0 | ✅ |
| `Infrastructure/Configurations/InventoryRequestConfiguration.cs` | 13.0 | ✅ |
| `Infrastructure/Configurations/PropertySellabilityConfiguration.cs` | 13.0 | ✅ |
| `Infrastructure/Configurations/InventoryIdempotencyKeyConfiguration.cs` | 10.0 | ✅ |
| `Infrastructure/Migrations/[ts]_AddInventoryControl.cs` (+ Designer) | 14.0 | ✅ |
| `Endpoints/AvailabilityEndpoints.cs` | 27.0 | ✅ |
| `Endpoints/InventoryCalendarEndpoints.cs` | 27.0 | ✅ |
| `Endpoints/AllotmentEndpoints.cs` | 28.0 | ✅ |
| `Endpoints/InventoryBlockEndpoints.cs` | 29.0 | ✅ |
| `Endpoints/InventoryRequestEndpoints.cs` | 30.0 | ✅ |
| `Endpoints/InventoryHoldEndpoints.cs` | 43.0 | ✅ |
| `Endpoints/InventoryMetricsEndpoints.cs` | 31.0 | ✅ |
| `Curation.Contracts/CurationSellabilityEvents.cs` | 4.0 | ✅ |
| `Booking.Contracts/BookingIntegrationEvents.cs` | 44.0 | ✅ |
| `UnitTests/Inventory/DailyInventoryTests.cs` | 5.0 | ✅ |
| `UnitTests/Inventory/InventoryLedgerTests.cs` | 11.0 | ✅ |
| `UnitTests/Inventory/AllotmentTests.cs` | 6.0 | ✅ |
| `UnitTests/Inventory/InventoryBlockTests.cs` | 7.0 | ✅ |
| `UnitTests/Inventory/InventoryHoldTests.cs` | 37.0 | ✅ |
| `UnitTests/Inventory/InventoryRequestTests.cs` | 9.0 | ✅ |
| `UnitTests/Inventory/InventoryServiceWindowTests.cs` | 3.0 | ✅ |
| `UnitTests/Inventory/PropertySellabilityTests.cs` | 8.0 | ✅ |
| `UnitTests/Inventory/InventoryMetricsQueryHandlerTests.cs` | 31.0 | ✅ |
| `UnitTests/Inventory/CurationSellabilityHandlerTests.cs` | 26.0 | ✅ |
| `IntegrationTests/Inventory/InventoryContractTests.cs` | 33.0, 49.0 | ✅ |
| `IntegrationTests/Inventory/InventoryPersistenceTests.cs` | 14.0 | ⚠️ Renomeado para `InventoryControlPersistenceTests.cs` — o nome original já existe desde a F01 |
| `IntegrationTests/Inventory/InventoryConcurrencyTests.cs` | 36.0, 48.0 | ✅ |
| `IntegrationTests/Inventory/InventoryLedgerReconciliationTests.cs` | 36.0, 48.0 | ✅ |
| `IntegrationTests/Inventory/AvailabilityEndpointsTests.cs` | 27.0 | ✅ |
| `IntegrationTests/Inventory/InventoryBlockEndpointsTests.cs` | 29.0 | ✅ |
| `IntegrationTests/Inventory/InventoryHoldLifecycleTests.cs` | 47.0 | ✅ |
| `IntegrationTests/Inventory/InventoryOutboxAndAuditTests.cs` | 35.0 | ✅ |
| `IntegrationTests/Inventory/InventorySecurityTests.cs` | 34.0, 49.0 | ✅ |
| `IntegrationTests/Inventory/InventoryEndToEndTests.cs` | 50.0 | ✅ |
| `docs/runbooks/inventory-control.md` | 51.0 | ✅ |

**Arquivos a modificar**

| Artefato | Task | Status |
|---|---|---|
| `InventoryModule.cs` | 3.0, 17.0, 41.0 | ✅ |
| `Infrastructure/InventoryDbContext.cs` | 14.0, 40.0 | ✅ |
| `Infrastructure/Migrations/InventoryDbContextModelSnapshot.cs` | 14.0, 40.0 | ✅ |
| `Endpoints/InventoryEndpoints.cs` | 27.0, 28.0, 29.0, 30.0, 31.0, 43.0 | ✅ |
| `Application/Observability/InventoryTelemetry.cs` | 32.0 | ✅ |
| `Application/CommercialOffers/CommercialRateCommands.cs` | 17.0 | ✅ |
| `LocalizeStay.Modules.Inventory.csproj` | 4.0, 44.0 | ✅ |
| `SharedKernel/Security/PermissionRequirement.cs` | 2.0 | ✅ |
| `SharedKernel/Security/SecurityServiceCollectionExtensions.cs` | 2.0 | ✅ |
| `SharedKernel/ErrorHandling/BusinessRuleViolationException.cs` | 1.0 | ✅ |
| `SharedKernel/ErrorHandling/GlobalExceptionHandler.cs` | 1.0 | ✅ |
| `LocalizeStay.Api/appsettings.json` | 3.0, 17.0, 41.0 | ✅ |
| `README.md` | 51.0 | ✅ |
| `localizestay-deploy/envs/*/localizestay.stack.yml` | — | ⚠️ Fora do escopo do backend (ADR-005); é entrega de infraestrutura e corre em paralelo |

**Desvios declarados em relação ao Inventário de Artefatos**

1. **`Application/Inventory/InventoryValidators.cs`** passa a conter apenas as **regras de validação compartilhadas** (intervalo de datas, teto de 92 dias, teto de 30 noites, `receivedAt` não futuro, `reasonNote` obrigatório em `reason: other`). Os validators específicos de cada Command/Query ficam **no próprio arquivo da fatia**. Motivo: um único arquivo de validators dependeria dos tipos de Command declarados em 12 tarefas diferentes, criando uma dependência circular e uma colisão de escrita entre fatias paralelas. O arquivo continua existindo e continua sendo o único lugar onde as regras transversais vivem.
2. **`IntegrationTests/Inventory/InventoryPersistenceTests.cs`** já existe no repositório desde a F01. O arquivo da F03 é `InventoryControlPersistenceTests.cs`.
3. **Arquivos de teste adicionais** foram criados além do inventário, porque o Orçamento de Fragmentação exige que cada tarefa seja provável isoladamente por um filtro de teste próprio: `BusinessRuleViolationMetadataTests`, `InventoryControlPermissionsTests`, `InventoryIdempotencyKeyTests`, `InventoryMapperTests`, `InventoryValidatorsTests`, `SellabilityRecalculatorTests`, `AllotmentCommandHandlerTests`, `InventoryBlockCommandHandlerTests`, `InventoryBlockRemovalHandlerTests`, `InventoryBlockImpactPreviewTests`, `InventoryRequestCommandHandlerTests`, `AvailabilityQueryHandlerTests`, `InventoryCalendarQueryHandlerTests`, `CurationPropertySuspendedHandlerTests`, `InventoryLedgerHoldTests`, `InventoryHoldExpirationServiceTests`, `InventoryHoldCommandHandlerTests`, `ReservationHoldHandlerTests`, `ReservationConfirmedHandlerTests`, `InventoryControlObservabilityTests`, `InventoryHoldPersistenceTests`, `InventoryHoldConcurrencyTests`, `AllotmentEndpointsTests`, `InventoryRequestEndpointsTests`, `InventoryHoldEndpointsTests`.

### C) Categorias Obrigatórias

| # | Categoria | Task(s) / N/A | Skill Relacionada | Status |
|---|---|---|---|---|
| 1 | Setup / Configuração | 3.0, 17.0, 41.0 (`appsettings`, options com `ValidateOnStart`, hosted service); 14.0 e 40.0 (migrations) | `dotnet-dependency-config` | ✅ |
| 2 | Modelos de Dados | 5.0 a 10.0, 12.0 a 14.0, 37.0, 38.0, 40.0 | `dotnet-architecture` | ✅ |
| 3 | Lógica de Negócio | 11.0, 17.0 a 26.0, 39.0, 41.0, 42.0, 45.0, 46.0 | `dotnet-architecture` | ✅ |
| 4 | Endpoints / Interfaces | 27.0 a 31.0, 43.0; certificação em 33.0 e 49.0 | `restful-api` | ✅ |
| 5 | Integrações Externas | 4.0 e 44.0 (contratos de evento), 25.0, 26.0, 45.0, 46.0 (consumidores), 19.0/41.0/42.0 (outbox). WhatsApp e e-mail permanecem canais humanos e **não** recebem integração automática — decisão explícita do PRD | `dotnet-dependency-config` | ✅ |
| 6 | Validações e Erros | 1.0 (metadata em Problem Details), 16.0 (regras compartilhadas), validators por fatia em 18.0 a 24.0 e 42.0 | `dotnet-code-quality` | ✅ |
| 7 | Testes | Subtarefa de teste em **todas** as 51 tarefas; certificação dedicada em 33.0 a 36.0 e 47.0 a 50.0 | `dotnet-testing` | ✅ |
| 8 | Observabilidade | 32.0 (métricas, spans e logs), 41.0 (backlog de expiração), 51.0 (runbook e alertas) | `dotnet-observability` | ✅ |
| 9 | Documentação | 51.0 (runbook + README); contrato OpenAPI já versionado e imutável nesta feature | — | ✅ |
| 10 | Segurança | 2.0 (permissões e policies), 27.0 (`AllowAnonymous` + `DisableRateLimiting` em `getAvailability`), 34.0 e 49.0 (certificação 401/403 e não vazamento da composição do saldo). Rate limit de borda: fora do escopo do backend por ADR-005 | `dotnet-production-readiness` | ✅ |

### D) Conformidade com o Orçamento de Fragmentação

Tier alvo: **`budget`** (padrão) — criar ≤ 3, modificar ≤ 3, subtarefas ≤ 4, exatamente 1 fatia vertical.

| Task | Criar | Modificar | Subtarefas | Fatias | complexity | Status |
|---|---:|---:|---:|---:|---|---|
| 1.0 | 1 | 2 | 3 | 1 | medium | ✅ |
| 2.0 | 1 | 2 | 3 | 1 | low | ✅ |
| 3.0 | 3 | 2 | 4 | 1 | medium | ✅ |
| 4.0 | 1 | 2 | 3 | 1 | low | ✅ |
| 5.0 | 2 | 0 | 4 | 1 | medium | ✅ |
| 6.0 | 3 | 0 | 4 | 1 | medium | ✅ |
| 7.0 | 3 | 0 | 4 | 1 | medium | ✅ |
| 8.0 | 3 | 0 | 4 | 1 | medium | ✅ |
| 9.0 | 3 | 0 | 4 | 1 | medium | ✅ |
| 10.0 | 3 | 0 | 3 | 1 | low | ✅ |
| 11.0 | 3 | 0 | 4 | 1 | high | ✅ |
| 12.0 | 3 | 0 | 4 | 1 | medium | ✅ |
| 13.0 | 2 | 0 | 3 | 1 | medium | ✅ |
| 14.0 | 3 | 2 | 4 | 1 | high | ✅ |
| 15.0 | 3 | 0 | 4 | 1 | medium | ✅ |
| 16.0 | 2 | 0 | 3 | 1 | medium | ✅ |
| 17.0 | 2 | 3 | 4 | 1 | medium | ✅ |
| 18.0 | 3 | 0 | 4 | 1 | high | ✅ |
| 19.0 | 2 | 0 | 4 | 1 | high | ✅ |
| 20.0 | 1 | 1 | 3 | 1 | medium | ✅ |
| 21.0 | 2 | 0 | 4 | 1 | medium | ✅ |
| 22.0 | 3 | 0 | 4 | 1 | medium | ✅ |
| 23.0 | 2 | 0 | 4 | 1 | medium | ✅ |
| 24.0 | 2 | 0 | 4 | 1 | medium | ✅ |
| 25.0 | 2 | 0 | 4 | 1 | medium | ✅ |
| 26.0 | 3 | 0 | 4 | 1 | medium | ✅ |
| 27.0 | 3 | 1 | 4 | 1 | medium | ✅ |
| 28.0 | 2 | 1 | 4 | 1 | medium | ✅ |
| 29.0 | 2 | 1 | 4 | 1 | medium | ✅ |
| 30.0 | 2 | 1 | 4 | 1 | medium | ✅ |
| 31.0 | 3 | 1 | 4 | 1 | medium | ✅ |
| 32.0 | 1 | 3 | 4 | 1 | medium | ✅ |
| 33.0 | 1 | 1 | 4 | 1 | medium | ✅ |
| 34.0 | 1 | 0 | 4 | 1 | medium | ✅ |
| 35.0 | 1 | 0 | 4 | 1 | medium | ✅ |
| 36.0 | 2 | 0 | 4 | 1 | high | ✅ |
| 37.0 | 3 | 0 | 4 | 1 | medium | ✅ |
| 38.0 | 3 | 0 | 4 | 1 | medium | ✅ |
| 39.0 | 1 | 2 | 4 | 1 | high | ✅ |
| 40.0 | 3 | 2 | 4 | 1 | high | ✅ |
| 41.0 | 2 | 2 | 4 | 1 | high | ✅ |
| 42.0 | 3 | 3 | 4 | 1 | high | ✅ |
| 43.0 | 2 | 1 | 4 | 1 | medium | ✅ |
| 44.0 | 1 | 2 | 3 | 1 | low | ✅ |
| 45.0 | 3 | 0 | 4 | 1 | medium | ✅ |
| 46.0 | 2 | 0 | 4 | 1 | medium | ✅ |
| 47.0 | 1 | 0 | 4 | 1 | medium | ✅ |
| 48.0 | 1 | 1 | 4 | 1 | high | ✅ |
| 49.0 | 0 | 2 | 3 | 1 | medium | ✅ |
| 50.0 | 1 | 0 | 4 | 1 | medium | ✅ |
| 51.0 | 1 | 1 | 4 | 1 | low | ✅ |

**Nenhuma linha estourou o orçamento.**

**Distribuição de `complexity`:**

| Valor | Quantidade | Percentual |
|---|---:|---:|
| `low` | 5 | 9,8% |
| `medium` | 36 | 70,6% |
| `high` | 10 | 19,6% |

As dez tarefas `high` correspondem a acoplamento irredutível e **exigem revisão humana do plano antes de implementar**:

| Task | Por que é acoplamento irredutível |
|---|---|
| 11.0 | O `InventoryLedger` é a invariante transversal de RN-03: carregar com `FOR UPDATE` ordenado, validar e aplicar delta precisam viver na mesma unidade, sob pena de reintroduzir a corrida que a feature existe para eliminar |
| 14.0 | Migration com chave composta, índice de exclusão PostgreSQL sobre `daterange` e cinco tabelas correlacionadas — não é fatiável sem deixar o schema inconsistente entre passos |
| 18.0 | A recusa de redução abaixo do comprometido exige avaliar todas as datas do período dentro da mesma transação que altera o `Allotment` e rematerializa `daily_inventory` |
| 19.0 | Bloqueio emergencial precisa cortar vendas, invalidar retenções e gravar dois eventos na mesma transação, com o carimbo `salesStoppedAt` que sustenta a métrica de um minuto |
| 36.0 | A reconstrução dos contadores a partir das fontes é o único teste que prova a decisão de persistir saldo derivado; falhar em fatiá-lo é preferível a fatiá-lo mal |
| 39.0 | `TryHold`, `Release` e `Commit` compartilham a mesma sequência de lock e a mesma guarda de retenção vencida; separá-los duplicaria a regra em três lugares |
| 40.0 | Mesma natureza de 14.0, com o agravante de a migration incremental ter de conviver com dados da Onda A já em produção |
| 41.0 | A varredura e a guarda de leitura são as duas metades de uma decisão só (ADR-004); entregar uma sem a outra produz janela morta de disponibilidade ou evento não publicado |
| 42.0 | Os três commands compartilham idempotência, transição de estado e outbox; o comprometimento de retenção expirada exige revalidação de saldo no mesmo caminho |
| 48.0 | Teste de concorrência real com Testcontainers: duas intenções simultâneas pela última unidade só é significativo como um único cenário controlado |

## Análise de Paralelização

### Verificação de duplicação de arquitetura

Nenhuma. Toda mutação de saldo converge no `InventoryLedger`; toda leitura de saldo usa projeções sobre `daily_inventory`; nenhum repositório genérico novo é criado; a janela de atendimento da F03 é abstração própria e **não** altera o `IBusinessCalendar` da F01/F02 (ADR-003); a idempotência replica o padrão de `CommercialOfferIdempotencyKey` sem generalizá-lo prematuramente.

### Análise de componentes faltantes

Nenhum GAP após a validação cruzada. Dois pontos ficam explicitamente fora do backend e precisam de acompanhamento: o middleware de rate limit em `localizestay-deploy` (ADR-005) e a existência de publicadores reais para os eventos de D06 e D03 — ambos não bloqueiam, porque os consumidores são escritos contra contratos `V1` versionados e os gates de curadoria partem de allowlist configurada.

### Pontos de integração validados

`inventory.accommodations` e `inventory.commercial_rates` (F02, mesmo schema), canal operacional da F01, três eventos de curadoria, três eventos de reserva, outbox transacional existente, `BusinessAuditWriter<InventoryDbContext>` e JWT LogTo com escopo `staff`. Nenhuma FK ou join atravessa módulos.

### Lanes de Execução Paralela

| Lane | Tarefas | Descrição |
|---|---|---|
| Lane A — Plataforma | 1.0, 2.0, 4.0, 44.0 | Erros, permissões e contratos de evento; sem dependência de domínio |
| Lane B — Tempo e fila | 3.0 → 9.0 → 22.0 → 30.0 | Janela de atendimento, `InventoryRequest` e a fila com SLA |
| Lane C — Saldo | 5.0/6.0/7.0 → 11.0 → 12.0 → 14.0 | Núcleo de `daily_inventory` e o ledger |
| Lane D — Vendabilidade | 8.0 → 13.0 → 17.0 → 25.0/26.0 | Gates de RN-07 e consumidores de curadoria |
| Lane E — Aplicação A | 15.0/16.0 → 18.0/19.0/21.0/23.0/24.0 → 27.0–31.0 | Fatias de aplicação e endpoints em arquivos disjuntos |
| Lane F — Certificação A | 32.0, 33.0, 34.0, 35.0, 36.0 | Cinco frentes independentes após os endpoints |
| Lane G — Retenção | 37.0 → 38.0/39.0 → 40.0 → 41.0/42.0 → 43.0 | Onda B, iniciável em paralelo à Fase 6 |
| Lane H — Certificação B | 47.0, 48.0, 49.0, 50.0, 51.0 | Fecha a Onda B |

As tarefas 5.0, 6.0, 7.0, 8.0 e 10.0 são domínio puro sem dependências entre si — cinco frentes paralelas. As tarefas 18.0, 19.0, 21.0, 22.0, 23.0 e 24.0 tocam arquivos disjuntos e podem correr juntas. As tarefas 27.0 a 31.0 criam arquivos de endpoint disjuntos; cada uma acrescenta **uma única linha** de registro em `InventoryEndpoints.cs`, edição trivialmente mesclável que não justifica serialização.

### Caminho Crítico

```
5.0/6.0/7.0 → 11.0 → 12.0 → 14.0 → 18.0 → 28.0 → 33.0
                              ↓
                    37.0 → 39.0 → 40.0 → 41.0 → 42.0 → 43.0 → 47.0 → 50.0 → 51.0
```

A Onda B pode começar em 37.0 assim que 5.0 estiver pronta, mas só passa por 39.0 depois de 11.0 e por 40.0 depois de 14.0. A Fase 6 (certificação da Onda A) corre em paralelo à Fase 7.

### Diagrama de Dependências

```text
Fase 1   1.0   2.0   3.0   4.0        (sem dependências, todas paralelas)
          │     │     │     │
          │     │     └─────┼──> 9.0 ──┐
          │     │           │           │
Fase 2   5.0  6.0  7.0  8.0  10.0      │
          └────┴────┴──> 11.0           │
                          │             │
Fase 3   12.0 <───────────┘   13.0 <────┘
          └──────> 14.0 <──────┘  (+ 10.0)
                    │
Fase 4   15.0  16.0  17.0 <─ 8.0
           └─────┴─────┴──> 18.0  19.0 ──> 20.0   21.0   22.0   23.0   24.0
                                    │                              ▲
                            4.0 ──> 25.0 ──> 26.0 ─────────────────┘
                                    │
Fase 5   27.0   28.0   29.0   30.0   31.0     (arquivos de endpoint disjuntos)
           └──────┴──────┴──────┴──────┘
                          │
Fase 6   32.0   33.0   34.0   35.0   36.0

Fase 7   37.0 ──> 38.0 ──> 40.0        11.0 ──> 39.0 ──┐
                    │                                    │
                    └────────────────────────────────────┴──> 41.0
Fase 8   41.0 ──> 42.0 ──> 43.0        44.0 ──> 45.0   46.0
Fase 9   47.0   48.0   49.0 ──> 50.0 ──> 51.0
```

## Comandos de Verificação Padrão

```bash
# Build
dotnet build ../localizestay-backend/LocalizeStay.sln --no-restore

# Formatação
dotnet format ../localizestay-backend/LocalizeStay.sln --verify-no-changes --no-restore

# Testes unitários de uma tarefa
dotnet test ../localizestay-backend/LocalizeStay.sln --filter "FullyQualifiedName~<NomeDaClasseDeTeste>"

# Testes de integração (exigem Docker para Testcontainers PostgreSQL)
dotnet test ../localizestay-backend/tests/LocalizeStay.IntegrationTests --filter "FullyQualifiedName~<NomeDaClasseDeTeste>"

# Testes de arquitetura (fronteiras de módulo e encapsulamento)
dotnet test ../localizestay-backend/tests/LocalizeStay.ArchitectureTests
```
