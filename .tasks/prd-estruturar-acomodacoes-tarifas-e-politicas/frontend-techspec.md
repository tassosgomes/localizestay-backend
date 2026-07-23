# Especificação Técnica de Frontend — F02: Estruturar Acomodações, Tarifas e Políticas

> **PRD de origem:** `tasks/prd-estruturar-acomodacoes-tarifas-e-politicas/prd.md`  
> **API Contract:** `tasks/prd-estruturar-acomodacoes-tarifas-e-politicas/api-contract.yaml`  
> **TechSpec backend:** `tasks/prd-estruturar-acomodacoes-tarifas-e-politicas/techspec.md`  
> **Data:** 2026-07-22  
> **Status:** Aprovado

---

## Resumo Executivo

A F02 será implementada em `apps/backoffice` como a feature `commercial-offers`, sob `/portfolio/offers`. A solução reutiliza React Router, TanStack Query, React Hook Form, Zod, Orval, MSW, Vitest, Playwright, CSS Modules e os componentes acessíveis existentes.

Orval gerará tipos, schemas Zod, hooks TanStack Query e mocks MSW diretamente do contrato. O estado do servidor permanecerá no TanStack Query; filtros e paginação ficarão na URL; formulários e diálogos manterão estado local. O salvamento será explícito por seção, sem autosave ou optimistic update.

O trade-off principal é exigir confirmações de salvamento e refetch após mutations, aumentando algumas interações e requisições. Em troca, reduz-se o risco de sobrescrever trabalho concorrente e mantém-se `expectedRevision` alinhado ao agregado soberano do backend.

---

## Skills de Referência

| Skill | Caminho | Decisões Influenciadas |
|---|---|---|
| `flow-frontend-techspec-creator` | `.agents/skills/flow-frontend-techspec-creator/SKILL.md` | Processo, contrato soberano, inventário e ADRs |
| `react-architecture` | `/home/tsgomes/.agents/skills/react/react-architecture/SKILL.md` | Feature-based, public API, roteamento e aliases |
| `react-code-quality` | `/home/tsgomes/.agents/skills/react/react-code-quality/SKILL.md` | TypeScript estrito, naming, hooks e componentização |
| `react-testing` | `/home/tsgomes/.agents/skills/react/react-testing/SKILL.md` | Vitest, Testing Library, MSW e cobertura |
| `react-runtime-config` | `/home/tsgomes/.agents/skills/react/react-runtime-config/SKILL.md` | Reuso de `API_URL` em runtime |
| `react-production-readiness` | `/home/tsgomes/.agents/skills/react/react-production-readiness/SKILL.md` | LGPD, telemetria, CI e tratamento de erros |
| `design-patterns` | `/home/tsgomes/.agents/skills/common/design-patterns/SKILL.md` | Composição e ausência deliberada de abstrações GoF desnecessárias |

---

## Mapeamento User Story → Tela → Endpoint

| User Story | Tela / Componente | Endpoints Consumidos |
|---|---|---|
| Cadastrar acomodações e condições progressivamente | `CommercialOfferDetailPage`, `AccommodationEditorPage`, `RateFormDialog` | `GET /commercial-offers`, `GET /commercial-offer`, operações de `/accommodations` e `/rates` |
| Reutilizar políticas e definir uma padrão | `PolicyManager`, `PolicyFormDialog`, `SetDefaultPolicyDialog` | `GET/POST /commercial-policies`, `PUT /commercial-policies/default`, `PATCH/DELETE /commercial-policies/{policyId}` |
| Conferir preços, ocupação e políticas | `CommercialOfferReviewPanel`, `AccommodationReviewSection` | `GET /commercial-offer`, `GET /accommodations`, `GET /rates`, `POST /commercial-offer-validations`, `POST /commercial-offer-submissions` |
| Fornecer condições pelos canais atuais | Sem tela de parceiro; a Operação transcreve os dados recebidos | Mutations de políticas, acomodações e tarifas; registros do canal continuam pertencendo à F01 |
| Medir prazo, completude e retrabalho | `CommercialOfferMetricsPage` | `GET /commercial-offer-metrics`, `GET /commercial-offer-history` |

---

## Arquitetura de Frontend

