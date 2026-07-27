# API Contract — Controlar Allotment, Disponibilidade, Bloqueios e Retenções

> **Gerado a partir de:** `tasks/prd-controlar-allotment-disponibilidade-bloqueios-e-retencoes/prd.md`
> **Data:** 2026-07-26
> **Status:** Em revisão
> **Versão:** 1.0.0
> **Spec técnica:** `api-contract.yaml` (OpenAPI 3.1)

## Premissas e decisões

| Decisão | Escolha | Motivo |
|---|---|---|
| Autenticação | JWT Bearer emitido pelo LogTo, escopo `staff` | Padrão vigente da plataforma (F01 e F02) |
| Endpoint público | Apenas `GET /availability` | D01 e a vitrine consultam saldo sem autenticar; o resto é backoffice |
| Permissões | `inventory:read`, `write`, `block`, `hold`, `metrics` | Separa consulta, manutenção de allotment, ação de bloqueio, ciclo de checkout e indicadores |
| Versionamento | `/api/v1` | Compatível com os contratos de F01 e F02 |
| Nomenclatura | Paths em inglês/plural/kebab-case; JSON em camelCase | Consistência entre clientes e API |
| Paginação | `_page` e `_size`, máximo 100 | Padrão REST do projeto |
| Erros | RFC 9457, `application/problem+json`, com `code`, `traceId` e `metadata` | Erros rastreáveis e tratáveis pelo frontend |
| Quantidades | Inteiros de unidades de acomodação | Restrição explícita do PRD; nunca fracionárias |
| Datas | Diárias em `date`; instantes em `date-time` UTC | Separa a noite de hospedagem do instante global |
| Períodos | `startDate`/`endDate` inclusivos; `checkIn` inclusivo e `checkOut` exclusivo | Período de contrato difere de período de estadia |
| Saldo | `available = allotted − committed − held − blocked`, piso zero | RN-03; data sem allotment tem saldo zero, nunca indefinido |
| Retenção | Duração global fixa de 15 minutos, servidor devolve `expiresAt` | Cliente não parametriza o ponto de maior concorrência |
| Janela de atendimento | Seg–sáb 08h–20h em `America/Fortaleza`, derivada no servidor | RN-14; o cliente informa apenas `receivedAt`. Errata de 2026-07-26: era `America/Sao_Paulo`; ambos são UTC−3 fixo desde 2019, e Fortaleza alinha o piloto ao calendário de F01/F02 |
| Fila de solicitações | Recurso próprio `/inventory-requests` | Sustenta SLA, prioridade do emergencial e exposição fora da janela |
| Concorrência | `expectedRevision` em allotment; `Idempotency-Key` em bloqueio, retenção e comprometimento | Protege as escritas críticas e concorrentes |
| Governança | Allotment e bloqueio com efeito imediato, sem Alteração Pendente | Divergência consciente com RN-08, registrada no PRD; F04 aplica governança depois |
| Eventos | In-process com outbox, documentados em `x-domain-events` | ADR-0002; schemas de payload são normativos |
| Ondas | `x-wave: A` (inventário) e `x-wave: B` (retenção) | Onda B só entra em produção validada ponta a ponta com D03-C01 |

## Permissões declarativas

| Permissão | Finalidade |
|---|---|
| `inventory:read` | Consultar calendário, allotments, bloqueios, fila, retenções e vendabilidade |
| `inventory:write` | Ceder e alterar allotment; registrar e mover solicitações da fila |
| `inventory:block` | Aplicar, simular e remover bloqueios, inclusive emergenciais |
| `inventory:hold` | Criar, liberar e comprometer retenções — usada por D03 no checkout |
| `inventory:metrics` | Consultar indicadores consolidados |

## Resumo de endpoints

Todos exigem JWT LogTo com escopo `staff`, exceto `GET /availability`, que é público.

