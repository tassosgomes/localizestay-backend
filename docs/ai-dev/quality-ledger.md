# Quality Ledger

Registro estruturado de problemas identificados durante a validação das tarefas do AI Flow.

---

## [2026-07-19] | PRD: prd-incorporar-parceiros-e-propriedades | Task: 10.0

Modelo utilizado:
(Preenchido pelo Orquestrador)

### Problemas Identificados

1. Categoria Técnica: Teste inadequado
   Severidade: Alta
   Fase Detectada: Teste / Revisão
   Origem Provável: Task mal fragmentada
   Necessitou Reimplementação Significativa? Sim
   Descrição: As suítes exigidas `PropertyOnboardingMetricsQueryHandlerTests` e `PropertyOnboardingReadEndpointsTests` não existem. Os dois filtros mandatórios terminam sem executar casos, deixando sem evidência intervalos, destino, timezone, denominador zero, paginação, ausência de tracking e PII.

2. Categoria Técnica: Problema de performance
   Severidade: Alta
   Fase Detectada: Revisão
   Origem Provável: Lacuna na TechSpec
   Necessitou Reimplementação Significativa? Sim
   Descrição: O handler de métricas usa `Include` de três coleções seguido de `ToListAsync`, carregando agregados completos e calculando em memória, apesar de a tarefa determinar projeção e agregação SQL no banco.

3. Categoria Técnica: Lógica incorreta
   Severidade: Alta
   Fase Detectada: Revisão
   Origem Provável: Limitação do modelo
   Necessitou Reimplementação Significativa? Não
   Descrição: O contrato define `to` como exclusivo, mas o filtro usa `OpenedAt <= query.To`.

4. Categoria Técnica: Problema de segurança
   Severidade: Média
   Fase Detectada: Revisão
   Origem Provável: Contexto insuficiente
   Necessitou Reimplementação Significativa? Sim
   Descrição: O histórico expõe `BusinessAuditEntry.Metadata` sem uma projeção segura ou sanitização, contrariando a proibição contratual de vazar tokens, documentos ou conteúdo integral de mensagens.

### Resumo da Tarefa

Total de Problemas: 4
Categoria Técnica mais frequente: Teste inadequado / performance / lógica / segurança (1 de cada)
Origem mais frequente: Task mal fragmentada / Lacuna na TechSpec / Limitação do modelo / Contexto insuficiente (1 de cada)
Indício de fragilidade estrutural? Sim
Sugestão de melhoria no:
- PRD: N/A
- TechSpec: Definir queries agregadas por métrica, sem materialização de agregados, e uma allowlist explícita de metadados auditáveis públicos.
- Template de Task: Exigir confirmação de que os filtros de testes selecionaram casos reais antes do handoff.
- Skill: `dotnet-testing` pode reforçar que um filtro sem casos é falha de evidência, mesmo que o runner retorne sucesso.

## [2026-07-19] | PRD: prd-incorporar-parceiros-e-propriedades | Task: 8.0

Modelo utilizado:
(Preenchido pelo Orquestrador)

### Problemas Identificados

1. Categoria Técnica: Teste inadequado
   Severidade: Alta
   Fase Detectada: Teste / Revisão
   Origem Provável: Task mal fragmentada
   Necessitou Reimplementação Significativa? Sim
   Descrição: As suítes obrigatórias `ReadinessCommandHandlerTests` e `PropertyOnboardingSubresourceEndpointsTests` não existem. Os filtros previstos na tarefa terminam sem executar cenários; faltam evidências para a matriz dos seis gates, erro `INVALID_GATE_EVIDENCE`, SLA de quatro horas úteis, resolução/cancelamento e idempotência HTTP.

2. Categoria Técnica: Falha de validação
   Severidade: Alta
   Fase Detectada: Revisão
   Origem Provável: Lacuna na TechSpec
   Necessitou Reimplementação Significativa? Sim
   Descrição: O handler aceita qualquer `authorizedContact` e cria evidência sintética `formal-authorization`; não exige nem preserva referência de autorização formal ou contratual, contrariando RF-02.

3. Categoria Técnica: Lógica incorreta
   Severidade: Alta
   Fase Detectada: Revisão
   Origem Provável: Lacuna na TechSpec
   Necessitou Reimplementação Significativa? Sim
   Descrição: A atualização de `signedContract` descarta `contractNumber`, `signedAt` e `responsibleParties`, persistindo somente `repositoryReference`; os campos também não são validados como necessários. O `ContractReference` exigido pela tarefa não é preservado.

### Resumo da Tarefa

Total de Problemas: 3
Categoria Técnica mais frequente: Falha de validação / lógica incorreta / teste inadequado (1 de cada)
Origem mais frequente: Lacuna na TechSpec (2)
Indício de fragilidade estrutural? Sim
Sugestão de melhoria no:
- PRD: N/A
- TechSpec: Definir o modelo persistido de `ContractReference` e o formato verificável da autorização formal/contratual para evitar evidência sintética.
- Template de Task: Exigir a presença das classes de teste nomeadas antes do handoff e tratar filtro sem teste correspondente como falha.
- Skill: `dotnet-testing` pode orientar a verificação explícita de que filtros de testes selecionaram casos reais.

---

## [2026-07-19] | PRD: prd-incorporar-parceiros-e-propriedades | Task: 6.0

Modelo utilizado:
(Preenchido pelo Orquestrador)

### Problemas Identificados

1. Categoria Técnica: Teste inadequado
   Severidade: Média
   Fase Detectada: Revisão
   Origem Provável: Task mal fragmentada
   Necessitou Reimplementação Significativa? Não
   Descrição: A subtarefa 6.5 exige evidência para normalização, conflito concorrente, patch parcial, paginação, máscara e 401/403. Os testes entregues cobrem normalização, duplicidade prévia, patch parcial no handler e máscara em listagem de um único registro, mas não cobrem conflito concorrente contra o índice único PostgreSQL, paginação com múltiplos resultados, nem respostas 401/403 nos endpoints `/api/v1/partners`.

### Resumo da Tarefa

Total de Problemas: 1
Categoria Técnica mais frequente: Teste inadequado
Origem mais frequente: Task mal fragmentada
Indício de fragilidade estrutural? Não
Sugestão de melhoria no:
- PRD: N/A
- TechSpec: Explicitar os cenários HTTP mínimos de autorização, concorrência e paginação no plano de testes da fatia de parceiros.
- Template de Task: Converter os itens de cobertura em casos nomeados de teste para evitar handoff sem todos os cenários verificáveis.
- Skill: `dotnet-testing` pode reforçar que índices únicos e authorization policies exigem cenários de integração reais.