### Estrutura de Pastas

~~~text
apps/backoffice/src/
├── features/
│   └── commercial-offers/
│       ├── api/
│       │   ├── generated/
│       │   └── commercialOfferProblemFeedback.ts
│       ├── components/
│       ├── forms/
│       ├── hooks/
│       ├── pages/
│       ├── validation/
│       └── index.ts
├── shared/
│   ├── api/
│   └── components/ui/
└── test/mocks/
~~~

A feature não importará caminhos internos de `portfolio-onboarding`. Capacidades técnicas compartilhadas serão reutilizadas por `shared`.

### Roteamento

| Rota | Componente | Layout | Auth |
|---|---|---|:---:|
| `/portfolio/offers` | `CommercialOfferQueuePage` | `StaffLayout` | ✅ |
| `/portfolio/offers/metrics` | `CommercialOfferMetricsPage` | `StaffLayout` | ✅ |
| `/portfolio/offers/:propertyId` | `CommercialOfferDetailPage` | `StaffLayout` | ✅ |
| `/portfolio/offers/:propertyId/accommodations/new` | `AccommodationEditorPage` | `StaffLayout` | ✅ |
| `/portfolio/offers/:propertyId/accommodations/:accommodationId` | `AccommodationEditorPage` | `StaffLayout` | ✅ |

O segmento `/portfolio/offers` será carregado com `lazy()`. Tarifas serão editadas em diálogo dentro da acomodação, sem rota adicional.

### Hierarquia de Componentes

~~~text
StaffLayout
├── CommercialOfferQueuePage
│   ├── CommercialOfferFilters
│   ├── CommercialOfferTable
│   └── Pagination
├── CommercialOfferDetailPage
│   ├── OfferSummary
│   ├── PendingIssueList
│   ├── PolicyManager
│   ├── AccommodationTable
│   ├── CommercialOfferReviewPanel
│   └── OfferHistoryTimeline
├── AccommodationEditorPage
│   ├── AccommodationForm
│   ├── RateTable
│   └── RateFormDialog
└── CommercialOfferMetricsPage
    └── CommercialOfferMetricCards
~~~

Componentes apresentacionais não executarão fetching.

---

## Geração de Tipos do API Contract

### Ferramenta Escolhida

- **Ferramenta:** Orval `8.22.x`.
- **Saídas:** React Query, tipos, Zod e MSW.
- **Modo:** `tags-split`.
- **Destino:** `apps/backoffice/src/features/commercial-offers/api/generated/`.
- **Regeneração:** manual no desenvolvimento e obrigatória no CI.
- **Versionamento:** código gerado commitado.
- **Drift:** CI regenera F01 e F02 e falha se houver diff.

~~~bash
cd ../localizestay-frontend
pnpm --filter @localizestay/backoffice generate:api
~~~

`orval.config.ts` manterá o target da F01 e adicionará targets próprios para F02. O CI fornecerá `COMMERCIAL_OFFERS_API_CONTRACT_PATH` sem alterar `API_URL` de runtime.

### Tipos Gerados Reutilizados

| Schema | Uso |
|---|---|
| `CommercialOfferSummary`, `CommercialOfferListResponse` | Fila operacional |
| `CommercialOffer` | Resumo, revisão, validação e pendências |
| `CommercialPolicy` e requests relacionados | Gestão de políticas |
| `Accommodation` e requests relacionados | Formulário e listagem de acomodações |
| `CommercialRate` e requests relacionados | Grade e formulário tarifário |
| `OfferValidation`, `OfferSubmission`, `OfferHistoryEntry` | Workflow e histórico |
| `CommercialOfferMetrics` | Indicadores gerenciais |
| `PendingIssue` | Navegação para campos bloqueantes |
| `ProblemDetails`, `ValidationError` | Erros de formulário e feedback |

Nenhum DTO HTTP será escrito manualmente. Tipos locais serão permitidos apenas para estado visual, como valor monetário formatado.

---

## Estratégia de Fetching

### Biblioteca