| Método | Path | Descrição | Permissão | Onda | Status principais |
|---|---|---|---|:--:|---|
| `GET` | `/api/v1/availability` | Consultar disponibilidade vendável | público | A | 200, 400, 404, 422, 429, 500 |
| `GET` | `/api/v1/properties/{propertyId}/sellability` | Diagnosticar vendabilidade (RN-07) | `read` | A | 200, 400, 401, 403, 404, 429, 500 |
| `GET` | `/api/v1/properties/{propertyId}/inventory-calendar` | Consultar calendário de inventário | `read` | A | 200, 400, 401, 403, 404, 422, 429, 500 |
| `GET` | `/api/v1/properties/{propertyId}/accommodations/{accommodationId}/daily-inventory/{date}` | Detalhar composição de uma data | `read` | A | 200, 400, 401, 403, 404, 429, 500 |
| `GET` | `/api/v1/properties/{propertyId}/allotments` | Listar allotments | `read` | A | 200, 400, 401, 403, 404, 422, 429, 500 |
| `POST` | `/api/v1/properties/{propertyId}/allotments` | Ceder allotment | `write` | A | 201, 400, 401, 403, 404, 409, 422, 429, 500 |
| `GET` | `/api/v1/properties/{propertyId}/allotments/{allotmentId}` | Consultar allotment | `read` | A | 200, 400, 401, 403, 404, 429, 500 |
| `PATCH` | `/api/v1/properties/{propertyId}/allotments/{allotmentId}` | Alterar allotment | `write` | A | 200, 400, 401, 403, 404, 409, 422, 429, 500 |
| `DELETE` | `/api/v1/properties/{propertyId}/allotments/{allotmentId}` | Cancelar allotment | `write` | A | 204, 400, 401, 403, 404, 409, 422, 429, 500 |
| `GET` | `/api/v1/properties/{propertyId}/inventory-blocks` | Listar bloqueios | `read` | A | 200, 400, 401, 403, 404, 422, 429, 500 |
| `POST` | `/api/v1/properties/{propertyId}/inventory-blocks` | Aplicar bloqueio | `block` | A | 201, 400, 401, 403, 404, 409, 422, 429, 500 |
| `POST` | `/api/v1/properties/{propertyId}/inventory-blocks/impact-preview` | Simular impacto de bloqueio | `block` | A | 200, 400, 401, 403, 404, 422, 429, 500 |
| `GET` | `/api/v1/properties/{propertyId}/inventory-blocks/{blockId}` | Consultar bloqueio | `read` | A | 200, 400, 401, 403, 404, 429, 500 |
| `PATCH` | `/api/v1/properties/{propertyId}/inventory-blocks/{blockId}` | Remover bloqueio | `block` | A | 200, 400, 401, 403, 404, 409, 422, 429, 500 |
| `GET` | `/api/v1/inventory-requests` | Listar fila de solicitações | `read` | A | 200, 400, 401, 403, 404, 422, 429, 500 |
| `POST` | `/api/v1/inventory-requests` | Registrar solicitação recebida | `write` | A | 201, 400, 401, 403, 404, 422, 429, 500 |
| `GET` | `/api/v1/inventory-requests/{requestId}` | Consultar solicitação | `read` | A | 200, 400, 401, 403, 404, 429, 500 |
| `PATCH` | `/api/v1/inventory-requests/{requestId}` | Atualizar situação da solicitação | `write` | A | 200, 400, 401, 403, 404, 409, 422, 429, 500 |
| `POST` | `/api/v1/inventory-holds` | Reter inventário | `hold` | B | 201, 400, 401, 403, 404, 409, 422, 429, 500 |
| `GET` | `/api/v1/inventory-holds/{holdId}` | Consultar retenção | `read` | B | 200, 400, 401, 403, 404, 429, 500 |
| `DELETE` | `/api/v1/inventory-holds/{holdId}` | Liberar retenção | `hold` | B | 204, 400, 401, 403, 404, 409, 429, 500 |
| `POST` | `/api/v1/inventory-holds/{holdId}/commitment` | Comprometer inventário | `hold` | B | 201, 400, 401, 403, 404, 409, 422, 429, 500 |
| `GET` | `/api/v1/inventory-metrics` | Consultar métricas | `metrics` | A | 200, 400, 401, 403, 404, 422, 429, 500 |

## Rastreabilidade RF → endpoints

| RF | Endpoints |
|---|---|
| RF-01 — Ceder allotment | `POST/PATCH/DELETE /allotments`, `GET /allotments` |
| RF-02 — Aplicar e remover bloqueios | `POST /inventory-blocks`, `POST /inventory-blocks/impact-preview`, `PATCH /inventory-blocks/{id}` |
| RF-03 — Calcular e consultar saldo | `GET /availability`, `GET /sellability` |
| RF-04 — Operar o calendário | `GET /inventory-calendar`, `GET /daily-inventory/{date}`, `/inventory-requests` (fila e SLA) |
| RF-05 — Interromper vendas por curadoria | Evento `curadoria-qualidade.propriedade-suspensa` → bloqueio `curationSuspension`; leitura por `GET /sellability` |
| RF-06 — Reter inventário | `POST /inventory-holds` |
| RF-07 — Expirar e liberar retenções | `DELETE /inventory-holds/{id}`, expiração automática por `expiresAt` |
| RF-08 — Comprometer inventário | `POST /inventory-holds/{id}/commitment` |

## Endpoints detalhados

### `GET /api/v1/availability` — público

**Propósito:** dizer se uma acomodação pode ser vendida para um período de estadia.
**Consumido por:** D01 — busca e página da hospedagem.
**Query:** `propertyId`, `accommodationId`, `checkIn` (obrigatório), `checkOut` (obrigatório), `units`.

Uma acomodação só é `bookable` quando **todas** as noites entre `checkIn` (inclusivo) e `checkOut` (exclusivo) têm saldo suficiente **e** a oferta atende a RN-07. Data sem allotment tem saldo zero.

```json
{
  "checkIn": "2026-09-14",
  "checkOut": "2026-09-17",
  "nights": 3,
  "units": 1,
  "data": [
    {
      "propertyId": "9547f6b8-c85d-47b6-a683-13306c20f862",
      "propertyName": "Pousada Mar do Sol",
      "accommodationId": "7332fc6e-71aa-43cd-90b4-d2fd99c97787",
      "accommodationName": "Chalé Vista Mar",
      "bookable": true,
      "availableUnits": 2,
      "unavailabilityReason": null,
      "firstUnavailableDate": null
    },
    {
      "propertyId": "9547f6b8-c85d-47b6-a683-13306c20f862",
      "propertyName": "Pousada Mar do Sol",
      "accommodationId": "a1c4e809-3f76-42db-b0e5-9c8d71f30a26",
      "accommodationName": "Suíte Jardim",
      "bookable": false,
      "availableUnits": 0,
      "unavailabilityReason": "insufficientBalance",
      "firstUnavailableDate": "2026-09-15"
    }
  ]
}
```

A composição interna do saldo — comprometido, retido, bloqueado e motivo — **nunca** aparece aqui. O viajante vê apenas `bookable` e um motivo deliberadamente genérico.

### `GET /api/v1/properties/{propertyId}/sellability`

