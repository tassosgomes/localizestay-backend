# API Contract — Estruturar Acomodações, Tarifas e Políticas

> **Gerado a partir de:** `tasks/prd-estruturar-acomodacoes-tarifas-e-politicas/prd.md`  
> **Data:** 2026-07-22  
> **Status:** Em revisão  
> **Versão:** 1.0.0

## Premissas e decisões

| Decisão | Escolha | Motivo |
|---|---|---|
| Autenticação | JWT Bearer emitido pelo LogTo | Padrão vigente da plataforma |
| Escopo | `staff` em todos os endpoints | F02 é exclusiva da Operação no MVP |
| Permissões | `commercial-offers:read`, `write`, `review`, `metrics` | Separa consulta, manutenção, dupla validação e indicadores |
| Versionamento | `/api/v1` | Compatível com o contrato de F01 |
| Nomenclatura | Paths em inglês/plural/kebab-case; JSON em camelCase | Consistência entre os clientes e a API |
| Paginação | `_page` e `_size`, máximo 100 | Padrão REST do projeto |
| Erros | RFC 9457, `application/problem+json`, com `code` e `traceId` | Erros rastreáveis e tratáveis pelo frontend |
| Dinheiro | Inteiro em centavos e moeda fixa `BRL` | Evita ponto flutuante e múltiplas moedas |
| Datas | ISO 8601; instantes em UTC e períodos tarifários em `date` | Separa instantes globais de diárias locais |
| Políticas | Tipos fixos e textos jurídicos versionados no servidor | Impede alteração acidental de regras aprovadas |
| Rascunhos | Acomodação exige inicialmente só nome; tarifa exige nome e conditionCode | Permite salvamento progressivo sem perder trabalho |
| Concorrência | Escritas sensíveis recebem `expectedRevision` | Evita validar ou enviar uma revisão obsoleta |
| Exclusão | Hard delete apenas antes do primeiro envio; depois, desativação com motivo | Preserva histórico e rastreabilidade |
| Validação | Recurso imutável criado por operador diferente do autor | Garante dupla validação em 100% dos envios |
| Invalidação | Qualquer mudança em preço, ocupação, política ou período incrementa a revisão | Exige nova conferência após alteração relevante |
| Envio | `Idempotency-Key`, snapshot e outbox transacional | Evita duplicidade e publica o evento com segurança |
| Integrações | Sem upload, webhook ou health endpoint nesta feature | WhatsApp/e-mail permanecem humanos e infraestrutura é transversal |
| SLA de comunicação | Reutiliza os registros de comunicação processada de F01 | Evita duplicar a captura de WhatsApp/e-mail entre capacidades |

## Permissões declarativas

| Permissão | Finalidade |
|---|---|
| `commercial-offers:read` | Consultar fila, oferta, políticas, acomodações, tarifas e histórico |
| `commercial-offers:write` | Criar, atualizar, desativar, excluir e enviar ofertas |
| `commercial-offers:review` | Realizar a segunda validação independente |
| `commercial-offers:metrics` | Consultar indicadores consolidados |

## Resumo de endpoints

Todos exigem JWT LogTo com escopo `staff`.