- **TanStack Query:** `5.101.x`.
- **Cliente:** `useApiMutator` e `createHttpClient` existentes.
- **Token:** `@logto/react`.
- **Cancelamento:** `AbortSignal` propagado.
- **Queries:** retry máximo de duas tentativas apenas para transporte, 5xx e 429.
- **Mutations:** sem retry automático e sem optimistic update.

### Endpoints → Hooks

| operationId | Hook |
|---|---|
| `listCommercialOffers` | `useListCommercialOffers` |
| `getCommercialOffer` | `useGetCommercialOffer` |
| `listCommercialPolicies` | `useListCommercialPolicies` |
| `createCommercialPolicy` | `useCreateCommercialPolicy` |
| `setDefaultCommercialPolicy` | `useSetDefaultCommercialPolicy` |
| `updateCommercialPolicy` | `useUpdateCommercialPolicy` |
| `deleteCommercialPolicy` | `useDeleteCommercialPolicy` |
| `listAccommodations` | `useListAccommodations` |
| `createAccommodation` | `useCreateAccommodation` |
| `getAccommodation` | `useGetAccommodation` |
| `updateAccommodation` | `useUpdateAccommodation` |
| `deleteAccommodation` | `useDeleteAccommodation` |
| `listCommercialRates` | `useListCommercialRates` |
| `createCommercialRate` | `useCreateCommercialRate` |
| `updateCommercialRate` | `useUpdateCommercialRate` |
| `deleteCommercialRate` | `useDeleteCommercialRate` |
| `createCommercialOfferValidation` | `useCreateCommercialOfferValidation` |
| `createCommercialOfferSubmission` | `useCreateCommercialOfferSubmission` |
| `listCommercialOfferHistory` | `useListCommercialOfferHistory` |
| `getCommercialOfferMetrics` | `useGetCommercialOfferMetrics` |

### Cache e Invalidação

- `staleTime`: 30 segundos para fila, detalhe e recursos.
- Métricas: 60 segundos.
- `gcTime`: 5 minutos.
- Mutation comercial invalida oferta, fila, recurso afetado, histórico e métricas.
- Validação invalida oferta, fila e histórico.
- Envio invalida oferta, fila, histórico e métricas.
- Após mutation, a revisão será obtida por refetch da oferta.
- `REVISION_MISMATCH` preserva o formulário local, recarrega o servidor e exige nova confirmação.
- O envio reutiliza a mesma `Idempotency-Key` apenas durante a mesma tentativa manual.

### Tratamento Centralizado de Erros

| `code` | Comportamento |
|---|---|
| `BAD_REQUEST` | Associar `errors[].field` ao formulário |
| `UNAUTHORIZED` | Renovar sessão ou iniciar login |
| `FORBIDDEN` | Bloquear ação e apresentar mensagem |
| `*_NOT_FOUND` | Estado de recurso inexistente |
| `REVISION_MISMATCH` | Recarregar dados e informar edição concorrente |
| `POLICY_TYPE_ALREADY_ACTIVE` | Destacar tipo já cadastrado |
| `RATE_PERIOD_OVERLAP` | Destacar período e recurso conflitante |
| `INVALID_OCCUPANCY_CONFIGURATION` | Associar erros à ocupação/camas |
| `REPLACEMENT_POLICY_REQUIRED` | Exigir substituta e motivo |
| `*_DELETION_NOT_ALLOWED` | Direcionar para desativação |
| `OFFER_NOT_READY` | Exibir e focar `pendingIssues` bloqueantes |
| `SELF_VALIDATION_NOT_ALLOWED` | Desabilitar validação para o autor |
| `VALIDATION_REQUIRED` | Recarregar revisão e validação |
| `PUBLISHED_OFFER_CHANGE_REQUIRES_F04` | Tornar tela somente leitura e orientar F04 |
| `IDEMPOTENCY_KEY_REUSED` | Gerar nova chave somente após abandonar a tentativa |
| `RATE_LIMIT_EXCEEDED` | Respeitar `Retry-After` |
| `INTERNAL_ERROR` | Mensagem genérica e `traceId` |

Códigos específicos permanecerão em um mapper da feature, delegando erros transversais ao mapper compartilhado.

---

## Gerenciamento de Estado

### Server State

TanStack Query gerenciará ofertas, políticas, acomodações, tarifas, validação, histórico e métricas.

