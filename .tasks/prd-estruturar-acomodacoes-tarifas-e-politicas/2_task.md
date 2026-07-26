---
status: pending
parallelizable: false
blocked_by: ["1.0"]
---

<task_context>
<domain>inventory/domain/incorporated-properties</domain>
<type>integration</type>
<scope>core_feature</scope>
<complexity>medium</complexity>
<dependencies>database</dependencies>
<unblocks>"3.0, 7.0"</unblocks>
</task_context>

# Tarefa 2.0: Materializar a propriedade incorporada a partir da F01

## Relacionada às User Stories

- [US-01] Cadastrar condições comerciais progressivamente (suporte)
- [US-04] Fornecer condições pelos canais atuais (suporte por continuidade da F01)

## Visão Geral

Introduzir `IncorporatedProperty` como identidade canônica da propriedade recebida da F01 e sincronizá-la atomicamente quando o onboarding for submetido. O UUID deve ser exatamente o `PropertyOnboarding.Id`, sem tabela de mapeamento e sem FK entre módulos.

## Requisitos

- Criar ou atualizar a propriedade canônica na mesma transação do envio F01.
- Preservar nome, destino, autoria inicial, timestamps e referência ao onboarding.
- A operação deve ser idempotente para retries da submissão F01.
- O backfill de onboardings existentes será executado na tarefa 7.0.
- Não produzir evento externo adicional nessa sincronização.

## Arquivos Envolvidos

- **Criar:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Domain/IncorporatedProperties/IncorporatedProperty.cs`
  - `../localizestay-backend/tests/LocalizeStay.UnitTests/Inventory/IncorporatedPropertyTests.cs`
- **Modificar:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/PropertyOnboardings/PropertyOnboardingCommands.cs` (materializar no envio F01)
- **Referência:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Domain/PropertyOnboardings/PropertyOnboarding.cs`
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Infrastructure/InventoryDbContext.cs`
  - `tasks/prd-estruturar-acomodacoes-tarifas-e-politicas/adrs/adr-001.md`
- **Skills para consultar durante implementação:**
  - `dotnet-architecture` — entidade de domínio e transação no DbContext existente
  - `dotnet-code-quality` — invariantes, naming e `CancellationToken`
  - `dotnet-testing` — testes unitários AAA e cenário idempotente
  - `dotnet-observability` — contexto estruturado sem PII

## Subtarefas

- [ ] 2.1 Modelar `IncorporatedProperty` com fábrica e método de sincronização explícito.
- [ ] 2.2 Alterar `SubmitToCurationCommandHandler` para criar/sincronizar a entidade antes do único `SaveChangesAsync`.
- [ ] 2.3 Garantir que retry/replay não duplique nem altere a identidade canônica.
- [ ] 2.4 Adicionar auditoria funcional suficiente para correlacionar onboarding e propriedade.
- [ ] 2.5 Criar testes de identidade, sincronização, idempotência e timestamps.

## Sequenciamento

- Bloqueado por: 1.0
- Desbloqueia: 3.0 e 7.0
- Paralelizável: Não; estabelece a raiz de identidade para todas as ofertas.

## Rastreabilidade

- Esta tarefa cobre: US-01 e US-04 como suporte; upstream de RF-01 a RF-06.
- Evidência esperada: submissão F01 materializa uma única propriedade com `Id == OnboardingId` e sem novo commit separado.

## Detalhes de Implementação

`IncorporatedProperty.Id` deve ser igual a `PropertyOnboarding.Id`. A entidade pertence ao módulo Inventory e será persistida no schema `inventory`. O handler existente já grava onboarding, auditoria, idempotência e outbox em um único `SaveChangesAsync`; a propriedade deve participar dessa mesma unidade transacional.

O método de sincronização deve manter uma ação clara, rejeitar troca de identidade e atualizar apenas dados canônicos aprovados. O ator inicial vem do comando autenticado da F01, nunca de payload externo da F02.

**Convenções da stack (das skills consultadas):**

- Não criar repositório genérico; usar `InventoryDbContext` conforme o desvio aprovado.
- Domínio sem dependência de EF Core; construtores não públicos e invariantes encapsuladas.
- Métodos assíncronos de handler propagam `CancellationToken` até EF Core.
- Logs usam `onboardingId` e `propertyId`; não registram contato, e-mail ou telefone.
- Testes usam xUnit, AwesomeAssertions, AAA e nomes em inglês.

## Critérios de Sucesso (Verificáveis)

- [ ] Testes unitários passam: `dotnet test ../localizestay-backend/LocalizeStay.sln --filter "FullyQualifiedName~IncorporatedPropertyTests|FullyQualifiedName~SubmissionCommandHandlerTests"`
- [ ] Build compila sem erros: `dotnet build ../localizestay-backend/LocalizeStay.sln --no-restore`
- [ ] Formatação válida: `dotnet format ../localizestay-backend/LocalizeStay.sln --verify-no-changes --no-restore`
- [ ] Duas execuções idempotentes produzem uma única `IncorporatedProperty` com o UUID do onboarding.
- [ ] Falha no commit não deixa propriedade materializada sem a submissão F01 correspondente.