| Método | Path | Descrição | Permissão | Status principais |
|---|---|---|---|---|
| `GET` | `/api/v1/commercial-offers` | Listar fila de ofertas | `read` | 200, 400, 401, 403, 404, 422, 429, 500 |
| `GET` | `/api/v1/properties/{propertyId}/commercial-offer` | Consultar oferta completa | `read` | 200, 400, 401, 403, 404, 422, 429, 500 |
| `GET` | `/api/v1/properties/{propertyId}/commercial-policies` | Listar políticas | `read` | 200, 400, 401, 403, 404, 422, 429, 500 |
| `POST` | `/api/v1/properties/{propertyId}/commercial-policies` | Cadastrar política | `write` | 201, 400, 401, 403, 404, 409, 422, 429, 500 |
| `PUT` | `/api/v1/properties/{propertyId}/commercial-policies/default` | Definir política padrão | `write` | 200, 400, 401, 403, 404, 409, 422, 429, 500 |
| `PATCH` | `/api/v1/properties/{propertyId}/commercial-policies/{policyId}` | Atualizar/desativar política | `write` | 200, 400, 401, 403, 404, 409, 422, 429, 500 |
| `DELETE` | `/api/v1/properties/{propertyId}/commercial-policies/{policyId}` | Excluir política nunca enviada | `write` | 204, 400, 401, 403, 404, 409, 422, 429, 500 |
| `GET` | `/api/v1/properties/{propertyId}/accommodations` | Listar acomodações | `read` | 200, 400, 401, 403, 404, 422, 429, 500 |
| `POST` | `/api/v1/properties/{propertyId}/accommodations` | Criar acomodação em rascunho | `write` | 201, 400, 401, 403, 404, 409, 422, 429, 500 |
| `GET` | `/api/v1/properties/{propertyId}/accommodations/{accommodationId}` | Consultar acomodação | `read` | 200, 400, 401, 403, 404, 422, 429, 500 |
| `PATCH` | `/api/v1/properties/{propertyId}/accommodations/{accommodationId}` | Atualizar/desativar acomodação | `write` | 200, 400, 401, 403, 404, 409, 422, 429, 500 |
| `DELETE` | `/api/v1/properties/{propertyId}/accommodations/{accommodationId}` | Excluir acomodação nunca enviada | `write` | 204, 400, 401, 403, 404, 409, 422, 429, 500 |
| `GET` | `/api/v1/properties/{propertyId}/accommodations/{accommodationId}/rates` | Listar tarifas | `read` | 200, 400, 401, 403, 404, 422, 429, 500 |
| `POST` | `/api/v1/properties/{propertyId}/accommodations/{accommodationId}/rates` | Criar tarifa em rascunho | `write` | 201, 400, 401, 403, 404, 409, 422, 429, 500 |
| `PATCH` | `/api/v1/properties/{propertyId}/accommodations/{accommodationId}/rates/{rateId}` | Atualizar/desativar tarifa | `write` | 200, 400, 401, 403, 404, 409, 422, 429, 500 |
| `DELETE` | `/api/v1/properties/{propertyId}/accommodations/{accommodationId}/rates/{rateId}` | Excluir tarifa nunca enviada | `write` | 204, 400, 401, 403, 404, 409, 422, 429, 500 |
| `POST` | `/api/v1/properties/{propertyId}/commercial-offer-validations` | Validar oferta por segundo operador | `review` | 201, 400, 401, 403, 404, 409, 422, 429, 500 |
| `POST` | `/api/v1/properties/{propertyId}/commercial-offer-submissions` | Enviar oferta à revisão | `write` | 201, 400, 401, 403, 404, 409, 422, 429, 500 |
| `GET` | `/api/v1/properties/{propertyId}/commercial-offer-history` | Consultar histórico | `read` | 200, 400, 401, 403, 404, 422, 429, 500 |
| `GET` | `/api/v1/commercial-offer-metrics` | Consultar métricas | `metrics` | 200, 400, 401, 403, 404, 422, 429, 500 |

## Endpoints detalhados

### `GET /api/v1/commercial-offers`

**Propósito:** alimentar a fila operacional de estruturação.  
**Consumido por:** backoffice — lista de ofertas.  
**Query:** `_page`, `_size`, `propertyId`, `status`, `hasBlockingIssues`, `overdue`, `sort`, `order`.

```json
{
  "data": [{
    "propertyId": "9547f6b8-c85d-47b6-a683-13306c20f862",
    "propertyName": "Pousada Mar do Sol",
    "destinationId": "dest-porto-de-galinhas",
    "status": "readyForValidation",
    "revision": 7,
    "completenessPercentage": 100,
    "blockingIssueCount": 0,
    "accommodationCount": 2,
    "completeAccommodationCount": 1,
    "everSubmitted": false,
    "authoredBy": { "id": "usr_01J2M8HGK7D23R", "displayName": "Ana Souza" },
    "completeInformationReceivedAt": "2026-07-20T14:00:00Z",
    "targetSubmissionAt": "2026-07-22T21:00:00Z",
    "lastSubmittedAt": null,
    "createdAt": "2026-07-18T13:30:00Z",
    "updatedAt": "2026-07-22T15:20:00Z"
  }],
  "pagination": { "page": 1, "size": 20, "total": 8, "totalPages": 1 }
}
```

### `GET /api/v1/properties/{propertyId}/commercial-offer`

**Propósito:** compor a tela de trabalho com políticas, acomodações, validação e pendências.  
**Consumido por:** backoffice — detalhe da oferta.

```json
{
  "propertyId": "9547f6b8-c85d-47b6-a683-13306c20f862",
  "propertyName": "Pousada Mar do Sol",
  "status": "readyForValidation",
  "revision": 8,
  "completenessPercentage": 100,
  "blockingIssueCount": 0,
  "accommodationCount": 1,
  "completeAccommodationCount": 1,
  "defaultPolicyId": "e96dab42-0170-4b98-a937-cdc25ad2f68d",
  "policies": [],
  "accommodations": [],
  "pendingIssues": [],
  "currentValidation": null,
  "latestReturn": null,
  "everSubmitted": false,
  "authoredBy": { "id": "usr_01J2M8HGK7D23R", "displayName": "Ana Souza" },
  "createdAt": "2026-07-18T13:30:00Z",
  "updatedAt": "2026-07-22T15:20:00Z"
}
```

