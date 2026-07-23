---
status: pending
parallelizable: true
blocked_by: ["3.0"]
---

<task_context>
<domain>inventory/domain/commercial-policies</domain>
<type>implementation</type>
<scope>core_feature</scope>
<complexity>high</complexity>
<dependencies>database</dependencies>
<unblocks>"7.0, 8.0, 10.0"</unblocks>
</task_context>

# Tarefa 4.0: Implementar políticas comerciais reutilizáveis

## Relacionada às User Stories

- [US-02] Reutilizar políticas e definir política padrão (direta)
- [US-01] Salvar condições comerciais progressivamente (suporte)

## Visão Geral

Implementar a fatia de políticas comerciais no agregado e na aplicação: criação pelos dois tipos permitidos, regra jurídica versionada, definição/troca de padrão, atualização opcional de acomodações existentes, desativação com substituta e hard delete somente antes do primeiro envio.

## Requisitos

- Permitir somente `flexible` e `nonRefundable` resolvidos pelo catálogo jurídico.
- Impedir dois registros ativos do mesmo tipo na propriedade.
- Permitir definir política padrão na criação e trocar o padrão com decisão explícita sobre acomodações existentes.
- Desativação em uso exige substituta ativa, diferente e pertencente à mesma propriedade.
- Hard delete exige `everSubmitted == false`, `isDefault == false` e `usageCount == 0`.
- Toda mutação verifica `expectedRevision` quando declarado pelo contrato.

## Arquivos Envolvidos

- **Criar:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Domain/CommercialOffers/CommercialPolicy.cs`
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/CommercialOffers/CommercialPolicyCommands.cs`
  - `../localizestay-backend/tests/LocalizeStay.UnitTests/Inventory/CommercialPolicyTests.cs`
- **Modificar:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Domain/CommercialOffers/CommercialOffer.cs` (operações de política)
- **Referência:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/Application/LegalPolicies/ILegalPolicyCatalog.cs`
  - `tasks/prd-estruturar-acomodacoes-tarifas-e-politicas/api-contract.yaml`
  - `domains/oferta-inventario/domain.md`
- **Skills para consultar durante implementação:**
  - `dotnet-architecture` — entidade filha, Commands/Handlers e DbContext direto
  - `dotnet-code-quality` — operações explícitas, exceções específicas e cancellation
  - `dotnet-testing` — testes parametrizados para tipos e matrizes de substituição
  - `dotnet-observability` — auditoria versus logs diagnósticos

## Subtarefas

- [ ] 4.1 Modelar `CommercialPolicy` com tipo, rule set imutável, status, padrão, uso e histórico de envio.
- [ ] 4.2 Implementar `CreateCommercialPolicyCommandHandler` usando `ILegalPolicyCatalog`.
- [ ] 4.3 Implementar `SetDefaultCommercialPolicyCommandHandler` e atualização atômica opcional das acomodações.
- [ ] 4.4 Implementar `UpdateCommercialPolicyCommandHandler` para desativação/substituição.
- [ ] 4.5 Implementar `DeleteCommercialPolicyCommandHandler` com hard delete protegido.
- [ ] 4.6 Registrar auditoria funcional e invalidar validação em cada alteração comercial.
- [ ] 4.7 Testar duplicidade, padrão, propagação, substituição, delete e códigos de erro.

## Sequenciamento

- Bloqueado por: 3.0
- Desbloqueia: 7.0, 8.0 e 10.0
- Paralelizável: Sim; pode evoluir junto com 5.0 e 6.0 após estabilização das APIs internas do agregado.

## Rastreabilidade

- Esta tarefa cobre: US-02 diretamente, US-01 como suporte e RF-01 integralmente no domínio/aplicação.
- Evidência esperada: comandos e testes provam os três critérios de aceite do RF-01 e os códigos `POLICY_TYPE_ALREADY_ACTIVE`, `REPLACEMENT_POLICY_REQUIRED` e `POLICY_DELETION_NOT_ALLOWED`.

## Detalhes de Implementação

Commands previstos: `CreateCommercialPolicyCommand`, `SetDefaultCommercialPolicyCommand`, `UpdateCommercialPolicyCommand` e `DeleteCommercialPolicyCommand`, com handlers CQRS nativos. Ator vem do JWT no endpoint e é passado ao Command; títulos, resumos e versão vêm exclusivamente do catálogo.

`SetDefaultPolicy` deve alterar padrão e possíveis associações na mesma transação, incrementar a revisão uma vez e retornar `updatedAccommodationCount`. A substituição na desativação deve reassociar acomodações e tarifas afetadas sem deixar referências para política inativa.

**Convenções da stack (das skills consultadas):**

- Handlers validam entrada com FluentValidation e deixam invariantes no agregado.
- Usar `InventoryDbContext` como Unit of Work, sem repository wrapper.
- Propagar `CancellationToken` para todas as consultas e `SaveChangesAsync`.
- Logs estruturados incluem `propertyId`, `offerRevision`, `operation` e `result`, sem texto jurídico.
- Testes xUnit + AwesomeAssertions seguem AAA e cobrem caminhos positivos e negativos.

## Critérios de Sucesso (Verificáveis)

- [ ] Testes passam: `dotnet test ../localizestay-backend/LocalizeStay.sln --filter "FullyQualifiedName~CommercialPolicyTests"`
- [ ] Build compila sem erros: `dotnet build ../localizestay-backend/LocalizeStay.sln --no-restore`
- [ ] Formatação válida: `dotnet format ../localizestay-backend/LocalizeStay.sln --verify-no-changes --no-restore`
- [ ] Tipo ativo duplicado retorna `POLICY_TYPE_ALREADY_ACTIVE`.
- [ ] Desativação em uso sem substituta retorna `REPLACEMENT_POLICY_REQUIRED`.
- [ ] Troca de padrão atualiza zero ou N acomodações conforme a decisão e incrementa a revisão uma única vez.
- [ ] Política enviada, padrão ou em uso não pode ser excluída fisicamente.