### Client State

| Estado | Onde vive | Motivo |
|---|---|---|
| Filtros, ordenação e paginação | URL search params | Links reproduzíveis e refresh seguro |
| Dados do formulário | React Hook Form | Estado local e isolado |
| Diálogos e expansão | `useState` | Efêmero |
| Revisão atual | Resposta de `getCommercialOffer` | Backend soberano |
| `Idempotency-Key` | Hook local estável | Reuso apenas na tentativa atual |
| Sessão e ator | `@logto/react` | Comparação com `authoredBy` |
| Dados da oferta | TanStack Query | Sem cópia em store global |

Redux, Zustand ou outro store global não serão introduzidos.

---

## Validação de Formulários

### Biblioteca

- React Hook Form `7.82.x`.
- Zod `4.4.x`.
- `@hookform/resolvers` `5.4.x`.
- Schemas básicos gerados pelo Orval.

### Refinamentos Locais

- `maxAdults + maxChildren <= totalCapacity`.
- Faixa infantil com mínimo menor ou igual ao máximo.
- `validFrom <= validTo`.
- Hóspedes incluídos compatíveis com a capacidade informada.
- Desativação exige motivo.
- Política em uso exige substituta diferente.
- Troca de política padrão exige escolha explícita sobre acomodações existentes.
- Valores BRL são convertidos para centavos sem ponto flutuante.
- Datas tarifárias permanecem `date`, sem conversão de fuso.
- Regras de completude final não serão duplicadas; `pendingIssues` do servidor permanece soberano.

### Regras de Domínio

| Regra | Aplicação no frontend |
|---|---|
| RN-01 | Rotas protegidas para staff |
| RN-07 | Pendências bloqueantes e gate de validação |
| RN-10 | Invalidação visível e oferta publicada somente leitura |
| RN-11 | Apenas Flexível e Não-Reembolsável |
| RN-12/RN-13 | Texto jurídico somente leitura vindo da API |
| RN-14 | Prazos exibidos na fila e nas métricas |

---

## Mocks e Ambiente de Desenvolvimento

- **Desenvolvimento sem backend:** Prism.
- **Testes unitários/integração:** MSW gerado pelo Orval, com overrides.
- **E2E:** MSW stateful no build de teste.
- **Conformidade HTTP:** smoke separado com Prism.

~~~bash
cd /home/tsgomes/github-tassosgomes/viajora-meta
npx @stoplight/prism-cli mock \
  tasks/prd-estruturar-acomodacoes-tarifas-e-politicas/api-contract.yaml \
  -p 4010
~~~

O backoffice usará `API_URL=http://localhost:4010/api/v1`.

---

## Inventário de Artefatos

### Arquivos a Criar

