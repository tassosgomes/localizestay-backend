# localizestay-backend

Monólito modular .NET da LocalizeStay: um único host ASP.NET Core que compõe nove módulos de
domínio (bounded contexts), cada um dono da sua própria lógica, schema PostgreSQL e API interna.

Decisões e guardrails que este código implementa estão documentadas no repositório
[`viajora-meta`](../viajora-meta): [Architecture Baseline](../viajora-meta/context/architecture-baseline.md),
[ADR-0001](../viajora-meta/docs/adr/ADR-0001-backend-dotnet-monolito-modular.md) (monólito modular
.NET), [ADR-0002](../viajora-meta/docs/adr/ADR-0002-postgresql-unico-adiamento-mongo-redis-broker.md)
(PostgreSQL único, outbox in-process, sem broker) e
[ADR-0009](../viajora-meta/docs/adr/ADR-0009-topologia-repositorios-gitops-portainer.md) (topologia
de repositórios e entrega).

## Estrutura

```
LocalizeStay.sln
src/
  LocalizeStay.Api/                        # Host único (ASP.NET Core minimal hosting); composition root
  BuildingBlocks/
    LocalizeStay.SharedKernel/             # Só capacidades técnicas — nenhuma semântica de negócio
  Modules/
    <Nome>/
      LocalizeStay.Modules.<Nome>/            # Domain + Application + Infrastructure (internal por padrão)
      LocalizeStay.Modules.<Nome>.Contracts/  # Único projeto público do módulo
tests/
  LocalizeStay.ArchitectureTests/          # NetArchTest.Rules — guardrails de fronteira automatizados
  LocalizeStay.UnitTests/                  # xUnit + AwesomeAssertions + Moq
```

Os nove módulos (bounded contexts do [domain map](../viajora-meta/context/domain-map.md)):

| Módulo (código) | Bounded context | Schema PostgreSQL |
|---|---|---|
| Discovery | Descoberta e Decisão | `discovery` |
| Inventory | Oferta e Inventário | `inventory` |
| Booking | Reserva | `booking` |
| Payments | Pagamentos e Financeiro | `payments` |
| CustomerCare | Atendimento e Mediação | `customer_care` |
| Curation | Curadoria e Qualidade | `curation` |
| Operations | Operação Interna | `operations` |
| IdentityAccess | Identidade e Acesso | `identity_access` |
| Insights | Métricas e Aprendizado | `insights` |

### Por que dois projetos por módulo

- `LocalizeStay.Modules.<Nome>` contém Domain, Application e Infrastructure. Suas classes são
  `internal` por padrão — só o próprio módulo pode usá-las, e o compilador garante isso.
- `LocalizeStay.Modules.<Nome>.Contracts` é a única superfície pública do módulo: DTOs, interfaces
  de API interna e eventos de integração. **Outros módulos só podem referenciar o Contracts** — nunca
  o projeto principal de outro módulo. O namespace do Contracts (`LocalizeStay.Contracts.<Nome>`) é
  deliberadamente separado de `LocalizeStay.Modules.<Nome>` para que os testes de arquitetura possam
  bloquear por namespace sem ambiguidade entre "o módulo" e "o contrato do módulo".
- Cada módulo tem seu próprio `DbContext`, com schema PostgreSQL próprio em snake_case e sua própria
  tabela de outbox (`<schema>.outbox_messages`). Não há chaves estrangeiras nem joins entre schemas de
  módulos diferentes — apenas identificadores estáveis validados por contrato.

### SharedKernel — só capacidades técnicas

`LocalizeStay.SharedKernel` não conhece nenhum módulo e nenhum módulo mistura sua lógica de negócio
ali. O que existe:

- **`Modules/IModule`** — contrato que cada módulo implementa para se registrar no host (DI +
  endpoints), sem o host precisar conhecer regras de negócio de nenhum módulo.
- **`Cqrs/`** — dispatcher CQRS nativo (`ICommand`/`IQuery`/handlers + `Dispatcher`), sem MediatR
  (ADR-0001). Handlers são descobertos por módulo via Scrutor, incluindo os `internal`.
- **`Events/` + `Outbox/`** — bus de eventos in-process (`IEventBus`) e outbox transacional
  (`OutboxMessage`, `OutboxProcessor<TDbContext>`) — publicador em background com retry limitado.
  Cada módulo grava seu próprio outbox na mesma transação da mudança de negócio; o processor de
  background lê, publica e marca como processado, sem depender de um broker (ADR-0002).
- **`Correlation/`** — correlation id ponta a ponta (`X-Correlation-Id`), disponível via
  `ICorrelationIdAccessor` e propagado para logs, traces e respostas de erro.
- **`Time/IClock`** — abstração de relógio para manter os módulos testáveis.
- **`ErrorHandling/`** — `DomainException` e subtipos (`NotFoundException`, `ConflictException`,
  `BusinessRuleViolationException`, `ExternalDependencyException`) + `GlobalExceptionHandler`,
  convertendo qualquer exceção em Problem Details (RFC 9457) sem vazar stack trace.