---

## [2026-07-19] | PRD: prd-incorporar-parceiros-e-propriedades | Task: 5.0 (Revalidação)

Modelo utilizado:
(Preenchido pelo Orquestrador)

### Problemas Identificados

1. Categoria Técnica: Violação de padrão arquitetural
   Severidade: Média
   Fase Detectada: Build
   Origem Provável: Task mal fragmentada
   Necessitou Reimplementação Significativa? Não
   Descrição: `dotnet format LocalizeStay.sln --verify-no-changes --no-restore` falha por ordenação de imports (`IMPORTS`) em `BusinessCalendarTests.cs` e `EligibilityValidatorTests.cs`. O critério verificável de qualidade da tarefa não foi satisfeito, embora os 17 testes focados, o build da solução e os 204 testes da solução tenham passado.

### Resumo da Tarefa

Total de Problemas: 1
Categoria Técnica mais frequente: Violação de padrão arquitetural
Origem mais frequente: Task mal fragmentada
Indício de fragilidade estrutural? Não
Sugestão de melhoria no:
- PRD: N/A
- TechSpec: N/A
- Template de Task: Incluir a verificação de formatação antes do handoff para validação.
- Skill: `dotnet-code-quality` pode reforçar a ordenação de imports como etapa obrigatória de conclusão.

---

## [2026-07-19] | PRD: prd-incorporar-parceiros-e-propriedades | Task: 5.0

Modelo utilizado:
(Preenchido pelo Orquestrador)

### Problemas Identificados

1. Categoria Técnica: Teste inadequado
   Severidade: Média
   Fase Detectada: Revisão
   Origem Provável: Task mal fragmentada
   Necessitou Reimplementação Significativa? Não
   Descrição: A implementação registra options com `ValidateOnStart`, mas não há teste que execute a composição/partida com as seções `Inventory:UpstreamEligibility` ou `Inventory:BusinessCalendar` ausentes ou inválidas. Isso deixa sem evidência o critério verificável de fail-fast fora do ambiente local e a subtarefa 5.6.

### Resumo da Tarefa

Total de Problemas: 1
Categoria Técnica mais frequente: Teste inadequado
Origem mais frequente: Task mal fragmentada
Indício de fragilidade estrutural? Não
Sugestão de melhoria no:
- PRD: N/A
- TechSpec: Especificar um cenário de teste de composição que valide `ValidateOnStart` para opções ausentes e inconsistentes.
- Template de Task: Incluir uma checklist explícita para testar opções obrigatórias com configuração ausente e inválida.
- Skill: `dotnet-testing` pode incluir um exemplo de teste de `IOptions` com `ValidateOnStart` em módulos.

---

## [2026-07-19] | PRD: prd-incorporar-parceiros-e-propriedades | Task: 1.0

Modelo utilizado:
(Preenchido pelo Orquestrador)

### Problemas Identificados

1. Categoria Técnica: Violação de padrão arquitetural (formatação / critério de sucesso)
   Severidade: Alta
   Fase Detectada: Build (validação automatizada)
   Origem Provável: Skill (débito técnico pré-existente no esqueleto basal `64454b4`, não coberto por critério de "não introduzir" da task)
   Necessitou Reimplementação Significativa? Sim (mecânica — uma execução de `dotnet format`)
   Descrição: `dotnet format LocalizeStay.sln --verify-no-changes --no-restore` retorna exit code 2 com 23 arquivos do esqueleto basal precisando de formatação (IDE0040/IDE1006/IMPORTS). Nenhum arquivo criado/modificado pela Task 1.0 aparece na lista, mas o critério de sucesso da task é textualmente `dotnet format --verify-no-changes --no-restore` e portanto falha literalmente.

2. Categoria Técnica: Problema de segurança (produção-readiness)
   Severidade: Média
   Fase Detectada: Revisão
   Origem Provável: Task (lacuna — task não especifica o default do flag)
   Necessitou Reimplementação Significativa? Não
   Descrição: `appsettings.json` define `LogTo:ValidateConfiguration=false`, desativando fail-fast fora do ambiente local. Recomenda-se remover a chave (deixar default `true`) ou criar `appsettings.Production.json` sobrescrevendo para `true`.

3. Categoria Técnica: Overengineering / código morto
   Severidade: Baixa
   Fase Detectada: Revisão
   Origem Provável: Modelo
   Necessitou Reimplementação Significativa? Não
   Descrição: `LogToOptions.ScopeClaimType` é declarado configurável mas nunca consumido — `PermissionHandler` usa `"scope"` literal. `TokenValidationParameters.RoleClaimType = logTo.PermissionClaimType` é atribuído mas o handler busca `"permission"` direto, tornando a propriedade efetivamente morta.

### Resumo da Tarefa

Total de Problemas: 3
Categoria Técnica mais frequente: Violação de padrão arquitetural / produção-readiness / overengineering (1 de cada)
Origem mais frequente: Skill / Task / Modelo (1 de cada)
Indício de fragilidade estrutural? Não — todos os arquivos entregues pela Task 1.0 estão corretamente formatados e testados; o débito de formatação é pré-existente.
Sugestão de melhoria no:
- PRD: N/A
- TechSpec: Recomenda-se incluir nota explícita sobre débito técnico de formatação pré-existente no esqueleto e como a Task 1.0 deve tratá-lo (sanitizar agora vs. task separada).
- Template de Task: Adicionar ao template que comandos de qualidade (`dotnet format --verify-no-changes`) devem ter escopo definido (solution-wide vs. arquivos alterados) quando o repositório já tiver débito pré-existente.
- Skill: O skill `dotnet-testing`/`dotnet-production-readiness` poderia documentar explicitamente o anti-padrão `ValidateConfiguration=false` em `appsettings.json` para opções de autenticação.

---

## [2026-07-19] | PRD: prd-incorporar-parceiros-e-propriedades | Task: 1.0 (Iteração 2)

Modelo utilizado:
(Preenchido pelo Orquestrador)

### Problemas Identificados

