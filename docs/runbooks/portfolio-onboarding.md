# Portfolio onboarding runbook

This runbook operates F01, the Inventory capability that incorporates a pre-selected hotel partner and its properties before Curation. It does not automate WhatsApp or email: staff record only the processed result through `communication-records`.

## Configuration and local setup

Run PostgreSQL with `docker compose -f docker-compose.dev.yml up -d`, then set the following non-secret configuration. Production values come from the deployment secret store, never from source control.

| Setting | Purpose |
| --- | --- |
| `ConnectionStrings__LocalizeStay` | PostgreSQL connection string |
| `LogTo__Issuer`, `LogTo__Audience` | LogTo JWT validation |
| `LogTo__ScopeClaimType`, `LogTo__PermissionClaimType` | Defaults are `scope` and `permission` |
| `InventoryEligibility__PreselectedPartnerIds` | Pilot pre-selection allow-list |
| `InventoryEligibility__ApprovedDestinationIds` | Pilot approved-destination allow-list |
| `BusinessCalendar__TimeZone`, `BusinessCalendar__BusinessHours__*`, `BusinessCalendar__Holidays` | Versioned SLA calendar |
| `OpenTelemetry__OtlpEndpoint` | Optional OTLP collector endpoint |

Start the host with `dotnet run --project src/LocalizeStay.Api` and use `/health/live` and `/health/ready` for liveness and readiness probes.

## Migration, rollback and smoke tests

Apply migrations through the normal deployment command before enabling traffic. The inventory migration is transactional; take a PostgreSQL backup/snapshot first. Roll back by deploying the previous compatible image and restoring the snapshot only when a schema rollback is required—no destructive migration is automatic.

After deployment, authenticate with a staff token and run these smoke tests:

1. `GET /health/live` and `GET /health/ready` return 200.
2. `GET /api/v1/partners?_page=1&_size=1` returns 200 for `portfolio-onboarding:read`.
3. Create a disposable partner and onboarding, validate the six gates, submit, then verify history and the outbox event.
4. Confirm unauthenticated and insufficient-permission requests return `application/problem+json` with 401 and 403.

## Telemetry, dashboards and alerts

Export traces, logs and metrics with OTLP. The dashboard uses `LocalizeStay.Inventory.Lifecycle` counters (`inventory.onboarding.*`), `inventory.onboarding.submission.duration`, and `inventory.communication.sla`. Alert immediately when `outbox.retry.exhausted{module="InventoryDbContext"}` is observed or the `within_sla / total` communication ratio is below `1`. Tags must never contain partner, property, document, message, token, or legal-identifier data. See `docs/ai-dev/inventory-telemetry.md` for the metric names.

## Acceptance blockers recorded

The five external decisions are tracked as release blockers; none is guessed by this service:

| Decision | Owner | Release condition |
| --- | --- | --- |
| LogTo issuer, audience and claims | Platform | Secret-backed production configuration validated |
| Pilot calendar, holidays and timezone | Operations | Versioned calendar published |
| Partner pre-selection and destination governance | Commercial | Pilot allow-lists approved |
| Legal identifiers and accepted documents | Legal | Rules approved and operational evidence configured |
| Official contract repository reference | Legal + Operations | Repository convention approved before MVP acceptance |