Arrays vazios são sempre `[]`; associações ausentes são `null`.

### `GET /api/v1/properties/{propertyId}/commercial-policies`

**Propósito:** listar políticas reutilizáveis da propriedade.  
**Consumido por:** seletores de política e gestão de políticas.  
**Query:** `status`.

```json
{
  "data": [{
    "id": "e96dab42-0170-4b98-a937-cdc25ad2f68d",
    "propertyId": "9547f6b8-c85d-47b6-a683-13306c20f862",
    "type": "flexible",
    "title": "Flexível",
    "rulesSummary": "Cancelamento gratuito até sete dias antes do check-in.",
    "ruleSetVersion": "BR-2026-01",
    "isDefault": true,
    "status": "active",
    "usageCount": 2,
    "everSubmitted": false,
    "createdAt": "2026-07-18T14:00:00Z",
    "updatedAt": "2026-07-18T14:00:00Z"
  }]
}
```

### `POST /api/v1/properties/{propertyId}/commercial-policies`

**Propósito:** cadastrar política Flexível ou Não-Reembolsável com regras aprovadas.  
**Consumido por:** formulário de nova política.

```json
{ "type": "flexible", "setAsDefault": true }
```

Resposta `201` contém `CommercialPolicy` e o header `Location`. Tipo ativo repetido retorna `409 POLICY_TYPE_ALREADY_ACTIVE`.

### `PUT /api/v1/properties/{propertyId}/commercial-policies/default`

**Propósito:** substituir a política padrão e escolher propagação para acomodações existentes.  
**Consumido por:** confirmação de troca da política padrão.

```json
{
  "policyId": "2a513e60-d8ad-4b6d-b972-fd560ecdc718",
  "applyToExistingAccommodations": true,
  "expectedRevision": 8
}
```

```json
{
  "defaultPolicy": {
    "id": "2a513e60-d8ad-4b6d-b972-fd560ecdc718",
    "propertyId": "9547f6b8-c85d-47b6-a683-13306c20f862",
    "type": "nonRefundable",
    "title": "Não-Reembolsável",
    "rulesSummary": "Sem reembolso por cancelamento voluntário, ressalvadas hipóteses legais.",
    "ruleSetVersion": "BR-2026-01",
    "isDefault": true,
    "status": "active",
    "usageCount": 2,
    "everSubmitted": false,
    "createdAt": "2026-07-18T14:05:00Z",
    "updatedAt": "2026-07-22T16:00:00Z"
  },
  "updatedAccommodationCount": 2,
  "revision": 9
}
```

### `PATCH /api/v1/properties/{propertyId}/commercial-policies/{policyId}`

**Propósito:** desativar política, preservando associação por meio de substituição.  
**Consumido por:** gestão de políticas.

```json
{
  "status": "inactive",
  "replacementPolicyId": "2a513e60-d8ad-4b6d-b972-fd560ecdc718",
  "deactivationReason": "Política substituída pela versão comercial vigente.",
  "expectedRevision": 9
}
```

Resposta `200`: `CommercialPolicy` atualizada. Política em uso sem substituta retorna `422 REPLACEMENT_POLICY_REQUIRED`.

### `DELETE /api/v1/properties/{propertyId}/commercial-policies/{policyId}`

**Propósito:** remover política criada por engano antes de qualquer envio.  
**Consumido por:** gestão de políticas.

Sem request body. Resposta `204` sem conteúdo. Política padrão, associada ou já enviada retorna `422 POLICY_DELETION_NOT_ALLOWED`.

### `GET /api/v1/properties/{propertyId}/accommodations`

**Propósito:** listar acomodações e sua completude comercial.  
**Consumido por:** navegação da oferta.  
**Query:** `_page`, `_size`, `status`, `completeness`, `sort`, `order`.