1. Categoria Técnica: N/A (iteração de correção — bloqueios anteriores resolvidos)
   Severidade: N/A
   Fase Detectada: Revalidação automatizada + revisão
   Origem Provável: N/A
   Necessitou Reimplementação Significativa? Não (apenas correções mecânicas e ajuste pontual de configuração)
   Descrição: A iter 1 falhava apenas em `dotnet format --verify-no-changes --no-restore` (exit 2) por débito pré-existente do esqueleto basal `64454b4`, e tinha 2 apontamentos não bloqueantes (`ValidateConfiguration=false` em `appsettings.json`; `ScopeClaimType`/`PermissionClaimType` declarados mas não consumidos). O `@implementer` executou `dotnet format` (auto-fix em 19 arquivos), corrigiu manualmente 4 violações IDE1006 (rename de campos privados para `_camelCase`), removeu `ValidateConfiguration` de `appsettings.json` (default `true` do tipo passa a valer fora de Development) e declarou-o apenas em `appsettings.Development.json`. `PermissionHandler` agora injeta `IOptions<LogToOptions>` e consome `ScopeClaimType`/`PermissionClaimType` em `FindAll(...)`.

### Resumo da Tarefa

Total de Problemas: 0 (zerados nesta iteração)
Categoria Técnica mais frequente: N/A
Origem mais frequente: N/A
Indício de fragilidade estrutural? Não — todos os débitos eram conhecidos e pré-existentes, ou apontamentos de revisão; nenhum novo problema foi introduzido.
Sugestão de melhoria no:
- PRD: N/A
- TechSpec: N/A
- Template de Task: Considerar template com seção "débito técnico conhecido" para que o validator diferencie bloqueio por código próprio vs. codebase pré-existente sem exigir retry (otimizaria 1 iteração).
- Skill: O `dotnet-production-readiness` poderia registrar o padrão positivo implementado nesta task (default seguro + override por ambiente) como referência reutilizável.

Iterações até estabilização: 2

---

## [2026-07-19] | PRD: prd-incorporar-parceiros-e-propriedades | Task: 2.0

Modelo utilizado:
(Preenchido pelo Orquestrador)

### Problemas Identificados

1. Categoria Técnica: Edge case ignorado (PII)
   Severidade: Baixa
   Fase Detectada: Revisão
   Origem Provável: Task (lacuna — task cita "telefone" nas convenções mas não exige regex específica)
   Necessitou Reimplementação Significativa? Não
   Descrição: `BusinessAuditEntry.LooksLikePersonalData` cobre e-mail, CPF formatado e CNPJ formatado, mas não telefone nem CPF/CNPJ não-formatados. Mitigado pela whitelist de 12 chaves de negócio (`_approvedMetadataKeys`) que só aceita identificadores de negócio; o regex atua como defesa em profundidade. Recomenda-se estender patterns em tarefa futura de hardening (4.0+).

2. Categoria Técnica: Teste inadequado (limitação aceitável)
   Severidade: Baixa
   Fase Detectada: Revisão
   Origem Provável: Skill (limitação do provider EF InMemory)
   Necessitou Reimplementação Significativa? Não
   Descrição: `Record_should_let_savechanges_round_trip_the_entry_in_the_same_transaction` usa EF Core InMemory, que não valida semântica transacional real (rollback conjunto). Cobertura transacional efetiva ficará para os testes de integração com PostgreSQL/Testcontainers previstos para a Task 8.0, conforme sequenciamento da TechSpec.

### Resumo da Tarefa

Total de Problemas: 2 (nenhum bloqueante)
Categoria Técnica mais frequente: Edge case / limitação de teste
Origem mais frequente: Task / Skill
Indício de fragilidade estrutural? Não — implementação coesa com ADR-003, fronteiras modulares respeitadas (`InventoryDbContext` interno, `InternalsVisibleTo` restrito a UnitTests), invariantes e whitelist corretos, contrato sem SaveChanges verificado por teste.
Sugestão de melhoria no:
- PRD: N/A
- TechSpec: Considerar mencionar explicitamente patterns PII adicionais (telefone, documentos não-formatados) quando a tarefa listar "telefone" nas convenções de sanitização.
- Template de Task: Adicionar nota sobre quando validar atomicidade transacional real (InMemory vs Testcontainers) para evitar confusão sobre cobertura de testes.
- Skill: `dotnet-testing` poderia registrar o pattern "InMemory não valida transações — atomicidade real exige Testcontainers" como referência recorrente.

---

## [2026-07-19] | PRD: prd-incorporar-parceiros-e-propriedades | Task: 3.0

Modelo utilizado:
(Preenchido pelo Orquestrador)

### Problemas Identificados

1. Categoria Técnica: Violação de padrão arquitetural / qualidade de código
   Severidade: Alta
   Fase Detectada: Revisão
   Origem Provável: Skill / Task (a própria task 3.0 cita "classes acima de 300 linhas" como anti-padrão)
   Necessitou Reimplementação Significativa? Sim (refatoração estrutural — extrair tipos auxiliares e reduzir classe principal)
   Descrição: A classe `PropertyOnboarding` possui 441 linhas, violando o limite de 300 linhas definido pelo skill `dotnet-code-quality` e pela convenção explícita da task 3.0. Os tipos `IdempotencyScope`, `BlockingReasonCode`, `BlockingReason` e `IdempotentReplayException` no final do arquivo devem ser extraídos para arquivos próprios, e a classe principal ainda precisará ser reduzida.

2. Categoria Técnica: Lógica incorreta
   Severidade: Alta
   Fase Detectada: Revisão
   Origem Provável: PRD / Task
   Necessitou Reimplementação Significativa? Sim (ajuste de invariante de domínio + teste regressivo)
   Descrição: `PropertyOnboarding.SubmitDuplicateReview` permite a decisão `DuplicateOfExistingProperty` mesmo quando `DuplicateReviewRequiresDecision == false`. A validação atual só rejeita `NotDuplicate` sem flag. Isso contradiz o PRD RF-03 (duplicidade deve ser sinalizada antes da revisão) e a subtarefa 3.6 (transições permitidas). Deve exigir a flag para qualquer decisão de revisão.

3. Categoria Técnica: Lógica incorreta (consistência de estado)
   Severidade: Média
   Fase Detectada: Revisão
   Origem Provável: Task
   Necessitou Reimplementação Significativa? Não
   Descrição: `FlagDuplicateReviewRequired` altera `DuplicateReviewRequiresDecision` mas não atualiza `UpdatedAt`, diferentemente de todos os outros métodos de mutação do agregado.

