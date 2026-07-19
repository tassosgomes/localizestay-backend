# Resumo de Tarefas de Implementação de Incorporar Parceiros e Propriedades

## Visão Geral

Este plano implementa a F01 como capacidade vertical do módulo `Inventory` do backend LocalizeStay. O trabalho cobre as 18 operações do contrato OpenAPI, regras de incorporação e duplicidade, persistência PostgreSQL no schema `inventory`, autenticação/autorização LogTo, auditoria, outbox, métricas, observabilidade e testes automatizados.

As tarefas foram divididas em quatro fases porque o incremento possui mais de dez tarefas principais e combina fundação transversal, domínio, API e certificação. As questões externas de LogTo, calendário, elegibilidade e referência documental são tratadas como critérios de entrada explícitos nas tarefas 1.0 e 5.0.

## Skills de Stack Consultadas

| Skill | Caminho | Influência |
|---|---|---|
| `dotnet-architecture` | `/home/tsgomes/.agents/skills/csharp/dotnet-architecture/SKILL.md` | Módulo vertical, CQRS nativo, Minimal APIs, DI, exceções e `AsNoTracking` |
| `dotnet-dependency-config` | `/home/tsgomes/.agents/skills/csharp/dotnet-dependency-config/SKILL.md` | EF Core/PostgreSQL, migrations, FluentValidation, options e outbox |
| `dotnet-code-quality` | `/home/tsgomes/.agents/skills/csharp/dotnet-code-quality/SKILL.md` | Código em inglês, naming C#, limites de métodos/classes, DI e `CancellationToken` |
| `dotnet-testing` | `/home/tsgomes/.agents/skills/csharp/dotnet-testing/SKILL.md` | xUnit, AwesomeAssertions, Moq, AAA, WebApplicationFactory e PostgreSQL Testcontainers |
| `dotnet-observability` | `/home/tsgomes/.agents/skills/csharp/dotnet-observability/SKILL.md` | Health checks, logs correlacionados, métricas e spans |
| `dotnet-production-readiness` | `/home/tsgomes/.agents/skills/csharp/dotnet-production-readiness/SKILL.md` | Sanitização de PII, JWT, rate limiting, OTLP, configuração e checklist de deploy |

## Fases de Implementação

### Fase 1 — Fundação transversal

Estabelecer segurança, Problem Details, infraestrutura de teste, auditoria compartilhada e serviços configuráveis que desbloqueiam o módulo.

### Fase 2 — Domínio e persistência

Implementar agregados, invariantes, mapeamentos EF Core, índices e migration do schema `inventory`.

### Fase 3 — Casos de uso e API

Entregar as 18 operações HTTP em fatias por recurso/workflow, sempre com validação, autorização, auditoria e testes na mesma tarefa.

### Fase 4 — Métricas e certificação

Concluir consultas gerenciais, telemetria, validação automática do contrato, documentação operacional e gates de CI.

## Tarefas

- [x] 1.0 Preparar segurança, erros HTTP e infraestrutura de testes
- [x] 2.0 Implementar auditoria de negócio compartilhada
- [x] 3.0 Modelar agregados e invariantes de incorporação
- [x] 4.0 Persistir o domínio no schema Inventory e gerar migration
- [x] 5.0 Implementar elegibilidade upstream e calendário operacional
- [x] 6.0 Implementar cadastro e consulta de parceiros
- [x] 7.0 Implementar abertura, edição e consulta de incorporações
- [x] 8.0 Implementar gates, pendências, comunicações e revisão de duplicidade
- [x] 9.0 Implementar encaminhamento, devolução, encerramento e outbox
- [x] 10.0 Implementar histórico, métricas e observabilidade de negócio
- [ ] 11.0 Certificar contrato, documentação e prontidão de entrega

## Rastreabilidade US → Tasks

| User Story | Tasks Relacionadas | Tipo de Cobertura |
|---|---|---|
| US-01 — Cadastrar parceiro uma vez e acompanhar propriedades separadamente | 3.0, 4.0, 6.0, 7.0 | Direta |
| US-02 — Validar contrato, responsáveis e canal antes da Curadoria | 3.0, 8.0, 9.0 | Direta |
| US-03 — Visualizar pendências, responsáveis, estado e prazo | 7.0, 8.0, 10.0 | Direta |
| US-04 — Fornecer informações por WhatsApp/e-mail sem portal | 5.0, 8.0 | Direta |
| US-05 — Medir prazo, completude e devoluções | 5.0, 10.0, 11.0 | Direta |