| Caminho | Tipo | Skills | Descrição |
|---|---|---|---|
| `apps/backoffice/src/features/commercial-offers/api/generated/**` | Código gerado | flow | Tipos, hooks, Zod e MSW |
| `.../api/commercialOfferProblemFeedback.ts` | Utility | code-quality | Erros específicos da F02 |
| `.../pages/CommercialOfferQueuePage.tsx` | Page | architecture | Fila de ofertas |
| `.../pages/CommercialOfferDetailPage.tsx` | Page | architecture | Políticas, acomodações, revisão e histórico |
| `.../pages/AccommodationEditorPage.tsx` | Page | architecture | Ocupação e tarifas |
| `.../pages/CommercialOfferMetricsPage.tsx` | Page | architecture | Indicadores |
| `.../components/CommercialOfferFilters.tsx` | Component | architecture | Filtros da fila |
| `.../components/CommercialOfferTable.tsx` | Component | accessibility | Tabela responsiva |
| `.../components/CommercialOfferStatus.tsx` | Component | accessibility | Estado por texto e ícone |
| `.../components/OfferSummary.tsx` | Component | architecture | Revisão, autoria e completude |
| `.../components/PendingIssueList.tsx` | Component | accessibility | Pendências acionáveis |
| `.../components/PolicyManager.tsx` | Component | architecture | Políticas e padrão |
| `.../components/AccommodationTable.tsx` | Component | accessibility | Lista de acomodações |
| `.../components/RateTable.tsx` | Component | accessibility | Tarifas em BRL |
| `.../components/CommercialOfferReviewPanel.tsx` | Component | accessibility | Validação e envio |
| `.../components/OfferHistoryTimeline.tsx` | Component | accessibility | Histórico funcional |
| `.../components/CommercialOfferMetricCards.tsx` | Component | accessibility | Percentuais com denominadores |
| `.../forms/CommercialPolicyFormDialog.tsx` | Form | code-quality | Nova política |
| `.../forms/SetDefaultPolicyDialog.tsx` | Form | accessibility | Troca e propagação |
| `.../forms/DeactivatePolicyDialog.tsx` | Form | accessibility | Substituição e motivo |
| `.../forms/AccommodationForm.tsx` | Form | code-quality | Dados e ocupação |
| `.../forms/RateFormDialog.tsx` | Form | code-quality | Tarifa progressiva |
| `.../forms/DeactivateResourceDialog.tsx` | Form | accessibility | Desativação de acomodação/tarifa |
| `.../forms/OfferValidationDialog.tsx` | Form | accessibility | Segunda validação |
| `.../forms/OfferSubmissionDialog.tsx` | Form | accessibility | Envio idempotente |
| `.../hooks/useCommercialOfferFilters.ts` | Hook | architecture | URL state da fila |
| `.../hooks/useAccommodationFilters.ts` | Hook | architecture | URL state das acomodações |
| `.../hooks/useStableSubmissionKey.ts` | Hook | code-quality | Chave da tentativa de envio |
| `.../validation/refinements.ts` | Validation | code-quality | Regras condicionais |
| `.../validation/formMappers.ts` | Mapper | code-quality | BRL, datas e requests gerados |
| `.../index.ts` | Public API | architecture | Exportações da feature |
| `.../**/*.module.css` | Style | DESIGN.md | Estilos com tokens existentes |
| `.../**/*.test.{ts,tsx}` | Test | react-testing | Unitários e integração |
| `apps/backoffice/src/test/mocks/commercialOfferScenarios.ts` | Mock | react-testing | Cenários stateful |
| `apps/backoffice/e2e/commercial-offers.spec.ts` | E2E | react-testing | Jornadas RF-01 a RF-06 |
| `apps/backoffice/e2e/commercial-offers-accessibility.spec.ts` | E2E/a11y | react-testing | WCAG automatizado |

### Arquivos a Modificar

| Caminho | Skills | Alteração |
|---|---|---|
| `apps/backoffice/orval.config.ts` | flow | Adicionar targets F02 |
| `apps/backoffice/src/router/routes.tsx` | architecture | Registrar `/portfolio/offers` |
| `apps/backoffice/src/shared/components/layout/StaffLayout.tsx` | architecture | Adicionar navegação |
| `apps/backoffice/src/test/mocks/e2eHandlers.ts` | testing | Registrar handlers F02 |
| `apps/backoffice/vitest.config.ts` | testing | Gate de 80% para a feature |
| `.github/workflows/ci.yml` | production-readiness | Gerar e verificar ambos os contratos |
| `README.md` | runtime-config | Documentar geração e Prism |

Não haverá nova variável de runtime nem nova dependência de produção.

### Arquivos de Referência

| Caminho | Motivo |
|---|---|
| `tasks/prd-estruturar-acomodacoes-tarifas-e-politicas/api-contract.yaml` | Fonte soberana |
| `tasks/prd-estruturar-acomodacoes-tarifas-e-politicas/techspec.md` | Revisão, concorrência e backend |
| `domains/oferta-inventario/domain.md` | Vocabulário e regras |
| `apps/backoffice/src/app/providers/QueryProvider.tsx` | Política de cache/retry |
| `apps/backoffice/src/shared/api/*` | Cliente e erros |
| `apps/backoffice/src/shared/components/ui/*` | Primitives acessíveis |
| `apps/backoffice/src/features/portfolio-onboarding/**` | Convenção feature-based |
| `DESIGN.md` | Tokens visuais |

---

## Acessibilidade