4. Categoria Técnica: Teste inadequado
   Severidade: Média
   Fase Detectada: Revisão
   Origem Provável: Task
   Necessitou Reimplementação Significativa? Não
   Descrição: Faltam testes regressivos para os problemas acima: `DuplicateOfExistingProperty` sem flag, `FlagDuplicateReviewRequired` atualizando timestamp, `RejectGate`/`ResetGateToPending` no `PropertyOnboarding`, e `GetBlockingReasons` quando `SubmittedToCuration`.

### Resumo da Tarefa

Total de Problemas: 4
Categoria Técnica mais frequente: Violação de padrão / Lógica incorreta
Origem mais frequente: Task
Indício de fragilidade estrutural? Sim — a classe `PropertyOnboarding` já nasce com 441 linhas e apresenta uma invariante de domínio inconsistente (revisão de duplicidade sem sinalização prévia), indicando que o modelo precisa de refatoração antes de ser consumido pelas tarefas 4.0+.
Sugestão de melhoria no:
- PRD: N/A
- TechSpec: Considerar incluir um diagrama de estados/transições do `PropertyOnboarding` para deixar explícitas as transições permitidas (ex: revisão de duplicidade só após sinalização).
- Template de Task: Adicionar check automático/implícito de limite de tamanho de classe (≤ 300 linhas) ao critério de qualidade.
- Skill: `dotnet-code-quality` poderia incluir exemplo concreto de como quebrar uma classe de agregado grande em value objects/serviços de domínio sem violar a regra de "domínio puro".

---

## [2026-07-19] | PRD: prd-incorporar-parceiros-e-propriedades | Task: 3.0 (Revalidação)

Modelo utilizado:
(Preenchido pelo Orquestrador)

### Problemas Identificados

Zero Defects Identified
Iterações até estabilização: 2

### Resumo da Tarefa

Total de Problemas: 0
Categoria Técnica mais frequente: N/A
Origem mais frequente: N/A
Indício de fragilidade estrutural? Não — todas as correções solicitadas na iteração anterior foram aplicadas: `PropertyOnboarding` reduzido para 279 linhas, `SubmitDuplicateReview` exige flag para qualquer decisão, `FlagDuplicateReviewRequired` atualiza `UpdatedAt`, e testes regressivos foram adicionados e passam.
Sugestão de melhoria no:
- PRD: N/A
- TechSpec: Considerar incluir um diagrama de estados/transições do `PropertyOnboarding` para deixar explícitas as transições permitidas (ex: revisão de duplicidade só após sinalização).
- Template de Task: Adicionar check automático/implícito de limite de tamanho de classe (≤ 300 linhas) ao critério de qualidade.
- Skill: `dotnet-code-quality` poderia incluir exemplo concreto de como quebrar uma classe de agregado grande em value objects/serviços de domínio sem violar a regra de "domínio puro".

---

## [2026-07-19] | PRD: prd-incorporar-parceiros-e-propriedades | Task: 4.0

Modelo utilizado:
(Preenchido pelo Orquestrador)

### Problemas Identificados

1. Categoria Técnica: Violação de padrão arquitetural
   Severidade: Alta
   Fase Detectada: Teste (validação automatizada)
   Origem Provável: Task / Skill
   Necessitou Reimplementação Significativa? Não (mecânica — alteração de visibilidade `public` → `internal`)
   Descrição: `LocalizeStay.ArchitectureTests.EncapsulationTests.Domain_application_and_infrastructure_types_should_not_be_public` falha para `Inventory.Domain` e `Inventory.Infrastructure`. Todos os tipos do domínio F01 (`Partner`, `PropertyOnboarding`, `Contact`, `LegalIdentifier`, `Address`, `Property`, `IdempotencyKey`, enums, etc.) estão `public`, e a migration `AddPortfolioOnboarding` gerada pelo EF Core também é `public partial class`. A baseline de arquitetura do repositório exige que apenas `*.Contracts` seja público; `Domain`, `Application` e `Infrastructure` devem ser `internal`. O `csproj` do módulo já declara `InternalsVisibleTo` para os projetos de teste, confirmando a intenção.

2. Categoria Técnica: Violação de padrão arquitetural
   Severidade: Média
   Fase Detectada: Teste (validação automatizada)
   Origem Provável: Task
   Necessitou Reimplementação Significativa? Não (mecânica)
   Descrição: A migration `20260719142051_AddPortfolioOnboarding.cs` foi gerada como `public partial class AddPortfolioOnboarding`, introduzindo um tipo público em `Inventory.Infrastructure`. A migration precisa ser ajustada para `internal partial class`, assim como o arquivo `.Designer.cs` e `InventoryDbContextModelSnapshot.cs`.

### Resumo da Tarefa

Total de Problemas: 2
Categoria Técnica mais frequente: Violação de padrão arquitetural
Origem mais frequente: Task
Indício de fragilidade estrutural? Sim — o módulo Inventory possui tipos de implementação expostos publicamente, quebrando a barreira de `Contracts` e o ADR de um schema/módulo dono. A correção é mecânica, mas o padrão precisa ser reforçado para as próximas tarefas.
Sugestão de melhoria no:
- PRD: N/A
- TechSpec: Incluir nota explícita na seção de arquitetura: "Tipos de `Domain`, `Application` e `Infrastructure` devem ser `internal`; apenas `*.Contracts` é público. Migrations geradas pelo EF Core devem ser ajustadas manualmente para `internal partial class`."
- Template de Task: Adicionar check automático/implícito de visibilidade de tipos (`public` vs `internal`) ao critério de qualidade, especialmente para tarefas que criam migrations ou novos tipos de domínio.
- Skill: O skill `dotnet-architecture` poderia incluir uma regra explícita sobre visibilidade de tipos em módulos (internal por padrão) e o ajuste necessário em migrations EF Core.

---

## [2026-07-19] | PRD: prd-incorporar-parceiros-e-propriedades | Task: 4.0 (Revalidação)

Modelo utilizado:
(Preenchido pelo Orquestrador)

### Problemas Identificados

Zero Defects Identified
Iterações até estabilização: 2

### Resumo da Tarefa