**Propósito:** explicar por que uma propriedade não é vendável mesmo com saldo.
**Consumido por:** backoffice — diagnóstico na tela de inventário.

```json
{
  "propertyId": "9547f6b8-c85d-47b6-a683-13306c20f862",
  "propertyName": "Pousada Mar do Sol",
  "sellable": false,
  "suspendedByCuration": false,
  "gates": [
    { "code": "propertyApproved", "status": "satisfied", "detail": null, "ownerDomain": "D06" },
    { "code": "contentApproved", "status": "satisfied", "detail": null, "ownerDomain": "D06" },
    { "code": "validRate", "status": "satisfied", "detail": null, "ownerDomain": "D02" },
    { "code": "testedChannel", "status": "satisfied", "detail": null, "ownerDomain": "D02" },
    {
      "code": "activeAllotment",
      "status": "blocked",
      "detail": "Nenhum allotment vigente nos próximos 90 dias.",
      "ownerDomain": "D02"
    }
  ],
  "evaluatedAt": "2026-07-26T13:40:00Z"
}
```

### `GET /api/v1/properties/{propertyId}/inventory-calendar`

**Propósito:** a tela de trabalho da Operação (RF-04).
**Consumido por:** backoffice — grade de datas por acomodação.
**Query:** `from` e `to` (obrigatórios, máximo 92 dias), `accommodationId`, `onlyUnavailable`.

```json
{
  "propertyId": "9547f6b8-c85d-47b6-a683-13306c20f862",
  "propertyName": "Pousada Mar do Sol",
  "from": "2026-09-14",
  "to": "2026-09-16",
  "sellable": true,
  "accommodations": [
    {
      "accommodationId": "7332fc6e-71aa-43cd-90b4-d2fd99c97787",
      "accommodationName": "Chalé Vista Mar",
      "belowCommercialFloor": false,
      "days": [
        {
          "date": "2026-09-14",
          "allottedUnits": 3,
          "committedUnits": 1,
          "heldUnits": 1,
          "blockedUnits": 1,
          "availableUnits": 0,
          "blocks": [
            {
              "blockId": "b41d7a90-6c2e-4f18-84b7-2ad9f0c31e77",
              "type": "planned",
              "reason": "maintenance",
              "reasonNote": "Troca do ar-condicionado.",
              "units": 1
            }
          ]
        },
        {
          "date": "2026-09-15",
          "allottedUnits": 3,
          "committedUnits": 1,
          "heldUnits": 0,
          "blockedUnits": 0,
          "availableUnits": 2,
          "blocks": []
        }
      ]
    }
  ]
}
```

Arrays vazios são sempre `[]`. Datas sem allotment aparecem na grade com `allottedUnits: 0`.

### `GET /api/v1/properties/{propertyId}/accommodations/{accommodationId}/daily-inventory/{date}`

**Propósito:** drill-down de uma célula sem saldo — qual parcela é comprometida, retida ou bloqueada.
**Consumido por:** backoffice — detalhe da data.

```json
{
  "propertyId": "9547f6b8-c85d-47b6-a683-13306c20f862",
  "accommodationId": "7332fc6e-71aa-43cd-90b4-d2fd99c97787",
  "date": "2026-09-14",
  "allottedUnits": 3,
  "committedUnits": 1,
  "heldUnits": 1,
  "blockedUnits": 1,
  "availableUnits": 0,
  "allotmentId": "1c0a3f4e-2b77-4f52-9a6d-0f1b7c8e5d31",
  "blocks": [],
  "commitments": [
    {
      "reservationId": "8a1e6f2c-4b90-4d75-8e33-c2f7b0d95a41",
      "checkIn": "2026-09-14",
      "checkOut": "2026-09-17",
      "units": 1
    }
  ],
  "holds": [
    {
      "holdId": "5d2f8b41-9c3a-4e07-b6d8-71a4f0e2c983",
      "reservationIntentId": "c7b9e401-63af-4a28-9d15-3e806f2b7c4d",
      "checkIn": "2026-09-14",
      "checkOut": "2026-09-16",
      "units": 1,
      "expiresAt": "2026-07-26T13:27:00Z"
    }
  ]
}
```

Reservas e retenções aparecem apenas por identificador e quantidade. Dados do viajante pertencem a D03.

### `POST /api/v1/properties/{propertyId}/allotments` — RF-01

**Propósito:** registrar a quantidade cedida exclusivamente à LocalizeStay.
**Consumido por:** backoffice — formulário de cessão e ação direta no calendário.

```json
{
  "accommodationId": "7332fc6e-71aa-43cd-90b4-d2fd99c97787",
  "units": 3,
  "startDate": "2026-09-01",
  "endDate": "2026-11-29",
  "contractReference": "CT-2026-0031",
  "requestId": "3f9c1d80-58ab-4a1e-9e7c-6b0f2d4a8c15",
  "notes": "Allotment negociado para a alta temporada do corredor Nordeste."
}
```

Resposta `201` com header `Location`:

```json
{
  "id": "1c0a3f4e-2b77-4f52-9a6d-0f1b7c8e5d31",
  "propertyId": "9547f6b8-c85d-47b6-a683-13306c20f862",
  "accommodationId": "7332fc6e-71aa-43cd-90b4-d2fd99c97787",
  "accommodationName": "Chalé Vista Mar",
  "units": 3,
  "startDate": "2026-09-01",
  "endDate": "2026-11-29",
  "status": "active",
  "belowCommercialFloor": false,
  "contractReference": "CT-2026-0031",
  "requestId": "3f9c1d80-58ab-4a1e-9e7c-6b0f2d4a8c15",
  "notes": "Allotment negociado para a alta temporada do corredor Nordeste.",
  "cancellationReason": null,
  "revision": 1,
  "createdBy": { "id": "usr_01J2M8HGK7D23R", "displayName": "Ana Souza" },
  "updatedBy": null,
  "createdAt": "2026-07-26T13:10:00Z",
  "updatedAt": "2026-07-26T13:10:00Z"
}
```