- WCAG 2.2 AA.
- Estados combinam texto, ícone e estilo.
- Navegação completa por teclado.
- Foco devolvido ao acionador após diálogos.
- Erros associados via `aria-describedby`.
- Feedback de salvamento em `aria-live`.
- Pendências levam foco ao campo correspondente.
- Tabelas semânticas com representação responsiva.
- Confirmação explícita para exclusão, desativação, validação e envio.
- Axe no Playwright e teste manual de teclado/leitor de tela.

---

## Internacionalização

- MVP somente PT-BR.
- Sem nova biblioteca de i18n.
- Código e identificadores em inglês.
- BRL via `Intl.NumberFormat('pt-BR', { currency: 'BRL' })`.
- Instantes no fuso operacional `America/Sao_Paulo`.
- Datas tarifárias `date` exibidas sem conversão de timezone.

---

## Análise de Impacto

| Componente | Impacto | Risco | Ação |
|---|---|---|---|
| Backoffice | Alto | Nova jornada comercial | Implementar feature isolada |
| Router e navegação | Médio | Novas rotas protegidas | Lazy loading e testes |
| Orval | Alto | Dois contratos e diretórios gerados | Targets isolados e teste de drift |
| CI | Médio | Dois paths de contrato | Regenerar ambos |
| Cliente HTTP | Referência | RFC 9457 e headers | Reutilizar sem alterar |
| Design system local | Referência | Mais formulários/tabelas | Reutilizar primitives |
| Backend | Referência | Concorrência por revisão | Tratar 409/422 |
| F01 | Baixo | Compartilha layout e infraestrutura | Sem importar domínio F01 |

---

## Abordagem de Testes

### Testes Unitários

- Filtros e serialização da URL.
- Conversão BRL ↔ centavos.
- Datas e refinamentos Zod.
- Mapeamento de erros.
- Chave idempotente.
- Cobertura mínima de 80% em statements, branches, functions e lines.

### Testes de Integração

Testing Library + MSW:

- loading, empty e erro;
- salvamento progressivo;
- troca/desativação de política;
- ocupação incoerente;
- sobreposição tarifária;
- conflito de revisão;
- autovalidação bloqueada;
- invalidação após alteração;
- envio e retry manual com a mesma chave;
- oferta devolvida e publicada somente leitura.

### Testes E2E

Playwright:

- política → acomodação → tarifa;
- correção de pendências;
- validação por segundo operador;
- envio;
- devolução e reedição;
- métricas e histórico;
- jornada WCAG automatizada.

### Testes de Contrato

~~~bash
pnpm --filter @localizestay/backoffice generate:api
git diff --exit-code -- \
  apps/backoffice/src/features/portfolio-onboarding/api/generated \
  apps/backoffice/src/features/commercial-offers/api/generated
~~~

---

## Sequenciamento de Desenvolvimento

### Build Order

1. Adaptar Orval e CI para múltiplos contratos.
2. Gerar tipos, hooks, Zod e MSW da F02.
3. Criar mappers, refinamentos, erros e filtros.
4. Criar componentes e formulários apresentacionais.
5. Implementar fila e métricas.
6. Implementar detalhe, políticas e acomodações.
7. Implementar tarifas, validação, envio e histórico.
8. Integrar cenários MSW.
9. Registrar rotas e navegação.
10. Executar testes unitários, integração, E2E, acessibilidade e drift.

### Dependências Técnicas Bloqueantes

- Aprovação formal do contrato OpenAPI, atualmente marcado como “Em revisão”.
- Disponibilidade do backend não bloqueia início, pois Prism/MSW permitem desenvolvimento.
- Ratificação operacional das permissões `read`, `write`, `review` e `metrics`.

---

## Performance

- Lazy loading de `/portfolio/offers`.
- Paginação no servidor.
- Cancelamento de queries ao trocar rota/filtros.
- Sem prefetch indiscriminado.
- Sem memoização preventiva.
- Grade tarifária carregada apenas para a acomodação aberta.
- Revisão carrega tarifas por acomodação sob demanda, evitando N+1 na tela inicial.
- Nenhuma nova dependência de runtime.

---

## Considerações Técnicas

### Decisões Principais