- **`Observability/`** — OpenTelemetry (traces, métricas, logs) com exportador OTLP opcional.
- **`HealthChecks/`** — `/health/live` (nunca depende de infraestrutura externa) e `/health/ready`
  (agrega os checks que cada módulo registra para sua própria dependência, ex.: banco).
- **`DependencyInjection/`** — `AddModuleDatabase<TDbContext>` (DbContext + outbox processor + health
  check, tudo em uma chamada) e `AddModuleHandlers` (scan de handlers/validators do próprio módulo).

### Exemplo mínimo de wiring

O módulo Inventory expõe um único endpoint trivial, `GET /api/inventory/status`, só para provar o
caminho completo host → módulo → dispatcher → handler, sem regra de negócio nenhuma. Os outros oito
módulos estão scaffolded (DbContext + registro no host) e aguardam sua primeira capacidade real.

## Rodando localmente

Pré-requisitos: .NET SDK 10 (`net10.0`), Docker.

```bash
# Sobe o PostgreSQL de desenvolvimento
docker compose -f docker-compose.dev.yml up -d

# Restaura, builda e roda a API
dotnet restore
dotnet build
dotnet run --project src/LocalizeStay.Api

# Endpoints úteis
curl http://localhost:5080/health/live
curl http://localhost:5080/health/ready
curl http://localhost:5080/api/inventory/status
```

A connection string padrão (`appsettings.json`, chave `ConnectionStrings:LocalizeStay`) aponta para o
Postgres do `docker-compose.dev.yml`. OpenTelemetry só exporta via OTLP se
`OpenTelemetry:OtlpEndpoint` estiver configurado; sem isso, a aplicação roda normalmente sem exportar
telemetria.

### Testes

```bash
dotnet test                                              # tudo
dotnet test tests/LocalizeStay.UnitTests                 # unitários
dotnet test tests/LocalizeStay.ArchitectureTests          # guardrails de arquitetura
dotnet test tests/LocalizeStay.IntegrationTests           # HTTP, PostgreSQL, contrato e fluxos F01
```

### Certificação F01 — incorporação de parceiros e propriedades

O contrato soberano é `.tasks/prd-incorporar-parceiros-e-propriedades/api-contract.yaml`. A suíte de
integração valida as 18 operações contra as rotas expostas e exercita o fluxo parceiro → incorporação
→ gates → pendências/comunicação → Curadoria → histórico/métricas em PostgreSQL via Testcontainers.

```bash
dotnet test tests/LocalizeStay.IntegrationTests --filter "FullyQualifiedName~ApiContractTests"
dotnet test tests/LocalizeStay.IntegrationTests --filter "FullyQualifiedName~PortfolioOnboardingEndToEndTests"
```

As variáveis não secretas, migration/rollback, smoke tests, telemetria e bloqueios externos de aceite
estão no [runbook de Portfolio Onboarding](docs/runbooks/portfolio-onboarding.md). Não há automação de
WhatsApp ou e-mail: a API registra somente o resultado que a equipe processou.

## Como os testes de arquitetura protegem as fronteiras

`tests/LocalizeStay.ArchitectureTests` usa [NetArchTest.Rules](https://github.com/BenMorris/NetArchTest)
para transformar os guardrails da baseline em testes que falham o build quando alguém tenta furar uma
fronteira:

1. **`ModuleBoundaryTests`** — nenhum módulo pode depender do namespace interno
   (`LocalizeStay.Modules.<Outro>`) de outro módulo; só do seu `Contracts`.
2. **`SharedKernelTests`** — `LocalizeStay.SharedKernel` não pode depender de nenhum módulo nem de
   nenhum Contracts.
3. **`ContractsTests`** — o `Contracts` de um módulo não pode depender da própria `Infrastructure`
   (nem de qualquer outro módulo).
4. **`EncapsulationTests`** — tipos em `Domain`, `Application` e `Infrastructure` de um módulo nunca
   podem ser públicos; só o `Contracts` é.

Essas quatro classes cobrem as quatro regras pedidas para o esqueleto. Antes de fixar o comportamento
final, cada uma foi validada tanto no caminho feliz (55 testes verdes) quanto injetando uma violação
real (uma dependência cruzada entre módulos) para confirmar que o teste realmente falha — não passa
só porque a asserção está mal escrita.

Além dos testes de arquitetura, `tests/LocalizeStay.UnitTests` cobre o dispatcher CQRS nativo, o bus
de eventos in-process e a extensão de registro de handlers por módulo (incluindo o caso de handlers
`internal`, que é o padrão em todo o código de módulo).

## CI/CD

`.github/workflows/ci.yml`: em PR e push para `main`, restaura/builda/testa (unitários +
arquitetura). Em push para `main`, adicionalmente builda a imagem Docker e publica no GHCR
(`ghcr.io/tassosgomes/localizestay-api`) com as tags `sha-<shortsha>` e `dev`, autenticando com
`GITHUB_TOKEN` (permissão `packages: write`). O deploy em si — quais tags rodam em qual ambiente — é
responsabilidade do repositório `localizestay-deploy` (ADR-0009); este repositório só publica a
imagem.