#### Erros possíveis

| HTTP | `code` | Quando ocorre |
|---|---|---|
| 409 | `ALLOTMENT_PERIOD_OVERLAP` | Já existe allotment da mesma acomodação com período sobreposto; `metadata` traz o allotment conflitante |
| 422 | `INVALID_DATE_RANGE` | `endDate` anterior a `startDate` |
| 404 | `ACCOMMODATION_NOT_FOUND` | Acomodação inexistente ou de outra propriedade |

Allotment com `units: 1` é **aceito**, com `belowCommercialFloor: true` — a categoria é comercializável, mas não conta para a meta de cobertura do piloto.

### `PATCH /api/v1/properties/{propertyId}/allotments/{allotmentId}` — RF-01

```json
{ "units": 2, "expectedRevision": 1 }
```

Redução que deixaria o total cedido abaixo do comprometido é recusada:

```json
{
  "type": "https://api.localizestay.com/problems/allotment-below-committed",
  "title": "Redução abaixo do comprometido",
  "status": 422,
  "detail": "Existem datas com capacidade comprometida acima da nova quantidade. Registre um bloqueio para reduzir a venda sem alterar o contrato.",
  "instance": "/api/v1/properties/9547f6b8-c85d-47b6-a683-13306c20f862/allotments/1c0a3f4e-2b77-4f52-9a6d-0f1b7c8e5d31",
  "code": "ALLOTMENT_BELOW_COMMITTED",
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
  "metadata": {
    "conflictingDates": [
      { "date": "2026-09-14", "committedUnits": 3 },
      { "date": "2026-09-15", "committedUnits": 3 }
    ]
  }
}
```

O `detail` já indica a ação corretiva — registrar bloqueio —, porque allotment representa o contrato, não a operação do dia.

### `POST /api/v1/properties/{propertyId}/inventory-blocks/impact-preview` — RF-02

**Propósito:** alimentar a confirmação explícita do bloqueio emergencial.
**Consumido por:** backoffice — modal de confirmação.

```json
{
  "accommodationId": "7332fc6e-71aa-43cd-90b4-d2fd99c97787",
  "type": "emergency",
  "units": null,
  "startDate": "2026-09-14",
  "endDate": "2026-09-16"
}
```

Resposta `200`:

```json
{
  "wouldBeAccepted": true,
  "rejectionCode": null,
  "affectedReservationCount": 1,
  "invalidatedHoldCount": 2,
  "freeBalanceByDate": [
    { "date": "2026-09-14", "freeUnits": 0 },
    { "date": "2026-09-15", "freeUnits": 2 },
    { "date": "2026-09-16", "freeUnits": 2 }
  ],
  "affectedReservations": [
    {
      "reservationId": "8a1e6f2c-4b90-4d75-8e33-c2f7b0d95a41",
      "checkIn": "2026-09-14",
      "checkOut": "2026-09-17",
      "units": 1
    }
  ],
  "invalidatedHolds": [
    {
      "holdId": "5d2f8b41-9c3a-4e07-b6d8-71a4f0e2c983",
      "reservationIntentId": "c7b9e401-63af-4a28-9d15-3e806f2b7c4d",
      "checkIn": "2026-09-14",
      "checkOut": "2026-09-16",
      "units": 1,
      "expiresAt": "2026-07-26T13:27:00Z"
    }
  ]
}
```

A simulação é indicativa: entre a prévia e a confirmação o saldo pode mudar. O `422` do POST é resposta legítima, não falha da prévia.

### `POST /api/v1/properties/{propertyId}/inventory-blocks` — RF-02

**Header obrigatório:** `Idempotency-Key`.

```json
{
  "accommodationId": "7332fc6e-71aa-43cd-90b4-d2fd99c97787",
  "type": "emergency",
  "origin": "partnerRequest",
  "reason": "partnerUnavailability",
  "reasonNote": "Parceiro comunicou infiltração no chalé.",
  "units": null,
  "startDate": "2026-09-14",
  "endDate": "2026-09-16",
  "requestId": "3f9c1d80-58ab-4a1e-9e7c-6b0f2d4a8c15",
  "confirmEmergencyImpact": true
}
```

Resposta `201`:

```json
{
  "id": "b41d7a90-6c2e-4f18-84b7-2ad9f0c31e77",
  "propertyId": "9547f6b8-c85d-47b6-a683-13306c20f862",
  "accommodationId": "7332fc6e-71aa-43cd-90b4-d2fd99c97787",
  "accommodationName": "Chalé Vista Mar",
  "type": "emergency",
  "origin": "partnerRequest",
  "reason": "partnerUnavailability",
  "reasonNote": "Parceiro comunicou infiltração no chalé.",
  "units": 3,
  "blocksEntireAccommodation": true,
  "startDate": "2026-09-14",
  "endDate": "2026-09-16",
  "status": "active",
  "requestId": "3f9c1d80-58ab-4a1e-9e7c-6b0f2d4a8c15",
  "affectedReservationCount": 1,
  "invalidatedHoldCount": 2,
  "salesStoppedAt": "2026-07-26T13:12:04Z",
  "removedAt": null,
  "removalReason": null,
  "removedBy": null,
  "createdBy": { "id": "usr_01J2M8HGK7D23R", "displayName": "Ana Souza" },
  "createdAt": "2026-07-26T13:12:00Z",
  "updatedAt": "2026-07-26T13:12:04Z"
}
```