## Validação de Cobertura

### Requisitos Funcionais

| Requisito | Task(s) | Status |
|---|---|---|
| RF-01 — Iniciar e estruturar a incorporação | 3.0, 5.0, 6.0, 7.0 | ✅ Coberto |
| RF-02 — Controlar gates e pendências de prontidão | 3.0, 5.0, 8.0, 10.0 | ✅ Coberto |
| RF-03 — Prevenir duplicidades | 3.0, 4.0, 6.0, 7.0, 8.0 | ✅ Coberto |
| RF-04 — Encaminhar e reabrir a incorporação | 3.0, 9.0, 10.0 | ✅ Coberto |
| RF-05 — Encerrar sem perder o histórico | 3.0, 9.0, 10.0 | ✅ Coberto |
| Métricas de sucesso do PRD | 5.0, 10.0, 11.0 | ✅ Coberto |

### Operações do API Contract

| Grupo de `operationId` | Task | Status |
|---|---|---|
| `listPartners`, `createPartner`, `getPartner`, `updatePartner` | 6.0 | ✅ |
| `listPropertyOnboardings`, `createPropertyOnboarding`, `getPropertyOnboarding`, `updatePropertyOnboarding` | 7.0 | ✅ |
| `updateReadinessGate`, `createPendingIssue`, `updatePendingIssue`, `createCommunicationRecord`, `createDuplicateReview` | 8.0 | ✅ |
| `submitPropertyOnboardingToCuration`, `createCurationReturn`, `closePropertyOnboarding` | 9.0 | ✅ |
| `listPropertyOnboardingHistory`, `getPropertyOnboardingMetrics` | 10.0 | ✅ |

### Artefatos da TechSpec

