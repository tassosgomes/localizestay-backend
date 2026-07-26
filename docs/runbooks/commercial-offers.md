# Commercial offers runbook

This runbook operates F02, the Inventory capability that structures a property's commercial offer
(accommodations, policies, rates), runs independent validation, submits the offer to Curation and
records returns. It reuses the platform's global OpenTelemetry/OTLP pipeline, health probes and
rate limiter; it adds no new infrastructure. It does not automate WhatsApp or email.

## Configuration and local setup

Run PostgreSQL with `docker compose -f docker-compose.dev.yml up -d`, then set the following
non-secret configuration. Production values come from the deployment secret store, never from
source control.

| Setting | Purpose |
| --- | --- |
| `ConnectionStrings__LocalizeStay` | PostgreSQL connection string |
| `LogTo__Issuer`, `LogTo__Audience` | LogTo JWT validation |
| `InventoryEligibility__PreselectedPartnerIds` | Pilot pre-selection allow-list |
| `InventoryEligibility__ApprovedDestinationIds` | Pilot approved-destination allow-list |
| `BusinessCalendar__TimeZone`, `BusinessCalendar__BusinessHours__*`, `BusinessCalendar__Holidays` | Versioned SLA calendar (2-business-day submission target, 4-business-hour communication target) |
| `LegalPolicies__Cancellation__RuleSetVersion`, `LegalPolicies__NoShow__RuleSetVersion`, `LegalPolicies__Deposit__RuleSetVersion`, `LegalPolicies__Guarantee__RuleSetVersion` | Approved legal `ruleSetVersion` per policy type; the catalog is validated at startup |
| `OpenTelemetry__OtlpEndpoint` | Optional OTLP collector endpoint |

Start the host with `dotnet run --project src/LocalizeStay.Api`. Use `/health/live` and
`/health/ready` for liveness and readiness probes. Liveness never queries PostgreSQL; readiness
aggregates each module's own database check (Inventory registers `inventory-database`).

## Migration, backfill and rollback

The F02 migration (`20260723015655_AddCommercialOffers`) creates nine tables in the `inventory`
schema with their foreign keys, indexes and the `commercial_offer_idempotency_keys` unique index.
A deterministic backfill creates one `CommercialOffer` row per existing `IncorporatedProperty`
(`ON CONFLICT DO NOTHING`), so F01 properties become operable by F02 immediately.

1. Take a PostgreSQL backup/snapshot before deploying.
2. Deploy the new image. The host applies migrations on startup before starting the outbox
   processors (`ModuleDatabaseMigrationService<TDbContext>` runs first).
3. Inspect the log line reporting migrations completed before sending traffic.
4. Re-running the backfill is safe: `ON CONFLICT DO NOTHING` keeps it idempotent.

Rollback is application-first, schema-last:

- **Application rollback (preferred):** redeploy the previous compatible image. The F02 tables are
  left in place; the previous image simply does not write to them. No destructive migration runs.
- **Schema rollback (last resort):** restore the PostgreSQL snapshot taken before deploy. There is
  no automatic `Down` migration in the deployment pipeline — restoring the snapshot is the only
  way to drop the F02 schema destructively. Never run a hand-written `DROP TABLE` in production.

## Smoke tests

After deployment, authenticate with a staff token carrying the commercial-offers permissions and
run these checks:

1. `GET /health/live` returns 200 without touching PostgreSQL; `GET /health/ready` returns 200
   with the `inventory-database` check reporting healthy.
2. `GET /api/v1/commercial-offers?_page=1&_size=1` returns 200 for `commercial-offers:read`.
3. Create a disposable property's offer, add one accommodation, one policy and one rate; validate
   with a second operator; submit; then confirm the snapshot row and the
   `oferta-inventario.oferta-estruturada` outbox row exist.
4. Confirm unauthenticated requests return 401 and insufficient-permission requests return 403 as
   `application/problem+json`.

## Outbox inspection and replay

Each module owns its outbox in `<schema>.outbox_messages`; Inventory's lives in
`inventory.outbox_messages`. The in-process `OutboxProcessor<InventoryDbContext>` polls every five
seconds and retries up to five times. Inspect pending or exhausted messages with:

```sql
SELECT id, type, occurred_on_utc, processed_on_utc, retry_count, error
FROM inventory.outbox_messages
WHERE processed_on_utc IS NULL
ORDER BY occurred_on_utc;
```

To replay a message that failed due to a transient consumer issue (not a poison payload), reset
its retry counter so the processor picks it up on the next poll:

```sql
UPDATE inventory.outbox_messages
SET retry_count = 0, error = NULL
WHERE id = '<message-id>';
```

Duplicate publishing is safe: `CurationOfferReturnedHandler` deduplicates returns by `event_id`,
and `SubmitCommercialOfferCommandHandler` deduplicates submissions by the idempotency key
(`commercial_offer_idempotency_keys`). Never manually insert into the outbox; only reset retries
on existing rows whose payload is valid.

## Telemetry, dashboards and alerts

Export traces, logs and metrics with OTLP. F02 instruments the
`LocalizeStay.Inventory.Lifecycle` `ActivitySource` and `Meter`. The full instrument set:

**Metrics (bounded tags only — `operation`, `result`, `status`):**