- Feature isolada `commercial-offers`.
- Orval com targets independentes por contrato.
- Salvamento explícito por seção.
- Sem autosave, store global ou optimistic update.
- `expectedRevision` sempre derivado do último refetch.
- React Hook Form com Zod gerado e refinamentos mínimos.
- URL como fonte de filtros/paginação.
- PT-BR sem infraestrutura prematura de i18n.

### Riscos Conhecidos

- **Edição concorrente:** mitigar com `expectedRevision`, refetch e confirmação.
- **Drift entre contratos:** mitigar com geração e diff no CI.
- **Orval limpar saída da outra feature:** configurar diretórios e `clean` isoladamente.
- **Formulário bloquear rascunho:** separar validade sintática de prontidão comercial.
- **Revisão com muitas tarifas:** carregamento sob demanda e paginação.
- **Permissões não expostas à UI:** backend permanece soberano; mostrar 403 seguro até decisão.
- **Contrato ainda em revisão:** não iniciar integração definitiva sem aprovação.

### Conformidade com Skills

| Decisão | Skill | Conforme? |
|---|---|:---:|
| Feature-based e public API | `react-architecture` | ✅ |
| TypeScript estrito e componentes pequenos | `react-code-quality` | ✅ |
| Vitest, RTL, MSW e 80% | `react-testing` | ✅ |
| `API_URL` em runtime | `react-runtime-config` | ✅ |
| Tipos/hooks derivados do contrato | `flow-frontend-techspec-creator` | ✅ |
| CSS Modules e tokens existentes | Codebase/DESIGN.md | ✅ |
| Sem padrão GoF adicional | `design-patterns`/YAGNI | ✅ |

---

## Questões em Aberto

- [ ] Aprovar formalmente o API Contract.
- [ ] Confirmar se todos os usuários com escopo `staff` receberão as quatro permissões da F02 ou se a UI deverá obter permissões finas.
- [ ] Confirmar os textos jurídicos/versionamento das políticas antes de operar dinheiro real.
- [ ] Validar se o carregamento sob demanda das tarifas atende à conferência operacional. Uma projeção consolidada exigiria atualização do contrato via `flow-contract-creator`.
- [x] Nenhum conflito direto entre PRD e contrato foi identificado.

---

## Architecture Decision Records

### Herdadas

- [ADR-001](adrs/adr-001.md) — propriedade incorporada canônica.
- [ADR-002](adrs/adr-002.md) — oferta comercial como agregado.
- [ADR-0001 global](../../docs/adr/ADR-0001-backend-dotnet-monolito-modular.md) — monólito modular.
- [ADR-0002 global](../../docs/adr/ADR-0002-postgresql-unico-adiamento-mongo-redis-broker.md) — PostgreSQL e infraestrutura enxuta.
- [ADR-0006 global](../../docs/adr/ADR-0006-logto-provedor-identidade.md) — LogTo.
- [ADR-0007 global](../../docs/adr/ADR-0007-observabilidade-otel-grafanacloud.md) — OpenTelemetry.
- [ADR-0010 global](../../docs/adr/ADR-0010-autorizacao-local-ecad-authz-como-referencia.md) — autorização local.
- [ADR F01-005](../prd-incorporar-parceiros-e-propriedades/adrs/adr-005.md) — Orval contract-first.
- [ADR F01-006](../prd-incorporar-parceiros-e-propriedades/adrs/adr-006.md) — separação de estado.

### Criadas nesta sessão

- [ADR-003: Estender a integração frontend contract-first por feature com Orval](adrs/adr-003.md) — geração isolada por contrato e drift no CI.
- [ADR-004: Usar salvamento explícito e separar server, URL e form state](adrs/adr-004.md) — estado sem store global, autosave ou optimistic update.

---

## Próximos Passos

1. Use a skill `flow-task-creator` referenciando este `frontend-techspec.md`.
2. Gere o cliente:

   ~~~bash
   cd ../localizestay-frontend
   pnpm --filter @localizestay/backoffice generate:api
   ~~~

3. Suba o mock:

   ~~~bash
   npx @stoplight/prism-cli mock \
     tasks/prd-estruturar-acomodacoes-tarifas-e-politicas/api-contract.yaml \
     -p 4010
   ~~~

4. Resolva os itens de “Questões em Aberto” antes ou durante os incrementos correspondentes.