#### Comportamento por tipo

| Tipo | Alcança saldo livre | Alcança retenção vigente | Alcança reserva confirmada | Confirmação explícita |
|---|:--:|:--:|:--:|:--:|
| `planned` | sim | **não** | **não** | não exigida |
| `emergency` | sim | invalida | não cancela; produz `bloqueio-afeta-reserva` | `confirmEmergencyImpact: true` |

#### Erros possíveis

| HTTP | `code` | Quando ocorre |
|---|---|---|
| 422 | `INSUFFICIENT_FREE_BALANCE` | Bloqueio planejado maior que o saldo livre; `metadata.freeBalanceByDate` informa o disponível por data |
| 422 | `EMERGENCY_BLOCK_CONFIRMATION_REQUIRED` | `type: emergency` sem `confirmEmergencyImpact: true` |
| 422 | `REASON_NOTE_REQUIRED` | `reason: other` sem `reasonNote` |
| 409 | `IDEMPOTENCY_KEY_REUSED` | Mesma chave com corpo diferente |

`salesStoppedAt` é a base da métrica de latência: do commit ao corte de novas vendas, no máximo um minuto.

### `PATCH /api/v1/properties/{propertyId}/inventory-blocks/{blockId}` — RF-02

```json
{ "status": "removed", "removalReason": "Parceiro confirmou que o reparo foi concluído." }
```

A capacidade volta a ser vendável imediatamente e o registro é preservado com `status: removed`, autor e motivo. Bloqueio já removido retorna `409 BLOCK_ALREADY_REMOVED`. Bloqueio de origem `curationSuspension` retorna `422 CURATION_BLOCK_NOT_REMOVABLE` — só a retomada da aprovação por D06 o encerra.

### `POST /api/v1/inventory-requests` — RF-04

**Propósito:** registrar a solicitação recebida por WhatsApp ou e-mail e iniciar a contagem do SLA.
**Consumido por:** backoffice — registro da fila.

```json
{
  "propertyId": "9547f6b8-c85d-47b6-a683-13306c20f862",
  "accommodationId": "7332fc6e-71aa-43cd-90b4-d2fd99c97787",
  "channel": "whatsapp",
  "requestType": "block",
  "receivedAt": "2026-07-26T03:40:00Z",
  "emergency": true,
  "requesterName": "Marcos Vinícius — recepção",
  "summary": "Parceiro comunicou indisponibilidade do Chalé Vista Mar de 14 a 16 de setembro."
}
```

Resposta `201` — o servidor deriva janela, prazo e prioridade:

```json
{
  "id": "3f9c1d80-58ab-4a1e-9e7c-6b0f2d4a8c15",
  "propertyId": "9547f6b8-c85d-47b6-a683-13306c20f862",
  "propertyName": "Pousada Mar do Sol",
  "accommodationId": "7332fc6e-71aa-43cd-90b4-d2fd99c97787",
  "channel": "whatsapp",
  "requestType": "block",
  "priority": "emergency",
  "status": "pending",
  "requesterName": "Marcos Vinícius — recepção",
  "receivedAt": "2026-07-26T03:40:00Z",
  "receivedOutsideWindow": true,
  "slaStartsAt": "2026-07-26T11:00:00Z",
  "slaDueAt": "2026-07-26T15:00:00Z",
  "processedAt": null,
  "processedWithinSla": null,
  "summary": "Parceiro comunicou indisponibilidade do Chalé Vista Mar de 14 a 16 de setembro.",
  "resultingAllotmentId": null,
  "resultingBlockId": null,
  "outcomeNote": null,
  "createdBy": { "id": "usr_01J2M8HGK7D23R", "displayName": "Ana Souza" },
  "processedBy": null,
  "createdAt": "2026-07-26T11:02:00Z",
  "updatedAt": "2026-07-26T11:02:00Z"
}
```

O exemplo mostra o caso do PRD: aviso recebido às 00h40 de Brasília (03h40 UTC), fora da janela. `slaStartsAt` vai para 08h00 do próximo período útil e a solicitação entra no topo da fila por `priority: emergency`.

Registrar a solicitação **não** altera o inventário. O vínculo com a alteração acontece quando o operador envia `requestId` no POST de allotment ou de bloqueio.

### `GET /api/v1/inventory-requests` — RF-04

**Query:** `_page`, `_size`, `propertyId`, `status`, `requestType`, `priority`, `channel`, `overdue`, `sort`, `order`.

Ordenação padrão `priorityThenReceivedAt` ascendente: avisos emergenciais primeiro, depois por horário de recebimento. É essa ordem que garante que o aviso de madrugada seja a primeira ação da abertura da janela.

### `POST /api/v1/inventory-holds` — RF-06 · Onda B

**Header obrigatório:** `Idempotency-Key`.
**Consumido por:** D03 — início do checkout. O viajante nunca chama diretamente.

```json
{
  "reservationIntentId": "c7b9e401-63af-4a28-9d15-3e806f2b7c4d",
  "accommodationId": "7332fc6e-71aa-43cd-90b4-d2fd99c97787",
  "checkIn": "2026-09-14",
  "checkOut": "2026-09-17",
  "units": 1
}
```

Resposta `201`:

