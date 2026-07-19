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
Categoria Técnica mais frequente: Violação de padrão / produção-readiness / overengineering (1 de cada)
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