Total de Problemas: 0
Categoria Técnica mais frequente: N/A
Origem mais frequente: N/A
Indício de fragilidade estrutural? Não — todas as correções solicitadas na iteração anterior foram aplicadas: todos os tipos de `Inventory.Domain` são `internal`, as migrations e `ModelSnapshot` são `internal partial class`, `ReadinessGateTests.cs` foi ajustado para evitar CS0051, e as shadow properties de `CurationReturnConfiguration` e `ReadinessGateConfiguration` foram corrigidas. Build, 187 testes da solução, 8 testes de `InventoryPersistenceTests`, 18 testes de integração, script idempotente de migration e `dotnet format --verify-no-changes` passaram com sucesso.
Sugestão de melhoria no:
- PRD: N/A
- TechSpec: Manter a nota sobre visibilidade `internal` e ajuste manual de migrations para reforçar o padrão nas próximas tarefas.
- Template de Task: Adicionar check automático/implícito de visibilidade de tipos (`public` vs `internal`) ao critério de qualidade, especialmente para tarefas que criam migrations ou novos tipos de domínio.
- Skill: O skill `dotnet-architecture` poderia incluir uma regra explícita sobre visibilidade de tipos em módulos (internal por padrão) e o ajuste necessário em migrations EF Core.

---

## [2026-07-19] | PRD: prd-incorporar-parceiros-e-propriedades | Task: 5.0 (Revalidação final)

Modelo utilizado:
(Preenchido pelo Orquestrador)

### Problemas Identificados

Zero Defects Identified
Iterações até estabilização: 3

### Resumo da Tarefa

Total de Problemas: 0
Categoria Técnica mais frequente: N/A
Origem mais frequente: N/A
Indício de fragilidade estrutural? Não — os feedbacks anteriores foram resolvidos; as opções obrigatórias são testadas como ausentes e inconsistentes, e a solução atende à formatação exigida.
Sugestão de melhoria no:
- PRD: N/A
- TechSpec: N/A
- Template de Task: N/A
- Skill: N/A

---

## [2026-07-19] | PRD: prd-incorporar-parceiros-e-propriedades | Task: 8.0

Modelo utilizado:
(Preenchido pelo Orquestrador)

### Problemas Identificados

1. Categoria Técnica: Violação de padrão arquitetural
   Severidade: Alta
   Fase Detectada: Teste
   Origem Provável: Skill insuficiente
   Necessitou Reimplementação Significativa? Não
   Descrição: A migration AddReadinessGateContractReference foi declarada pública em Inventory.Infrastructure, violando a regra de encapsulamento e fazendo a suíte completa falhar.

2. Categoria Técnica: Teste inadequado
   Severidade: Média
   Fase Detectada: Revisão
   Origem Provável: Task mal fragmentada
   Necessitou Reimplementação Significativa? Não
   Descrição: A cobertura exigida para idempotência da revisão de duplicidade e encerramento como duplicada não foi implementada.

3. Categoria Técnica: Falha de validação
   Severidade: Média
   Fase Detectada: Revisão
   Origem Provável: Lacuna na TechSpec
   Necessitou Reimplementação Significativa? Não
   Descrição: OperationalChannelTest.TestedAt é obrigatório no contrato, porém não é validado pelo UpdateReadinessGateCommandValidator.

### Resumo da Tarefa

Total de Problemas: 3
Categoria Técnica mais frequente: N/A
Origem mais frequente: N/A
Indício de fragilidade estrutural? Sim — a suíte focada passa, mas não cobre a regra arquitetural nem a idempotência contratual.
Sugestão de melhoria no:
- PRD: N/A
- TechSpec: Especificar validação de presença para todos os campos obrigatórios condicionais do gate operacional.
- Template de Task: Exigir que cenários obrigatórios de idempotência tenham teste nomeado.
- Skill: dotnet-testing pode reforçar a execução da suíte completa quando migrations alteram Infrastructure.

---

## [2026-07-19] | PRD: prd-incorporar-parceiros-e-propriedades | Task: 7.0

Modelo utilizado:
(Preenchido pelo Orquestrador)

### Problemas Identificados

1. Categoria Técnica: Lógica incorreta
   Severidade: Alta
   Fase Detectada: Revisão
   Origem Provável: Contexto Insuficiente
   Necessitou Reimplementação Significativa? Não
   Descrição: O filtro `readinessStatus` não considera pendências abertas, podendo classificar e retornar uma incorporação bloqueada como `ready`.

2. Categoria Técnica: Erro de integração
   Severidade: Alta
   Fase Detectada: Revisão
   Origem Provável: TechSpec
   Necessitou Reimplementação Significativa? Não
   Descrição: Os valores enum retornados pelo endpoint usam PascalCase de `Enum.ToString()` em vez dos valores camelCase definidos pelo API Contract.

3. Categoria Técnica: Falha de validação
   Severidade: Média
   Fase Detectada: Revisão
   Origem Provável: Skill
   Necessitou Reimplementação Significativa? Não
   Descrição: O cálculo do filtro `overdue` usa `DateTimeOffset.UtcNow` em vez de `IClock`, tornando a regra temporal não determinística.

4. Categoria Técnica: Teste inadequado
   Severidade: Alta
   Fase Detectada: Teste
   Origem Provável: Task
   Necessitou Reimplementação Significativa? Não
   Descrição: O arquivo e os cenários de integração `PropertyOnboardingEndpointsTests` exigidos pela tarefa não foram criados; o filtro de teste específico não encontrou cenários HTTP a executar.

### Resumo da Tarefa

Total de Problemas: 4
Categoria Técnica mais frequente: N/A (uma ocorrência por categoria)
Origem mais frequente: N/A (uma ocorrência por origem)
Indício de fragilidade estrutural? Sim — os mapeamentos de contrato e as regras de consulta não possuem cobertura HTTP dedicada, permitindo divergências de payload e de estado de prontidão.
Sugestão de melhoria no:
- PRD: N/A
- TechSpec: Explicitar a conversão obrigatória dos enums internos para os valores camelCase do contrato.
- Template de Task: Exigir que o comando filtrado valide ao menos um teste descoberto, especialmente quando a tarefa nomeia uma classe de integração.
- Skill: `dotnet-testing` poderia incluir uma verificação explícita de que filtros de `dotnet test` encontraram casos de teste.

---

## [2026-07-19] | PRD: prd-incorporar-parceiros-e-propriedades | Task: 6.0 (Revalidação)