```json
{
  "data": [{
    "id": "7332fc6e-71aa-43cd-90b4-d2fd99c97787",
    "propertyId": "9547f6b8-c85d-47b6-a683-13306c20f862",
    "commercialName": "Suíte Jardim",
    "category": "Suíte",
    "bedConfiguration": [{ "type": "queen", "quantity": 1 }, { "type": "single", "quantity": 1 }],
    "structuralFeatures": ["privateBathroom", "airConditioning", "balcony"],
    "totalCapacity": 3,
    "maxAdults": 2,
    "maxChildren": 1,
    "childAgeRange": { "minAgeInclusive": 0, "maxAgeInclusive": 11 },
    "childAgeRangeSource": "propertyDefault",
    "policyId": "e96dab42-0170-4b98-a937-cdc25ad2f68d",
    "status": "active",
    "deactivationReason": null,
    "completenessPercentage": 100,
    "pendingIssues": [],
    "rateCount": 2,
    "activeRateCount": 2,
    "everSubmitted": false,
    "createdAt": "2026-07-19T12:00:00Z",
    "updatedAt": "2026-07-22T15:20:00Z"
  }],
  "pagination": { "page": 1, "size": 20, "total": 1, "totalPages": 1 }
}
```

### `POST /api/v1/properties/{propertyId}/accommodations`

**Propósito:** iniciar uma acomodação em rascunho, herdando política e faixa infantil padrão.  
**Consumido por:** formulário de acomodação.

```json
{
  "commercialName": "Suíte Jardim",
  "category": "Suíte",
  "bedConfiguration": [{ "type": "queen", "quantity": 1 }],
  "structuralFeatures": ["privateBathroom", "airConditioning"],
  "totalCapacity": 2,
  "maxAdults": 2,
  "maxChildren": 0
}
```

Resposta `201` contém `Accommodation` e `Location`. Apenas `commercialName` é obrigatório no primeiro salvamento.

### `GET /api/v1/properties/{propertyId}/accommodations/{accommodationId}`

**Propósito:** carregar a edição e as pendências de uma acomodação.  
**Consumido por:** detalhe da acomodação.

```json
{
  "id": "7332fc6e-71aa-43cd-90b4-d2fd99c97787",
  "propertyId": "9547f6b8-c85d-47b6-a683-13306c20f862",
  "commercialName": "Suíte Jardim",
  "category": null,
  "bedConfiguration": [],
  "structuralFeatures": [],
  "totalCapacity": null,
  "maxAdults": null,
  "maxChildren": null,
  "childAgeRange": { "minAgeInclusive": 0, "maxAgeInclusive": 11 },
  "childAgeRangeSource": "propertyDefault",
  "policyId": "e96dab42-0170-4b98-a937-cdc25ad2f68d",
  "status": "draft",
  "deactivationReason": null,
  "completenessPercentage": 35,
  "pendingIssues": [{
    "code": "OCCUPANCY_REQUIRED",
    "message": "Informe capacidade e limites de ocupação.",
    "severity": "blocking",
    "resourceType": "accommodation",
    "resourceId": "7332fc6e-71aa-43cd-90b4-d2fd99c97787",
    "field": "totalCapacity"
  }],
  "rateCount": 0,
  "activeRateCount": 0,
  "everSubmitted": false,
  "createdAt": "2026-07-19T12:00:00Z",
  "updatedAt": "2026-07-19T12:00:00Z"
}
```

### `PATCH /api/v1/properties/{propertyId}/accommodations/{accommodationId}`

**Propósito:** salvar parcialmente ocupação, camas, características, política ou estado.  
**Consumido por:** formulário de acomodação.

```json
{
  "bedConfiguration": [{ "type": "queen", "quantity": 1 }, { "type": "single", "quantity": 1 }],
  "totalCapacity": 3,
  "maxAdults": 2,
  "maxChildren": 1,
  "childAgeRange": { "minAgeInclusive": 0, "maxAgeInclusive": 10 },
  "expectedRevision": 9
}
```

Resposta `200`: `Accommodation` atualizada. Ocupação incoerente retorna `422 INVALID_OCCUPANCY_CONFIGURATION`.

### `DELETE /api/v1/properties/{propertyId}/accommodations/{accommodationId}`

**Propósito:** remover acomodação nunca enviada e suas tarifas de rascunho.  
**Consumido por:** detalhe da acomodação.

Sem request body. Resposta `204`. Acomodação já enviada retorna `422 ACCOMMODATION_DELETION_NOT_ALLOWED`; use `PATCH` com `status: inactive` e `deactivationReason`.

### `GET /api/v1/properties/{propertyId}/accommodations/{accommodationId}/rates`

**Propósito:** listar períodos e condições tarifárias.  
**Consumido por:** grade de tarifas.  
**Query:** `_page`, `_size`, `status`, `activeOn`, `validFrom`, `validTo`, `sort`, `order`.

