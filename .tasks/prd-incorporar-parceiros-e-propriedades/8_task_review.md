# Revisão da Tarefa 8.0

## Resultado da validação automatizada

- `dotnet test tests/LocalizeStay.UnitTests/LocalizeStay.UnitTests.csproj --filter "FullyQualifiedName~ReadinessCommandHandlerTests"`: aprovado — 9 testes.
- `dotnet test tests/LocalizeStay.IntegrationTests/LocalizeStay.IntegrationTests.csproj --filter "FullyQualifiedName~PropertyOnboardingSubresourceEndpointsTests"`: aprovado — 6 testes.
- `dotnet build LocalizeStay.sln --no-restore`: aprovado — 24 projetos, 0 erros, 0 avisos.
- `dotnet format LocalizeStay.sln --verify-no-changes --no-restore`: aprovado — 0 arquivos a formatar.
- `git diff --check`: aprovado.

## Revisão técnica

Aprovada. A correção preserva a presença de `assigneeId` e `targetAt` no PATCH, distinguindo omissão de `null` explícito. Assim, ambos os campos anuláveis podem ser limpos conforme o contrato `UpdatePendingIssueRequest`. Os dois cenários HTTP de regressão foram adicionados e passaram.

Também foram verificados os cinco subrecursos contra a tarefa, PRD, TechSpec e contrato API-first: evidências específicas de gate e erro `INVALID_GATE_EVIDENCE`; pendências sem exclusão física e `resolutionNote` obrigatório; comunicação limitada a WhatsApp/e-mail e SLA pelo calendário; revisão de duplicidade idempotente e fechamento como duplicada; policies de escrita, códigos 200/201 e `Location` nas criações. Auditorias não carregam PII de contato ou resumo de comunicação.

## Recomendação final

**APROVADA**