```json
{
  "id": "5d2f8b41-9c3a-4e07-b6d8-71a4f0e2c983",
  "reservationIntentId": "c7b9e401-63af-4a28-9d15-3e806f2b7c4d",
  "propertyId": "9547f6b8-c85d-47b6-a683-13306c20f862",
  "accommodationId": "7332fc6e-71aa-43cd-90b4-d2fd99c97787",
  "checkIn": "2026-09-14",
  "checkOut": "2026-09-17",
  "nights": 3,
  "units": 1,
  "status": "held",
  "heldAt": "2026-07-26T13:12:00Z",
  "expiresAt": "2026-07-26T13:27:00Z",
  "releasedAt": null,
  "releaseReason": null,
  "committedAt": null,
  "reservationId": null,
  "invalidatedByBlockId": null
}
```

Recusa por saldo insuficiente — **nenhuma capacidade é separada**:

```json
{
  "type": "https://api.localizestay.com/problems/insufficient-availability",
  "title": "Saldo insuficiente para a estadia",
  "status": 422,
  "detail": "Não há saldo disponível em ao menos uma noite da estadia.",
  "instance": "/api/v1/inventory-holds",
  "code": "INSUFFICIENT_AVAILABILITY",
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
  "metadata": { "unavailableDates": ["2026-09-15"] }
}
```

É a mesma resposta que a intenção perdedora recebe quando duas jornadas concorrem pela última unidade: apenas uma retenção é criada.

### `DELETE /api/v1/inventory-holds/{holdId}` — RF-07 · Onda B

**Query opcional:** `reason` (`reservationNotCompleted`, `checkoutAbandoned`, `operationalCorrection`).

Idempotente por desenho: retenção já expirada, já liberada ou já invalidada retorna `204` **sem devolver capacidade duas vezes**. Retenção já comprometida retorna `409 HOLD_ALREADY_COMMITTED` — liberar inventário de reserva confirmada pertence a D03.

### `POST /api/v1/inventory-holds/{holdId}/commitment` — RF-08 · Onda B

**Header obrigatório:** `Idempotency-Key`.

```json
{ "reservationId": "8a1e6f2c-4b90-4d75-8e33-c2f7b0d95a41" }
```

Resposta `201`:

```json
{
  "holdId": "5d2f8b41-9c3a-4e07-b6d8-71a4f0e2c983",
  "reservationId": "8a1e6f2c-4b90-4d75-8e33-c2f7b0d95a41",
  "propertyId": "9547f6b8-c85d-47b6-a683-13306c20f862",
  "accommodationId": "7332fc6e-71aa-43cd-90b4-d2fd99c97787",
  "checkIn": "2026-09-14",
  "checkOut": "2026-09-17",
  "units": 1,
  "committedAt": "2026-07-26T13:19:00Z",
  "revalidated": false
}
```

Com a retenção vigente, a capacidade migra de retida para comprometida **sem alterar o total disponível**. Com a retenção já expirada, o saldo é reavaliado: havendo disponibilidade, `revalidated: true`; não havendo, `422 COMMITMENT_WITHOUT_AVAILABILITY`, sem comprometer capacidade inexistente, e a divergência segue para D03 e D07.

### `GET /api/v1/inventory-metrics`

**Query:** `from` e `to` (obrigatórios), `propertyId`.

```json
{
  "period": { "startDate": "2026-09-01", "endDate": "2026-09-30" },
  "unbackedSales": { "count": 0, "target": 0 },
  "offersWithoutAllotment": { "count": 0, "target": 0 },
  "slaCompliance": { "processedWithinSla": 23, "totalProcessed": 23, "percentage": 100 },
  "inventoryCoverage": {
    "propertiesWithActiveAllotment": 8,
    "propertiesMeetingCommercialFloor": 7,
    "target": 8
  },
  "emergencyBlockLatency": {
    "sampleSize": 4,
    "p95Seconds": 3.8,
    "maxSeconds": 6.1,
    "targetSeconds": 60
  },
  "outOfWindowExposure": { "confirmedReservations": 0, "pendingEmergencyRequests": 1 },
  "holdExpiration": null
}
```

`holdExpiration` é `null` enquanto a Onda B não estiver em produção.

## Schemas de entidades

### Allotment

| Campo | Tipo | Obrigatório | Nullable | Descrição |
|---|---|:--:|:--:|---|
| `id` | UUID | Sim | Não | Identificador do allotment |
| `propertyId` | UUID | Sim | Não | Propriedade |
| `accommodationId` | UUID | Sim | Não | Acomodação cedida |
| `accommodationName` | string | Sim | Não | Nome comercial da acomodação |
| `units` | integer (1–999) | Sim | Não | Unidades cedidas, uniformes em todo o período |
| `startDate` | date | Sim | Não | Primeira data, inclusiva |
| `endDate` | date | Sim | Não | Última data, inclusiva |
| `status` | enum | Sim | Não | `active`, `expired`, `cancelled` |
| `belowCommercialFloor` | boolean | Sim | Não | `true` quando `units < 2` |
| `contractReference` | string | Não | Sim | Instrumento contratual |
| `requestId` | UUID | Não | Sim | Solicitação que originou a cessão |
| `notes` | string | Não | Sim | Observações |
| `cancellationReason` | string | Não | Sim | Preenchido apenas em `cancelled` |
| `revision` | integer | Sim | Não | Incrementada a cada alteração |
| `createdBy` / `updatedBy` | StaffActor | Sim / Não | Não / Sim | Trilha de auditoria |
| `createdAt` / `updatedAt` | datetime | Sim | Não | ISO 8601 UTC |

### InventoryBlock

