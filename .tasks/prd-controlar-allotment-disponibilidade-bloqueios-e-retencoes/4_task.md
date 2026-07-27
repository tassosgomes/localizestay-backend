---
status: pending
parallelizable: true
blocked_by: []
---

<task_context>
<domain>inventory/integration/curation-contracts</domain>
<type>integration</type>
<scope>configuration</scope>
<complexity>low</complexity>
<dependencies>external_apis</dependencies>
<unblocks>"25.0, 26.0"</unblocks>
<vertical_slice>Os três eventos de curadoria existem como schemas versionados que o módulo Inventory pode consumir.</vertical_slice>
</task_context>

# Tarefa 4.0: Declarar os contratos de eventos de curadoria

## Relacionada às User Stories

- [US-03] Bloquear datas ao receber aviso de indisponibilidade (suporte — a suspensão de curadoria é o caso equivalente vindo de D06)

## Visão Geral

RF-05 exige interromper a venda de uma propriedade quando D06 comunica suspensão, e restabelecê-la quando a aprovação volta a vigorar. D06 ainda não existe como módulo implementado e **não há publicador**. Os contratos existem para que o consumidor seja escrito contra um schema versionado, conforme ADR-002.

## Requisitos

- Declarar `CurationPropertyApprovedV1`, `CurationPropertySuspendedV1` e `CurationContentApprovedV1` em `LocalizeStay.Modules.Curation.Contracts` — **nunca** no módulo Inventory.
- Payload **mínimo**: apenas o que a F03 consome. Acrescentar campo depois é compatível; renomear ou remover não é.
- Cada evento herda de `IntegrationEvent` e carrega `EventId` para consumo idempotente.
- O projeto `LocalizeStay.Modules.Inventory.csproj` passa a referenciar `Curation.Contracts`.
- Os testes de arquitetura devem continuar aprovando: contratos são públicos, o módulo continua sem referenciar o assembly de implementação de Curation.

## Arquivos Envolvidos

- **Criar:**
  - `../localizestay-backend/src/Modules/Curation/LocalizeStay.Modules.Curation.Contracts/CurationSellabilityEvents.cs`
- **Modificar:**
  - `../localizestay-backend/src/Modules/Inventory/LocalizeStay.Modules.Inventory/LocalizeStay.Modules.Inventory.csproj` (referência a `Curation.Contracts`)
  - `../localizestay-backend/tests/LocalizeStay.ArchitectureTests/ContractsTests.cs` (cobrir os três novos contratos)
- **Referência:**
  - `../localizestay-backend/src/Modules/Curation/LocalizeStay.Modules.Curation.Contracts/` (formato de `CurationOfferReturnedV1`, criado pela F02)
  - `../localizestay-backend/src/BuildingBlocks/LocalizeStay.SharedKernel/Events/IntegrationEvent.cs`
  - `.tasks/prd-controlar-allotment-disponibilidade-bloqueios-e-retencoes/adrs/adr-002.md`
- **Skills para consultar durante implementação:**
  - `dotnet-architecture` — contratos em assembly próprio, fronteira de módulo
  - `dotnet-code-quality` — records imutáveis, nomes em inglês

## Subtarefas

- [ ] 4.1 Declarar os três records `V1` com payload mínimo, herdando de `IntegrationEvent`.
- [ ] 4.2 Referenciar `Curation.Contracts` no csproj do Inventory.
- [ ] 4.3 Estender `ContractsTests` para provar que os três tipos são públicos, imutáveis e versionados com sufixo `V1`.

## Sequenciamento

- Bloqueado por: Nenhum
- Desbloqueia: 25.0, 26.0
- Paralelizável: Sim; cria um arquivo novo em outro módulo e altera um csproj.

## Rastreabilidade

- Esta tarefa cobre: os três eventos consumidos declarados no contrato (`x-domain-events.consumes`) e a fronteira com D06 exigida por ADR-002.
- Evidência esperada: `ContractsTests` verde e os consumidores de 25.0/26.0 compilando contra os tipos.

## Detalhes de Implementação

Payload mínimo sugerido:

```csharp
public sealed record CurationPropertyApprovedV1(Guid PropertyId, DateTimeOffset ApprovedAt) : IntegrationEvent;

public sealed record CurationPropertySuspendedV1(Guid PropertyId, string Reason, DateTimeOffset SuspendedAt) : IntegrationEvent;

public sealed record CurationContentApprovedV1(Guid PropertyId, DateTimeOffset ApprovedAt) : IntegrationEvent;
```

Nomes dos eventos no barramento seguem o padrão do Domain Doc: `curadoria-qualidade.propriedade-aprovada`, `curadoria-qualidade.propriedade-suspensa` e `curadoria-qualidade.conteudo-aprovado`.

Enquanto D06 não publicar, os gates correspondentes são alimentados pela allowlist de configuração da tarefa 17.0, com default `blocked`. **A ausência de uma propriedade na allowlist nunca significa aprovação.**

**Convenções da stack (das skills consultadas):**

- Contratos de integração vivem em `*.Contracts`, são públicos e imutáveis (`dotnet-architecture`).
- Records posicionais, nomes em inglês, sufixo de versão explícito (`dotnet-code-quality`).
- Nenhum join, FK ou referência ao assembly de implementação de outro módulo.

## Critérios de Sucesso (Verificáveis)

- [ ] Testes passam: `dotnet test ../localizestay-backend/tests/LocalizeStay.ArchitectureTests`
- [ ] Build compila sem erros: `dotnet build ../localizestay-backend/LocalizeStay.sln --no-restore`
- [ ] Formatação válida: `dotnet format ../localizestay-backend/LocalizeStay.sln --verify-no-changes --no-restore`
- [ ] Os três tipos são públicos, `sealed record` e herdam de `IntegrationEvent`.
- [ ] O módulo Inventory referencia apenas `Curation.Contracts`, nunca `LocalizeStay.Modules.Curation`.