```json
{
  "data": [{
    "id": "7eb68321-f5e7-49f7-9ee6-ef450496012b",
    "accommodationId": "7332fc6e-71aa-43cd-90b4-d2fd99c97787",
    "name": "Verão 2027",
    "conditionCode": "standard-breakfast",
    "basePriceCents": 48900,
    "includedGuests": 2,
    "additionalAdultPriceCents": 12000,
    "additionalChildPriceCents": 6000,
    "validFrom": "2026-12-01",
    "validTo": "2027-02-28",
    "minimumNights": 2,
    "policyId": "e96dab42-0170-4b98-a937-cdc25ad2f68d",
    "mealPlan": "breakfast",
    "currency": "BRL",
    "mandatoryFeesIncluded": true,
    "status": "active",
    "deactivationReason": null,
    "completenessPercentage": 100,
    "pendingIssues": [],
    "everSubmitted": false,
    "createdAt": "2026-07-20T11:00:00Z",
    "updatedAt": "2026-07-22T15:20:00Z"
  }],
  "pagination": { "page": 1, "size": 20, "total": 1, "totalPages": 1 }
}
```

### `POST /api/v1/properties/{propertyId}/accommodations/{accommodationId}/rates`

**Propósito:** iniciar uma condição tarifária em rascunho.  
**Consumido por:** formulário de tarifa.

```json
{
  "name": "Verão 2027",
  "conditionCode": "standard-breakfast",
  "basePriceCents": 48900,
  "includedGuests": 2,
  "additionalAdultPriceCents": 12000,
  "additionalChildPriceCents": 6000,
  "validFrom": "2026-12-01",
  "validTo": "2027-02-28",
  "minimumNights": 2,
  "policyId": "e96dab42-0170-4b98-a937-cdc25ad2f68d",
  "mealPlan": "breakfast"
}
```

Resposta `201` contém `CommercialRate` e `Location`. Moeda `BRL` e taxas obrigatórias incluídas são invariantes do servidor.

### `PATCH /api/v1/properties/{propertyId}/accommodations/{accommodationId}/rates/{rateId}`

**Propósito:** salvar parcialmente ou desativar uma tarifa.  
**Consumido por:** formulário de tarifa.

```json
{
  "basePriceCents": 51900,
  "minimumNights": 3,
  "expectedRevision": 10
}
```

Resposta `200`: tarifa atualizada. Sobreposição para a mesma acomodação, `conditionCode`, política e alimentação retorna `409 RATE_PERIOD_OVERLAP`. Alteração após validação a invalida.

### `DELETE /api/v1/properties/{propertyId}/accommodations/{accommodationId}/rates/{rateId}`

**Propósito:** remover tarifa nunca enviada.  
**Consumido por:** grade de tarifas.

Sem request body. Resposta `204`. Tarifa já enviada retorna `422 RATE_DELETION_NOT_ALLOWED`; use desativação com motivo.

### `POST /api/v1/properties/{propertyId}/commercial-offer-validations`

**Propósito:** registrar conferência de preços, ocupação, políticas e períodos por segundo operador.  
**Consumido por:** tela de revisão comercial.

```json
{
  "expectedRevision": 11,
  "comment": "Preços, ocupação, política e períodos conferidos."
}
```

```json
{
  "id": "89386911-d42f-43f5-bd59-2fd6be2b9488",
  "propertyId": "9547f6b8-c85d-47b6-a683-13306c20f862",
  "revision": 11,
  "status": "valid",
  "validatedBy": { "id": "usr_01J2N3D7E4Q8KC", "displayName": "Bruno Lima" },
  "validatedAt": "2026-07-22T16:10:00Z",
  "invalidatedAt": null,
  "invalidationReason": null,
  "comment": "Preços, ocupação, política e períodos conferidos."
}
```

O autor da revisão recebe `422 SELF_VALIDATION_NOT_ALLOWED`. Oferta incompleta recebe `422 OFFER_NOT_READY`; revisão divergente recebe `409 REVISION_MISMATCH`.

### `POST /api/v1/properties/{propertyId}/commercial-offer-submissions`

**Propósito:** criar envio idempotente e publicar `oferta-inventario.oferta-estruturada`.  
**Consumido por:** confirmação de envio.  
**Header obrigatório:** `Idempotency-Key: d6c6f453-d1bc-4ca8-bf0f-2f061207192e`.

```json
{
  "expectedRevision": 11,
  "validationId": "89386911-d42f-43f5-bd59-2fd6be2b9488"
}
```