| Artefato | Task | Status |
|---|---|---|
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Domain/Partners/Partner.cs` | 3.0 | ✅ |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Domain/PropertyOnboardings/PropertyOnboarding.cs` | 3.0 | ✅ |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Domain/PropertyOnboardings/{ReadinessGate,PendingIssue,CommunicationRecord,DuplicateReview,CurationReturn}.cs` | 3.0 | ✅ |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/Partners/{PartnerCommands,PartnerQueries}.cs` | 6.0 | ✅ |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/PropertyOnboardings/{PropertyOnboardingCommands,ReadinessCommands,ReviewCommands,PropertyOnboardingQueries}.cs` | 7.0, 8.0, 9.0, 10.0 | ✅ |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/Validation/InventoryValidators.cs` | 6.0, 7.0, 8.0, 9.0 | ✅ |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/Upstream/{IPartnerPreselectionValidator,IDestinationEligibilityValidator}.cs` | 5.0 | ✅ |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Infrastructure/Upstream/ConfiguredEligibilityValidators.cs` | 5.0 | ✅ |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Infrastructure/Configurations/*.cs` | 4.0 | ✅ |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Infrastructure/Migrations/[timestamp]_AddPortfolioOnboarding.cs` | 4.0 | ✅ |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Endpoints/{PartnerEndpoints,PropertyOnboardingEndpoints,PropertyOnboardingSubresourceEndpoints,PropertyOnboardingReadEndpoints}.cs` | 6.0, 7.0, 8.0, 9.0, 10.0 | ✅ |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory.Contracts/InventoryIntegrationEvents.cs` | 9.0 | ✅ |
| `../localizestay-backend/src/BuildingBlocks/LocalizeStay.SharedKernel/Auditing/{BusinessAuditEntry,IBusinessAuditWriter,BusinessAuditWriter}.cs` | 2.0 | ✅ |
| `../localizestay-backend/src/BuildingBlocks/LocalizeStay.SharedKernel/Security/PermissionRequirement.cs` | 1.0 | ✅ |
| `../localizestay-backend/tests/LocalizeStay.UnitTests/Inventory/*.cs` | 2.0–10.0 | ✅ |
| `../localizestay-backend/tests/LocalizeStay.IntegrationTests/Infrastructure/LocalizeStayWebApplicationFactory.cs` | 1.0 | ✅ |
| `../localizestay-backend/tests/LocalizeStay.IntegrationTests/Inventory/*.cs` | 1.0, 4.0, 6.0–11.0 | ✅ |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/InventoryModule.cs` | 2.0, 5.0–10.0 | ✅ |
| `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Infrastructure/InventoryDbContext.cs` | 2.0, 4.0 | ✅ |
| `../localizestay-backend/src/LocalizeStay.Api/Program.cs` | 1.0 | ✅ |
| `../localizestay-backend/src/LocalizeStay.Api/appsettings.json` | 1.0, 5.0 | ✅ |
| `../localizestay-backend/src/BuildingBlocks/LocalizeStay.SharedKernel/ErrorHandling/GlobalExceptionHandler.cs` | 1.0 | ✅ |
| `../localizestay-backend/Directory.Packages.props` | 1.0 | ✅ |
| `../localizestay-backend/LocalizeStay.sln` | 1.0 | ✅ |

### Categorias Obrigatórias

| # | Categoria | Task(s) / N/A | Skill Relacionada | Status |
|---|---|---|---|---|
| 1 | Setup / Configuração | 1.0, 5.0 | `dotnet-dependency-config` | ✅ |
| 2 | Modelos de Dados | 3.0, 4.0 | `dotnet-architecture` | ✅ |
| 3 | Lógica de Negócio | 3.0, 6.0–10.0 | `dotnet-architecture` | ✅ |
| 4 | Endpoints / Interfaces | 6.0–10.0 | `restful-api` conforme TechSpec | ✅ |
| 5 | Integrações Externas | 1.0 LogTo; 5.0 elegibilidade configurável; 9.0 outbox | `dotnet-dependency-config` | ✅ |
| 6 | Validações e Erros | 1.0, 3.0, 6.0–9.0 | `dotnet-code-quality` | ✅ |
| 7 | Testes | Subtarefas em 1.0–11.0 | `dotnet-testing` | ✅ |
| 8 | Observabilidade | 1.0, 4.0, 9.0, 10.0 | `dotnet-observability` | ✅ |
| 9 | Documentação | 11.0 | — | ✅ |
| 10 | Segurança | 1.0, 6.0–10.0, 11.0 | `dotnet-production-readiness` | ✅ |

## Análise de Paralelização

### Lanes de Execução Paralela

| Lane | Tarefas | Descrição |
|---|---|---|
| Lane A — Plataforma | 1.0 → 2.0 | Segurança, erros, testes base e auditoria compartilhada |
| Lane B — Domínio/dados | 3.0 → 4.0 | Agregados, invariantes, EF Core e migration |
| Lane C — Upstream/SLA | 5.0 | Elegibilidade e calendário podem avançar após 1.0, em paralelo a 2.0–4.0 |
| Lane D — APIs de cadastro | 6.0 e 7.0 | Parceiros e abertura/consulta podem avançar em paralelo após 4.0 e 5.0 |
| Lane E — Workflow | 8.0 → 9.0 | Subrecursos de prontidão e transições finais |
| Lane F — Leitura/certificação | 10.0 → 11.0 | Histórico, métricas, telemetria, contrato e documentação |

### Caminho Crítico

`1.0 → 2.0 → 3.0 → 4.0 → 7.0 → 8.0 → 9.0 → 10.0 → 11.0`

O caminho alternativo `1.0 → 5.0 → 7.0` deve terminar antes da abertura de incorporações. A tarefa 6.0 pode ser executada em paralelo à 7.0 e precisa estar concluída antes da certificação 11.0.

### Diagrama de Dependências

```text
1.0 ──→ 2.0 ──→ 3.0 ──→ 4.0 ──┬──→ 6.0 ────────────────┐
  └────────────→ 5.0 ──────────┴──→ 7.0 → 8.0 → 9.0 ─┤
                                  └────────→ 10.0 ←────┘
                                                ↓
                                              11.0
```

## Premissas e Bloqueios Externos

- A tarefa 1.0 exige confirmar `issuer`, `audience`, claim de escopo e claim de permissões do LogTo; valores sensíveis devem vir de ambiente/secret store.
- A tarefa 5.0 exige calendário, feriados, fuso e horário útil versionados, além da fonte inicial de pré-seleções e destinos aprovados.
- As tarefas 8.0 e 11.0 exigem o padrão definitivo de referência do repositório documental e os identificadores legais aceitos pelo Jurídico.
- O contrato soberano para validação automática é `api-contract.yaml`; `api-contract.md` permanece como documentação humana complementar.