| Metric | Type | Tags | Source |
| --- | --- | --- | --- |
| `inventory.commercial_offer.created` | counter | `result` | draft creation on first `GET` |
| `inventory.commercial_offer.mutation` | counter | `operation` | accommodation/policy/rate create/update/delete |
| `inventory.commercial_offer.validation` | counter | `result` | `POST .../validation` |
| `inventory.commercial_offer.validation_invalidated` | counter | — | any mutation that clears `CurrentValidation` |
| `inventory.commercial_offer.submission` | counter | `result` | `POST .../submission` |
| `inventory.commercial_offer.returned` | counter | `result` | `CurationOfferReturnedV1` handler |
| `inventory.commercial_offer.rate_overlap` | counter | `operation` | `RATE_PERIOD_OVERLAP` rejections |
| `inventory.commercial_offer.submission_duration` | histogram (s) | — | `now - offer.CreatedAt` on submit |
| `inventory.commercial_offer.outbox_failure` | counter | — | submit `DbUpdateException` |

**Custom spans:** `inventory.commercial_offer.load`, `.validate`, `.submit`, `.return`,
`.metrics`. Span tags carry identifiers (`property_id`, `revision`, `validation_id`,
`submission_id`, `event_id`); the optional `X-Correlation-Id` header is propagated end to end.

**Log scopes** carry `propertyId`, `offerRevision`, `operation`, `result`, `validationId`,
`submissionId`, `eventId` and `correlationId`. Templates never embed full prices, snapshots,
comments, legal text, tokens or PII.

### Alerts

| Alert | Condition | Severity | Owner |
| --- | --- | --- | --- |
| Outbox stuck | `outbox.retry.exhausted{module="InventoryDbContext"}` OR `inventory.commercial_offer.outbox_failure` increases | High | Operations |
| Persistence/concurrency spike | `REVISION_MISMATCH` or `DbUpdateConcurrencyException` rate > baseline for 10 min | Medium | Inventory eng |
| Submission without validation | any `inventory.commercial_offer.submission{result="success"}` where the offer had no `CurrentValidation` (invariant violation) | Critical | Inventory eng |
| SLA breach | `inventory.onboarding.submission.duration` p95 > 2 business days, or `inventory.communication.sla{result="outside_sla"}` > 0 in pilot | Medium | Operations |
| Rate overlap storm | `inventory.commercial_offer.rate_overlap` rate > baseline | Low | Inventory eng |

SLA and rework thresholds are calibrated during the pilot; the dashboard must show numerator and
denominator for every ratio (`GetCommercialOfferMetricsQueryHandler` exposes them explicitly).

## SLI / SLO

| SLI | SLO (pilot) | Source |
| --- | --- | --- |
| Offer completeness within target | ≥ 80% of offers reach `CompleteInformationReceivedAt` | `commercial_offer` metrics query |
| Submission within 2 business days of completeness | ≥ 90% | `submission_duration` + business calendar |
| First-review acceptance | ≥ 80% (`(submitted - returned) / submitted`) | submissions vs returns |
| Communication processed within 4 business hours | 100% during pilot | `inventory.communication.sla` |
| Dual validation coverage | tracked (target pending ratification) | metrics query |

## Troubleshooting

### `REVISION_MISMATCH` on concurrent edits

The `Revision` column is an EF Core concurrency token. Two operators editing the same offer
serialise: the loser gets `REVISION_MISMATCH` (HTTP 409) and must reload via `GET` to pick up the
new revision. The dashboard should surface spikes; a sustained spike indicates contention on a
hot property and should be triaged with the commercial team.

### `RATE_PERIOD_OVERLAP`

Two rates with the same `accommodationId`, `conditionCode`, `policyId` and `mealPlan` overlap in
`[validFrom, validTo]`. The domain rejects this before persistence; the
`inventory.commercial_offer.rate_overlap` counter records the operation (`create`/`update`). The
operator must narrow one rate's window or change one of the discriminating fields.

### Submission replayed with a different payload

`SubmitCommercialOfferCommandHandler` fingerprints the canonical payload
(`propertyId`, `submissionId`, `validationId`, `expectedRevision`) and stores it in
`commercial_offer_idempotency_keys`. Reusing the same `submissionId` with a different fingerprint
returns `IDEMPOTENCY_KEY_REUSED` (HTTP 409). Generate a new `submissionId` for the new attempt.

### Curation return ignored

`CurationOfferReturnedHandler` ignores an event when the offer is already `Returned` or when the
`event_id` was already processed (deduplication). The handler logs a warning with `eventId`,
`propertyId` and `submissionId`; no metric is incremented for ignored events. Investigate only if
the return is genuinely new and still ignored (property not found means an out-of-order event for
a property that has not reached F02 yet).

### Outbox not draining

If `inventory.outbox_messages` grows, confirm the `OutboxProcessor<InventoryDbContext>` hosted
service is running (it is registered by `AddModuleDatabase` and starts after migrations). A
message reaching `retry_count = 5` triggers `outbox.retry.exhausted` and is left for manual
replay (see above). Inspect the `error` column for the failure reason before resetting.

## Acceptance blockers tracked

| Decision | Owner | Release condition |
| --- | --- | --- |
| Final `ruleSetVersion` per policy type | Legal + Commercial | Approved versions configured before operating real money |
| `commercial-offers:*` permission names | Identity & Access | Ratified in the permission catalogue |
| `CurationOfferReturnedV1` payload | Curation | Payload approved before RF-06 end-to-end certification |
| Dual-validation authorship rule | Operations | Reviewer-must-differ rule ratified regardless of permission granted |