```json
{
  "id": "20d96ead-fcd4-4337-9680-ccb46ea91042",
  "propertyId": "9547f6b8-c85d-47b6-a683-13306c20f862",
  "revision": 11,
  "validationId": "89386911-d42f-43f5-bd59-2fd6be2b9488",
  "status": "accepted",
  "eventName": "oferta-inventario.oferta-estruturada",
  "submittedBy": { "id": "usr_01J2M8HGK7D23R", "displayName": "Ana Souza" },
  "submittedAt": "2026-07-22T16:20:00Z"
}
```

Validação inválida/antiga retorna `422 VALIDATION_REQUIRED`; revisão concorrente retorna `409 REVISION_MISMATCH`.

### `GET /api/v1/properties/{propertyId}/commercial-offer-history`

**Propósito:** preservar alterações, validações, envios, devoluções e desativações.  
**Consumido por:** timeline da oferta e investigação de retrabalho.  
**Query:** `_page`, `_size`, `eventType`.

```json
{
  "data": [{
    "id": "a62695f2-ef05-48c0-b494-b7941319cf7d",
    "eventType": "returned",
    "revision": 11,
    "summary": "Oferta devolvida para correção do acréscimo infantil.",
    "actorType": "downstreamDomain",
    "actor": null,
    "reason": "Confirmar o acréscimo por criança na tarifa Verão 2027.",
    "occurredAt": "2026-07-23T12:00:00Z"
  }],
  "pagination": { "page": 1, "size": 20, "total": 1, "totalPages": 1 }
}
```

Devoluções chegam pela integração de domínio downstream, não por endpoint público desta feature. Corrigir uma oferta devolvida usa os mesmos `PATCH`, invalida a validação anterior e exige nova validação e novo envio.

### `GET /api/v1/commercial-offer-metrics`

**Propósito:** medir prazo, completude, primeira aceitação, dupla validação e retrabalho.  
**Consumido por:** painel gerencial.  
**Query obrigatória:** `from`, `to`. **Opcional:** `destinationId`.

```json
{
  "from": "2026-07-01T00:00:00Z",
  "to": "2026-08-01T00:00:00Z",
  "totalOffers": 10,
  "completeProperties": 8,
  "firstReviewAcceptanceRate": 0.9,
  "submissionWithinTwoBusinessDaysRate": 1.0,
  "dualValidationRate": 1.0,
  "requestsProcessedWithinFourBusinessHoursRate": 1.0,
  "returnedOfferCount": 1,
  "averageReworkCount": 0.2
}
```

## Schemas de entidades principais

### CommercialOffer

| Campo | Tipo | Obrigatório | Nullable | Descrição |
|---|---|---:|---:|---|
| `propertyId` | UUID | Sim | Não | Identificador público da propriedade |
| `propertyName` | string | Sim | Não | Nome para exibição |
| `status` | enum | Sim | Não | `draft`, `readyForValidation`, `validated`, `readyForReview`, `returned`, `published` |
| `revision` | integer | Sim | Não | Versão comercial usada no controle otimista |
| `completenessPercentage` | integer | Sim | Não | Percentual de 0 a 100 |
| `blockingIssueCount` | integer | Sim | Não | Quantidade de pendências bloqueantes |
| `authoredBy` | StaffActor | Sim | Não | Operador autor da revisão atual |
| `defaultPolicyId` | UUID | Sim | Sim | Política herdada por novas acomodações |
| `policies` | CommercialPolicy[] | Sim | Não | Lista vazia quando não houver políticas |
| `accommodations` | Accommodation[] | Sim | Não | Lista vazia quando não houver acomodações |
| `pendingIssues` | PendingIssue[] | Sim | Não | Pendências acionáveis da oferta |
| `currentValidation` | OfferValidation | Não | Sim | Validação vigente da revisão atual |
| `latestReturn` | OfferReturn | Não | Sim | Devolução downstream mais recente |

### CommercialPolicy

| Campo | Tipo | Obrigatório | Nullable | Descrição |
|---|---|---:|---:|---|
| `id` | UUID | Sim | Não | Identificador público |
| `type` | enum | Sim | Não | `flexible` ou `nonRefundable` |
| `rulesSummary` | string | Sim | Não | Resumo imutável resolvido pelo servidor |
| `ruleSetVersion` | string | Sim | Não | Versão jurídica aplicada |
| `isDefault` | boolean | Sim | Não | Indica padrão para novas acomodações |
| `status` | enum | Sim | Não | `active` ou `inactive` |
| `usageCount` | integer | Sim | Não | Associações que exigem substituição ao desativar |
| `everSubmitted` | boolean | Sim | Não | Bloqueia hard delete quando verdadeiro |

### Accommodation

