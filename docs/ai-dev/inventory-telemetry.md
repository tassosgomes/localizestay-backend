# Inventory telemetry alerts

The platform must scrape the `LocalizeStay.Inventory.Lifecycle` and `LocalizeStay.Outbox`
meters. Alert during the pilot when either condition occurs:

- `outbox.retry.exhausted` has a sample with `module=InventoryDbContext`; this is emitted when a
  pending outbox message reaches its fifth failed attempt.
- the ratio of `inventory.communication.sla{result="within_sla"}` to all
  `inventory.communication.sla` samples is below `1` (100%).

`inventory.onboarding.*` counters and `inventory.onboarding.submission.duration` provide the
onboarding lifecycle dashboard. Instrument tags are limited to destination, lifecycle, gate and
result values; no partner, onboarding, document, token or message-content value is a metric tag.