| Campo | Tipo | Obrigatório | Nullable | Descrição |
|---|---|:--:|:--:|---|
| `id` | UUID | Sim | Não | Identificador do bloqueio |
| `accommodationId` | UUID | Sim | Não | Acomodação afetada |
| `type` | enum | Sim | Não | `planned`, `emergency` |
| `origin` | enum | Sim | Não | `partnerRequest`, `internalOperation`, `curationSuspension` |
| `reason` | enum | Sim | Não | Motivo obrigatório do bloqueio |
| `reasonNote` | string | Não | Sim | Obrigatório quando `reason` é `other` |
| `units` | integer | Sim | Não | Unidades retiradas por data |
| `blocksEntireAccommodation` | boolean | Não | Não | `true` quando zera a capacidade |
| `startDate` / `endDate` | date | Sim | Não | Período inclusivo |
| `status` | enum | Sim | Não | `active`, `removed` |
| `requestId` | UUID | Não | Sim | Solicitação de origem |
| `affectedReservationCount` | integer | Não | Não | Reservas alcançadas; sempre 0 em `planned` |
| `invalidatedHoldCount` | integer | Não | Não | Retenções invalidadas; sempre 0 em `planned` |
| `salesStoppedAt` | datetime | Não | Sim | Base da métrica de latência |
| `removedAt` / `removalReason` / `removedBy` | — | Não | Sim | Preservados no histórico |

### DailyInventory

| Campo | Tipo | Obrigatório | Descrição |
|---|---|:--:|---|
| `date` | date | Sim | Data da diária |
| `allottedUnits` | integer ≥ 0 | Sim | Total cedido; 0 quando não há allotment |
| `committedUnits` | integer ≥ 0 | Sim | Reservas confirmadas |
| `heldUnits` | integer ≥ 0 | Sim | Retenções vigentes; 0 antes da Onda B |
| `blockedUnits` | integer ≥ 0 | Sim | Bloqueios ativos |
| `availableUnits` | integer ≥ 0 | Sim | Saldo vendável, com piso zero |
| `blocks` | array | Sim | Bloqueios ativos da data; `[]` quando não há |

### InventoryRequest

| Campo | Tipo | Obrigatório | Descrição |
|---|---|:--:|---|
| `channel` | enum | Sim | `whatsapp`, `email` |
| `requestType` | enum | Sim | `allotmentGrant`, `allotmentChange`, `block`, `blockRemoval` |
| `priority` | enum | Sim | `emergency`, `standard` — derivada de `emergency` no request |
| `status` | enum | Sim | `pending`, `inProgress`, `processed`, `cancelled` |
| `receivedAt` | datetime | Sim | Horário real da mensagem, não do registro |
| `receivedOutsideWindow` | boolean | Sim | Derivado da janela seg–sáb 08h–20h |
| `slaStartsAt` | datetime | Sim | `receivedAt` dentro da janela; 08h do próximo período útil fora dela |
| `slaDueAt` | datetime | Sim | Quatro horas úteis a partir de `slaStartsAt` |
| `processedWithinSla` | boolean | Não | Calculado no fechamento; `null` enquanto pendente |
| `resultingAllotmentId` / `resultingBlockId` | UUID | Não | Alteração que a solicitação originou |

### InventoryHold

| Campo | Tipo | Obrigatório | Descrição |
|---|---|:--:|---|
| `reservationIntentId` | UUID | Sim | Intenção de reserva de D03 |
| `checkIn` / `checkOut` | date | Sim | `checkIn` inclusivo, `checkOut` exclusivo |
| `nights` / `units` | integer | Sim | Noites retidas e unidades separadas |
| `status` | enum | Sim | `held`, `expired`, `released`, `committed`, `invalidated` |
| `expiresAt` | datetime | Sim | Derivado do parâmetro global de 15 minutos |
| `invalidatedByBlockId` | UUID | Não | Bloqueio emergencial que invalidou a retenção |

## Códigos de erro

Formato RFC 9457 em `application/problem+json`, com `code`, `traceId` e, quando aplicável, `errors` e `metadata`.

| HTTP | `code` | Descrição |
|---|---|---|
| 400 | `BAD_REQUEST` | Parâmetro ausente ou sintaticamente inválido |
| 401 | `UNAUTHORIZED` | Token ausente, inválido ou expirado |
| 403 | `FORBIDDEN` | Identidade sem a permissão declarada |
| 404 | `PROPERTY_NOT_FOUND` | Propriedade inexistente |
| 404 | `ACCOMMODATION_NOT_FOUND` | Acomodação inexistente ou de outra propriedade |
| 404 | `ALLOTMENT_NOT_FOUND` · `BLOCK_NOT_FOUND` · `REQUEST_NOT_FOUND` · `HOLD_NOT_FOUND` | Recurso inexistente |
| 409 | `ALLOTMENT_PERIOD_OVERLAP` | Período sobreposto na mesma acomodação |
| 409 | `REVISION_MISMATCH` | `expectedRevision` obsoleto |
| 409 | `BLOCK_ALREADY_REMOVED` | Bloqueio já removido |
| 409 | `REQUEST_ALREADY_CLOSED` | Solicitação processada ou cancelada não volta para pendente |
| 409 | `HOLD_ALREADY_COMMITTED` | Retenção já convertida em comprometimento |
| 409 | `IDEMPOTENCY_KEY_REUSED` | Mesma chave com corpo diferente |
| 422 | `INVALID_DATE_RANGE` | `endDate`/`checkOut` incompatível com o início |
| 422 | `DATE_RANGE_TOO_LARGE` | Calendário acima de 92 dias ou estadia acima de 30 noites |
| 422 | `ALLOTMENT_BELOW_COMMITTED` | Redução abaixo do comprometido; `metadata.conflictingDates` |
| 422 | `INSUFFICIENT_FREE_BALANCE` | Bloqueio planejado maior que o saldo livre; `metadata.freeBalanceByDate` |
| 422 | `EMERGENCY_BLOCK_CONFIRMATION_REQUIRED` | Bloqueio emergencial sem confirmação explícita |
| 422 | `REASON_NOTE_REQUIRED` | `reason: other` sem `reasonNote` |
| 422 | `CURATION_BLOCK_NOT_REMOVABLE` | Bloqueio de suspensão de D06 só termina com a retomada da aprovação |
| 422 | `INSUFFICIENT_AVAILABILITY` | Saldo insuficiente em ao menos uma noite; `metadata.unavailableDates` |
| 422 | `COMMITMENT_WITHOUT_AVAILABILITY` | Retenção expirada e sem saldo para comprometer |
| 429 | `RATE_LIMIT_EXCEEDED` | Limite de requisições excedido; header `Retry-After` |
| 500 | `INTERNAL_ERROR` | Falha interna — usar `traceId` nos logs |