Modelo utilizado:
(Preenchido pelo Orquestrador)

### Problemas Identificados

Zero Defects Identified
Iterações até estabilização: 2

### Resumo da Tarefa

Total de Problemas: 0
Categoria Técnica mais frequente: N/A
Origem mais frequente: N/A
Indício de fragilidade estrutural? Não — os cenários de integração ausentes foram acrescentados e passaram: concorrência no índice único PostgreSQL, paginação real e autorização 401/403.
Sugestão de melhoria no:
- PRD: N/A
- TechSpec: N/A
- Template de Task: N/A
- Skill: N/A

---

## [2026-07-19] | PRD: prd-incorporar-parceiros-e-propriedades | Task: 7.0 (Revalidação)

Modelo utilizado:
(Preenchido pelo Orquestrador)

### Problemas Identificados

1. Categoria Técnica: Teste inadequado
   Severidade: Média
   Fase Detectada: Revisão
   Origem Provável: Task mal fragmentada
   Necessitou Reimplementação Significativa? Não
   Descrição: Os testes focados de criação, conflito ativo, similaridade, filtros e paginação passam, mas a subtarefa 7.6 exige demonstrar que um novo ciclo para a mesma propriedade é permitido após o encerramento do anterior. Não há teste que encerre um ciclo e abra o seguinte.

### Resumo da Tarefa

Total de Problemas: 1
Categoria Técnica mais frequente: Teste inadequado
Origem mais frequente: Task mal fragmentada
Indício de fragilidade estrutural? Não
Sugestão de melhoria no:
- PRD: N/A
- TechSpec: Nomear o cenário de novo ciclo encerrado no plano de testes da operação de abertura.
- Template de Task: Transformar os cenários de cobertura obrigatória em casos de teste nomeados.
- Skill: `dotnet-testing` pode reforçar a cobertura de transições que liberam restrições de unicidade.

---

## [2026-07-19] | PRD: prd-incorporar-parceiros-e-propriedades | Task: 7.0 (Revalidação final)

Modelo utilizado:
(Preenchido pelo Orquestrador)

### Problemas Identificados

1. Categoria Técnica: Erro de integração
   Severidade: Alta
   Fase Detectada: Revisão
   Origem Provável: TechSpec
   Necessitou Reimplementação Significativa? Não
   Descrição: O mapper de resposta retorna `duplicateReview.reviews`, mas o contrato soberano exige `duplicateReview.candidates` e define `latestDecision`. O payload de criação, consulta e atualização de incorporação fica incompatível com `DuplicateReviewState`, impedindo o cliente de consumir os candidatos de similaridade.

### Resumo da Tarefa

Total de Problemas: 1
Categoria Técnica mais frequente: Erro de integração
Origem mais frequente: TechSpec
Indício de fragilidade estrutural? Sim — os testes HTTP verificam somente `duplicateReview.required`, sem validar a forma completa do schema contratual.
Sugestão de melhoria no:
- PRD: N/A
- TechSpec: Especificar o mapeamento dos candidatos de similaridade e da última decisão para o DTO público.
- Template de Task: Exigir teste de conformidade de schema nos payloads de cada `operationId`.
- Skill: `dotnet-testing` pode incluir uma verificação explícita das propriedades obrigatórias do OpenAPI nos testes de integração.

---

## [2026-07-19] | PRD: prd-incorporar-parceiros-e-propriedades | Task: 7.0 (Revalidação pós-correção de duplicateReview)

Modelo utilizado:
(Preenchido pelo Orquestrador)

### Problemas Identificados

1. Categoria Técnica: Teste inadequado
   Severidade: Média
   Fase Detectada: Revisão
   Origem Provável: Task mal fragmentada
   Necessitou Reimplementação Significativa? Não
   Descrição: O mapper foi corrigido para retornar `duplicateReview.candidates` e `latestDecision`, mas o teste HTTP de similaridade continua verificando somente `duplicateReview.required`. Não há cobertura contra nova divergência do schema `DuplicateReviewState` nem dos campos obrigatórios de cada candidato.

### Resumo da Tarefa

Total de Problemas: 1
Categoria Técnica mais frequente: Teste inadequado
Origem mais frequente: Task mal fragmentada
Indício de fragilidade estrutural? Sim — uma correção de incompatibilidade contratual não ganhou asserção de regressão no teste que cobre o endpoint.
Sugestão de melhoria no:
- PRD: N/A
- TechSpec: N/A
- Template de Task: Exigir asserções dos campos alterados em toda correção de contrato HTTP.
- Skill: `dotnet-testing` pode reforçar testes de regressão que verifiquem o payload completo de schemas corrigidos.

---

## [2026-07-19] | PRD: prd-incorporar-parceiros-e-propriedades | Task: 7.0 (Revalidação final)

Modelo utilizado:
(Preenchido pelo Orquestrador)

### Problemas Identificados

Zero Defects Identified
Iterações até estabilização: 5

### Resumo da Tarefa

Total de Problemas: 0
Categoria Técnica mais frequente: N/A
Origem mais frequente: N/A
Indício de fragilidade estrutural? Não — a regressão contratual de `duplicateReview` passou a ter cobertura HTTP dos campos obrigatórios de `DuplicateCandidate` e de `latestDecision`.
Sugestão de melhoria no:
- PRD: N/A
- TechSpec: N/A
- Template de Task: N/A
- Skill: N/A

---

## [2026-07-19] | PRD: prd-incorporar-parceiros-e-propriedades | Task: 8.0 (Revalidação)

Modelo utilizado:
(Preenchido pelo Orquestrador)

### Problemas Identificados

1. Categoria Técnica: Falha de validação
   Severidade: Média
   Fase Detectada: Revisão
   Origem Provável: Lacuna na TechSpec
   Necessitou Reimplementação Significativa? Não
   Descrição: `CreateCommunicationRecordCommandValidator` aceita `receivedAt` e `processedAt` ausentes. Como ambos desserializam como `DateTimeOffset.MinValue` e a única regra é `processedAt >= receivedAt`, o handler pode persistir uma comunicação em `0001-01-01`, contrariando os campos obrigatórios do contrato e invalidando a medição de SLA.

### Resumo da Tarefa