| Campo | Tipo | Obrigatório | Nullable | Descrição |
|---|---|---:|---:|---|
| `id` | UUID | Sim | Não | Identificador público |
| `commercialName` | string | Sim | Não | Nome comercial |
| `category` | string | Não | Sim | Categoria comercial livre e controlada pela Operação |
| `bedConfiguration` | BedConfigurationItem[] | Sim | Não | Camas e quantidades; vazia no rascunho |
| `structuralFeatures` | enum[] | Sim | Não | Características físicas, não editoriais |
| `totalCapacity` | integer | Não | Sim | Capacidade máxima total |
| `maxAdults` | integer | Não | Sim | Limite de adultos |
| `maxChildren` | integer | Não | Sim | Limite de crianças |
| `childAgeRange` | ChildAgeRange | Não | Sim | Faixa efetiva da acomodação |
| `childAgeRangeSource` | enum | Sim | Não | `propertyDefault`, `accommodationOverride` ou `none` |
| `policyId` | UUID | Não | Sim | Política da acomodação |
| `status` | enum | Sim | Não | `draft`, `active` ou `inactive` |
| `pendingIssues` | PendingIssue[] | Sim | Não | Bloqueios comerciais; nunca `null` |

### CommercialRate

| Campo | Tipo | Obrigatório | Nullable | Descrição |
|---|---|---:|---:|---|
| `id` | UUID | Sim | Não | Identificador público |
| `name` | string | Sim | Não | Nome operacional do período |
| `conditionCode` | kebab-case | Sim | Não | Identifica a mesma condição para regra de sobreposição |
| `basePriceCents` | integer | Não | Sim | Valor-base da diária em centavos |
| `includedGuests` | integer | Não | Sim | Hóspedes incluídos no preço-base |
| `additionalAdultPriceCents` | integer | Não | Sim | Acréscimo por adulto em centavos |
| `additionalChildPriceCents` | integer | Não | Sim | Acréscimo por criança em centavos |
| `validFrom` | date | Não | Sim | Primeira diária do período, inclusiva |
| `validTo` | date | Não | Sim | Última diária do período, inclusiva |
| `minimumNights` | integer | Não | Sim | Mínimo definido pela data de check-in |
| `policyId` | UUID | Não | Sim | Política aplicável |
| `mealPlan` | enum | Não | Sim | `roomOnly`, `breakfast`, `halfBoard`, `fullBoard` |
| `currency` | const | Sim | Não | Sempre `BRL` |
| `mandatoryFeesIncluded` | const | Sim | Não | Sempre `true` |
| `status` | enum | Sim | Não | `draft`, `active` ou `inactive` |

### OfferValidation e OfferSubmission

| Campo | Tipo | Obrigatório | Nullable | Descrição |
|---|---|---:|---:|---|
| `OfferValidation.revision` | integer | Sim | Não | Revisão efetivamente conferida |
| `OfferValidation.validatedBy` | StaffActor | Sim | Não | Deve diferir do autor da revisão |
| `OfferValidation.status` | enum | Sim | Não | `valid` ou `invalidated` |
| `OfferSubmission.validationId` | UUID | Sim | Não | Validação válida da mesma revisão |
| `OfferSubmission.eventName` | const | Sim | Não | `oferta-inventario.oferta-estruturada` |
| `OfferSubmission.submittedAt` | datetime | Sim | Não | Instante UTC do envio |

## Regras transversais

- A oferta fica pronta para validação com pelo menos uma acomodação comercialmente completa e uma tarifa ativa atual ou futura.
- `maxAdults + maxChildren` não pode superar `totalCapacity`; hóspedes incluídos não podem superar a capacidade.
- Períodos são inclusivos. Tarifas ativas não podem se sobrepor quando compartilham acomodação, `conditionCode`, `policyId` e `mealPlan`.
- Cada diária usa o preço da tarifa aplicável à sua data; o mínimo de noites é o da tarifa vigente no check-in.
- Textos, fotos e comodidades editoriais não entram na completude de F02.
- Alterações publicadas não são aceitas pela F02 e retornam `422 PUBLISHED_OFFER_CHANGE_REQUIRES_F04`.
- Preço e política preservados em reservas existentes nunca são alterados por esta API.
- Atores são derivados do JWT; o cliente não envia `createdBy`, `validatedBy` ou `submittedBy`.
- O indicador de quatro horas úteis é calculado a partir dos `communication-records` registrados na incorporação F01.

## Códigos de erro