## Eventos de domínio

Rodam in-process com outbox transacional (ADR-0002). Os schemas de payload estão em `components/schemas` e o mapa em `x-domain-events`.

### Produz

| Evento | Onda | Gatilho | Consumidores |
|---|:--:|---|---|
| `oferta-inventario.inventario-bloqueado` | A | Bloqueio aplicado, planejado ou emergencial | D01, D07, D09 |
| `oferta-inventario.bloqueio-afeta-reserva` | A | Bloqueio emergencial ou suspensão alcança reserva confirmada | D05 |
| `oferta-inventario.inventario-retido` | B | Retenção criada no início do checkout | D03, D09 |
| `oferta-inventario.retencao-expirada` | B | Prazo terminou sem confirmação | D03, D09 |
| `oferta-inventario.inventario-liberado` | B | Capacidade retida ou bloqueada volta a ser vendável | D01, D03, D09 |
| `oferta-inventario.inventario-comprometido` | B | Retenção convertida pela confirmação da reserva | D03, D07, D09 |

### Consome

| Evento | Onda | Efeito |
|---|:--:|---|
| `reserva.intencao-iniciada` | B | Equivale a `POST /inventory-holds` |
| `reserva.confirmada` | B | Equivale a `POST /inventory-holds/{holdId}/commitment` |
| `reserva.nao-concluida` | B | Equivale a `DELETE /inventory-holds/{holdId}`; idempotente |
| `curadoria-qualidade.propriedade-aprovada` | A | Satisfaz `propertyApproved` e encerra o bloqueio `curationSuspension` |
| `curadoria-qualidade.propriedade-suspensa` | A | Cria bloqueio `curationSuspension` em todas as acomodações, efeito equivalente ao emergencial, sem cancelar reservas |

## Estados e regras

- `availableUnits` nunca é negativo e nunca é indefinido: data sem allotment vale zero.
- Bloqueio planejado só alcança saldo livre; jamais retenção vigente ou reserva confirmada.
- Bloqueio emergencial é sempre aceito, invalida retenções e **não** cancela nem altera reserva alguma.
- Reduzir allotment abaixo do comprometido é proibido — a operação correta é registrar bloqueio.
- Remover bloqueio devolve capacidade e preserva o histórico do bloqueio removido.
- Retenção tem duração global de 15 minutos e não é parametrizada pelo cliente nas Ondas A e B.
- Liberação de retenção é idempotente: capacidade nunca é devolvida duas vezes.
- Comprometer uma retenção vigente não altera o total disponível — a capacidade migra de retida para comprometida.
- Aplicação de allotment ou bloqueio grava autor, horário e motivo na trilha de auditoria.
- Nenhum canal humano altera o inventário automaticamente: registrar solicitação não aplica alteração.

## Como usar este contrato

### Backend
Implemente os endpoints conforme o YAML. `x-backend-notes` traz hints por operação — índices, transação, outbox e pontos de concorrência. Use a skill `flow-techspec-creator` referenciando `api-contract.yaml`.

### Frontend
```bash
npx openapi-typescript api-contract.yaml -o src/types/inventory-api.ts
npx @stoplight/prism-cli mock api-contract.yaml   # mock em http://localhost:4010
```
Use a skill `flow-frontend-techspec-creator` — os schemas são a fonte de verdade dos tipos.

### Testes de contrato
```bash
npx dredd api-contract.yaml http://localhost:5000
```

## Questões em aberto

- [ ] Confirmar o calendário de feriados aplicável ao SLA de quatro horas úteis — depende da definição da escala que sustenta a janela seg–sáb 08h–20h (questão em aberto do PRD, responsável: Operação com D07).
- [ ] Validar com D03 os nomes `reservationIntentId` e `reservationId` antes da Onda B; o contrato de D03-C01 ainda não existe.
- [ ] Confirmar se D01 precisa de um endpoint de disponibilidade multi-propriedade por destino ou se a busca resolve isso na própria camada de D01 antes de chamar `/availability`.
- [ ] Definir o limite de janela de consulta pública (`checkOut` até 30 noites) com base no comportamento real de busca.
- [ ] Decidir se o painel de retenções vigentes (Phase 2) entra como `GET /inventory-holds` paginado ou como projeção de métricas.
- [ ] Confirmar com Segurança se `GET /availability` público exige rate limit por IP mais restritivo que o padrão da plataforma.