Total de Problemas: 1
Categoria Técnica mais frequente: Falha de validação
Origem mais frequente: Lacuna na TechSpec
Indício de fragilidade estrutural? Não
Sugestão de melhoria no:
- PRD: N/A
- TechSpec: Explicitar a validação de presença dos timestamps obrigatórios de comunicação, além da ordem cronológica.
- Template de Task: Exigir cenário HTTP para cada campo obrigatório não anulável que possa receber valor default durante a desserialização.
- Skill: `dotnet-testing` pode reforçar testes de campos obrigatórios em requests Minimal API.

---

## [2026-07-19] | PRD: prd-incorporar-parceiros-e-propriedades | Task: 8.0 (Revalidação final)

Modelo utilizado:
(Preenchido pelo Orquestrador)

### Problemas Identificados

1. Categoria Técnica: Erro de integração
   Severidade: Média
   Fase Detectada: Revisão
   Origem Provável: Lacuna na TechSpec
   Necessitou Reimplementação Significativa? Não
   Descrição: O contrato de atualização de pendência permite `assigneeId` e `targetAt` nulos, mas o command perde a presença do campo e trata null como omissão. Portanto, o cliente não consegue limpar a atribuição ou a data-alvo por PATCH.

### Resumo da Tarefa

Total de Problemas: 1
Categoria Técnica mais frequente: Erro de integração
Origem mais frequente: Lacuna na TechSpec
Indício de fragilidade estrutural? Não
Sugestão de melhoria no:
- PRD: N/A
- TechSpec: Especificar a semântica de atualização para campos anuláveis e a distinção entre omissão e null explícito em PATCH.
- Template de Task: Exigir cenário HTTP de limpeza para cada campo anulável em requests PATCH.
- Skill: `dotnet-testing` pode reforçar testes de semântica PATCH (omissão versus null).

---

## [2026-07-19] | PRD: prd-incorporar-parceiros-e-propriedades | Task: 8.0 (Revalidação pós-correção de PATCH anulável)

Modelo utilizado:
(Preenchido pelo Orquestrador)

### Problemas Identificados

Zero Defects Identified
Iterações até estabilização: 3

### Resumo da Tarefa

Total de Problemas: 0
Categoria Técnica mais frequente: N/A
Origem mais frequente: N/A
Indício de fragilidade estrutural? Não — a regressão de `null` explícito em PATCH possui cobertura HTTP para ambos os campos anuláveis.
Sugestão de melhoria no:
- PRD: N/A
- TechSpec: N/A
- Template de Task: N/A
- Skill: N/A

---

## [2026-07-19] | PRD: prd-incorporar-parceiros-e-propriedades | Task: 9.0

Modelo utilizado:
(Preenchido pelo Orquestrador)

### Problemas Identificados

1. Categoria Técnica: Teste inadequado
   Severidade: Alta
   Fase Detectada: Teste / Revisão
   Origem Provável: Task mal fragmentada
   Necessitou Reimplementação Significativa? Sim
   Descrição: As suítes obrigatórias `PropertyOnboardingWorkflowTests` e `OutboxAndAuditTests` não foram criadas. Os filtros de integração terminam sem executar cenários, deixando sem evidência o fluxo HTTP, a autorização, a atomicidade PostgreSQL de estado+audit+outbox, retry, conflito de payload, devolução e novo ciclo após encerramento.

2. Categoria Técnica: Erro de integração
   Severidade: Alta
   Fase Detectada: Revisão
   Origem Provável: Lacuna na TechSpec
   Necessitou Reimplementação Significativa? Não
   Descrição: Submit e devolução fazem consulta de idempotência antes de inserir, mas não convertem uma colisão concorrente do índice único em replay ou `409 STATE_CONFLICT`. A segunda requisição simultânea pode receber `DbUpdateException`/500, violando a garantia de idempotência.

### Resumo da Tarefa

Total de Problemas: 2
Categoria Técnica mais frequente: Teste inadequado / Erro de integração
Origem mais frequente: Task mal fragmentada / Lacuna na TechSpec
Indício de fragilidade estrutural? Sim
Sugestão de melhoria no:
- PRD: N/A
- TechSpec: Definir o comportamento de concorrência da chave de idempotência e exigir que colisões de unicidade sejam convertidas em replay ou `STATE_CONFLICT`.
- Template de Task: Exigir que filtros de testes indicados na tarefa tenham ao menos um teste descoberto, além do exit code.
- Skill: `dotnet-testing` pode reforçar testes de integração de concorrência e rollback transacional com PostgreSQL.

---

## [2026-07-19] | PRD: prd-incorporar-parceiros-e-propriedades | Task: 9.0 (Revalidação pós-correção)

Modelo utilizado:
(Preenchido pelo Orquestrador)

### Problemas Identificados

1. Categoria Técnica: Falha de validação
   Severidade: Baixa
   Fase Detectada: Build
   Origem Provável: Contexto insuficiente
   Necessitou Reimplementação Significativa? Não
   Descrição: `dotnet format LocalizeStay.sln --verify-no-changes --no-restore` falhou porque a migration `20260719184815_AddIdempotencyPayloadFingerprint.cs` contém um charset/BOM inválido na linha 1, coluna 1.

### Resumo da Tarefa

Total de Problemas: 1
Categoria Técnica mais frequente: Falha de validação
Origem mais frequente: Contexto insuficiente
Indício de fragilidade estrutural? Não
Sugestão de melhoria no:
- PRD: N/A
- TechSpec: N/A
- Template de Task: Incluir `dotnet format --verify-no-changes` como gate antes de solicitar a validação.
- Skill: `dotnet-code-quality` pode reforçar a preservação de UTF-8 sem BOM em arquivos C# novos.

---

## [2026-07-19] | PRD: prd-incorporar-parceiros-e-propriedades | Task: 9.0 (Revalidação pós-correção de BOM)

Modelo utilizado:
(Preenchido pelo Orquestrador)

### Problemas Identificados

1. Categoria Técnica: Erro de integração
   Severidade: Alta
   Fase Detectada: Revisão
   Origem Provável: Lacuna na TechSpec
   Necessitou Reimplementação Significativa? Não
   Descrição: `PropertyOnboardingCommands` cria `ActivitySource` e `Meter` com o nome `LocalizeStay.Inventory.Lifecycle`, mas `OpenTelemetryExtensions` registra somente o source `LocalizeStay.Inventory.Upstream` e não registra nenhum meter do lifecycle. Logo, o span de submit e os counters de sucesso, bloqueio e falha de outbox da subtarefa 9.6 não são coletados nem exportados.