| HTTP | `code` | Quando ocorre |
|---:|---|---|
| 400 | `BAD_REQUEST` | JSON, parâmetro ou formato inválido |
| 401 | `UNAUTHORIZED` | JWT ausente, inválido ou expirado |
| 403 | `FORBIDDEN` | Escopo ou permissão insuficiente |
| 404 | `PROPERTY_NOT_FOUND` | Propriedade incorporada não encontrada |
| 404 | `POLICY_NOT_FOUND` | Política não encontrada na propriedade |
| 404 | `ACCOMMODATION_NOT_FOUND` | Acomodação não encontrada na propriedade |
| 404 | `RATE_NOT_FOUND` | Tarifa não encontrada na acomodação |
| 409 | `REVISION_MISMATCH` | `expectedRevision` não corresponde à revisão atual |
| 409 | `POLICY_TYPE_ALREADY_ACTIVE` | Já existe política ativa do mesmo tipo |
| 409 | `RATE_PERIOD_OVERLAP` | Períodos tarifários equivalentes se sobrepõem |
| 409 | `IDEMPOTENCY_KEY_REUSED` | Chave reutilizada com payload diferente |
| 422 | `INVALID_OCCUPANCY_CONFIGURATION` | Capacidade, adultos, crianças ou camas incoerentes |
| 422 | `REPLACEMENT_POLICY_REQUIRED` | Política em uso desativada sem substituta |
| 422 | `POLICY_DELETION_NOT_ALLOWED` | Política padrão, associada ou já enviada |
| 422 | `ACCOMMODATION_DELETION_NOT_ALLOWED` | Acomodação já enviada |
| 422 | `RATE_DELETION_NOT_ALLOWED` | Tarifa já enviada |
| 422 | `OFFER_NOT_READY` | Há pendências bloqueantes ou nenhuma tarifa atual/futura |
| 422 | `SELF_VALIDATION_NOT_ALLOWED` | Autor tenta validar a própria revisão |
| 422 | `VALIDATION_REQUIRED` | Envio sem validação válida da revisão atual |
| 422 | `PUBLISHED_OFFER_CHANGE_REQUIRES_F04` | Alteração pertence à governança de F04 |
| 429 | `RATE_LIMIT_EXCEEDED` | Limite de requisições excedido |
| 500 | `INTERNAL_ERROR` | Falha inesperada; correlacionar por `traceId` |

### Formato padrão de erro

```json
{
  "type": "https://api.localizestay.com/problems/offer-not-ready",
  "title": "Oferta ainda não está pronta",
  "status": 422,
  "detail": "Existem pendências comerciais que impedem a validação.",
  "instance": "/api/v1/properties/9547f6b8-c85d-47b6-a683-13306c20f862/commercial-offer-validations",
  "code": "OFFER_NOT_READY",
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
  "errors": [{
    "field": "accommodations[0].rates",
    "code": "CURRENT_OR_FUTURE_RATE_REQUIRED",
    "message": "Informe ao menos um período tarifário atual ou futuro."
  }],
  "metadata": {}
}
```

## Rastreabilidade PRD → endpoints

| Requisito | Cobertura |
|---|---|
| RF-01 | CRUD restrito de políticas e `PUT .../commercial-policies/default` |
| RF-02 | CRUD de acomodações, ocupação, camas, características e herança infantil |
| RF-03 | CRUD de tarifas, BRL/centavos, alimentação, política, período e sobreposição |
| RF-04 | Rascunhos progressivos, `pendingIssues`, hard delete condicional e desativação |
| RF-05 | `commercial-offer-validations`, `commercial-offer-submissions` e revisão otimista |
| RF-06 | Estado `returned`, histórico, correção por PATCH e nova validação/envio |
| Métricas | Fila operacional e `GET /commercial-offer-metrics` |

## Questões em aberto

- Validar com Jurídico a redação e o identificador final de `ruleSetVersion` antes de operar dinheiro real.
- Ratificar com Plataforma os nomes definitivos das permissões declarativas.
- Definir no contrato de integração downstream o payload completo consumido quando uma oferta é devolvida; este contrato expõe apenas o resultado no read model.
- Confirmar o calendário operacional usado nos indicadores de duas jornadas úteis e quatro horas úteis.

## Como usar este contrato

### Backend

Use `api-contract.yaml` como especificação dos endpoints e gere testes de conformidade. `x-backend-notes` contém restrições de persistência, concorrência e outbox.

### Frontend

Gere os tipos TypeScript diretamente dos schemas:

```bash
npx openapi-typescript api-contract.yaml -o src/types/api.ts
```

### Mock

```bash
npx @stoplight/prism-cli mock api-contract.yaml
```

O mock ficará disponível, por padrão, em `http://localhost:4010`.
