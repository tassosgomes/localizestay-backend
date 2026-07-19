# Quality Ledger

Registro estruturado de problemas identificados durante a validação das tarefas do AI Flow.

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