### Resumo da Tarefa

Total de Problemas: 1
Categoria Técnica mais frequente: Erro de integração
Origem mais frequente: Lacuna na TechSpec
Indício de fragilidade estrutural? Sim — há instrumento manual sem teste de captura nem registro no provedor OpenTelemetry.
Sugestão de melhoria no:
- PRD: N/A
- TechSpec: Declarar explicitamente os nomes de source/meter e sua inclusão no pipeline OpenTelemetry.
- Template de Task: Exigir teste que observe cada span e métrica manual especificada.
- Skill: `dotnet-observability` pode reforçar a verificação de `AddSource` e `AddMeter` ao criar instrumentação manual.

---

## [2026-07-19] | PRD: prd-incorporar-parceiros-e-propriedades | Task: 9.0 (Revalidação pós-correção de telemetria)

Modelo utilizado:
(Preenchido pelo Orquestrador)

### Problemas Identificados

1. Categoria Técnica: Falha de validação
   Severidade: Baixa
   Fase Detectada: Build
   Origem Provável: Contexto insuficiente
   Necessitou Reimplementação Significativa? Não
   Descrição: `dotnet format LocalizeStay.sln --verify-no-changes --no-restore` falhou porque `tests/LocalizeStay.UnitTests/Inventory/SubmissionCommandHandlerTests.cs` possui imports fora da ordem de formatação, na linha 1, coluna 1.

### Resumo da Tarefa

Total de Problemas: 1
Categoria Técnica mais frequente: Falha de validação
Origem mais frequente: Contexto insuficiente
Indício de fragilidade estrutural? Não
Sugestão de melhoria no:
- PRD: N/A
- TechSpec: N/A
- Template de Task: Exigir execução do gate de formatação imediatamente antes de solicitar validação.
- Skill: `dotnet-code-quality` pode reforçar organização de imports em testes novos.

---

## [2026-07-19] | PRD: prd-incorporar-parceiros-e-propriedades | Task: 9.0 (Revalidação final)

Modelo utilizado:
(Preenchido pelo Orquestrador)

### Problemas Identificados

Zero Defects Identified
Iterações até estabilização: 5

### Resumo da Tarefa

Total de Problemas: 0
Categoria Técnica mais frequente: N/A
Origem mais frequente: N/A
Indício de fragilidade estrutural? Não — as correções de concorrência, atomicidade, telemetria e formatação possuem cobertura nos gates exigidos.
Sugestão de melhoria no:
- PRD: N/A
- TechSpec: N/A
- Template de Task: N/A
- Skill: N/A

---

## [2026-07-19] | PRD: prd-incorporar-parceiros-e-propriedades | Task: 9.0 (Validação independente)

Modelo utilizado:
(Preenchido pelo Orquestrador)

### Problemas Identificados

Zero Defects Identified
Iterações até estabilização: 1

### Resumo da Tarefa

Total de Problemas: 0
Categoria Técnica mais frequente: N/A
Origem mais frequente: N/A
Indício de fragilidade estrutural? Não — foram verificados o rollback transacional, a concorrência idempotente, o contrato HTTP e o registro do source/meter de lifecycle.
Sugestão de melhoria no:
- PRD: N/A
- TechSpec: N/A
- Template de Task: N/A
- Skill: N/A

---

## [2026-07-19] | PRD: prd-incorporar-parceiros-e-propriedades | Task: 10.0 (Revalidação pós-correção)

Modelo utilizado:
(Preenchido pelo Orquestrador)

### Problemas Identificados

1. Categoria Técnica: Falha de validação
   Severidade: Média
   Fase Detectada: Build
   Origem Provável: Contexto insuficiente
   Necessitou Reimplementação Significativa? Não
   Descrição: `dotnet format LocalizeStay.sln --verify-no-changes --no-restore` falhou em cinco arquivos, incluindo consultas, comandos, telemetria e imports modificados na task.

2. Categoria Técnica: Erro de integração
   Severidade: Alta
   Fase Detectada: Revisão
   Origem Provável: Lacuna na TechSpec
   Necessitou Reimplementação Significativa? Não
   Descrição: O instrumento `outbox.retry.exhausted` é criado no meter `LocalizeStay.Outbox`, mas o pipeline OpenTelemetry registra somente `LocalizeStay.Inventory.Lifecycle`; o alerta obrigatório de quinta tentativa não é exportado.

3. Categoria Técnica: Teste inadequado
   Severidade: Média
   Fase Detectada: Revisão
   Origem Provável: Task mal fragmentada
   Necessitou Reimplementação Significativa? Não
   Descrição: As suítes específicas executam, mas não validam com dataset determinístico todos os numeradores, denominadores e percentuais das métricas contratuais.

### Resumo da Tarefa

Total de Problemas: 3
Categoria Técnica mais frequente: Falha de validação / Erro de integração / Teste inadequado
Origem mais frequente: Contexto insuficiente / Lacuna na TechSpec / Task mal fragmentada
Indício de fragilidade estrutural? Sim — um instrumento manual documentado não está incluído no provider OTLP e os cálculos de negócio não têm cobertura completa.
Sugestão de melhoria no:
- PRD: N/A
- TechSpec: Declarar todos os meters exigidos e seus alertas no pipeline OpenTelemetry.
- Template de Task: Exigir uma matriz de cenários que cubra cada métrica contratual com numerador, denominador e percentual.
- Skill: `dotnet-observability` e `dotnet-testing` podem reforçar a validação de exportação de meters e a cobertura integral de métricas de negócio.

---

## [2026-07-19] | PRD: prd-incorporar-parceiros-e-propriedades | Task: 10.0 (Revalidação final)

Modelo utilizado:
(Preenchido pelo Orquestrador)

### Problemas Identificados

Zero Defects Identified
Iterações até estabilização: 3

### Resumo da Tarefa

Total de Problemas: 0
Categoria Técnica mais frequente: N/A
Origem mais frequente: N/A
Indício de fragilidade estrutural? Não — os gates obrigatórios passaram e a integração OTLP, as métricas contratuais e a sanitização do histórico foram revisadas.
Sugestão de melhoria no:
- PRD: N/A
- TechSpec: N/A
- Template de Task: N/A
- Skill: N/A
