# Product Achievement Foundation

## Ownership and identity

Product achievements are VectorQuake product-domain facts. They are separate from store-specific achievement transport and configuration. The first canonical product ID is `campaign.complete`, exposed as `GameAchievementIds.NormalCampaignComplete`; it is not a Steam API Name.

The code-defined `GameAchievementCatalog` currently contains one one-shot definition. The catalog intentionally has no localized copy, icon, progress rule, AppID, provider metadata, or store mapping.

## Product-global durable state

The product-global document is `Saves/achievements.json`, schema version 1:

```text
SchemaVersion
EarnedAchievementIds[]
PendingAchievementPublicationIds[]
```

`EarnedAchievementIds` records durable product truth. `PendingAchievementPublicationIds` is the external-publication outbox, and pending is always a subset of earned. A new earn adds both values in one atomic document write before any publisher call. Accepted or already-satisfied publication removes only the pending value with a later atomic write.

The document has no save-slot identity. Delete Slot, New Game, campaign-slot clearing, active-slot changes, and profile migration do not reset product achievements. Unknown but syntactically valid product IDs are preserved for forward compatibility and excluded from publication until the current catalog knows them. A valid backup is restored before a missing primary is treated as a new document, closing the atomic fallback interruption window. Corrupt, schema-invalid, and unsupported documents fail closed; after quarantine, the artifact keeps later loads fail-closed until explicit repair, so they are not silently replaced with an empty ledger.

## Dependency and publication boundaries

`Game.Product.Achievements.Domain` has no Stage, Gameplay, UI, Platform, or store transport reference. `FileProductAchievementRepository` depends on the product-owned `IAchievementTextStore`. The composition-only `StageAtomicAchievementTextStoreAdapter` wraps the existing atomic file implementation without moving or reversing the Stage save module.

`IAchievementPublicationSink` is store-neutral and supports synchronous or asynchronous exactly-once callbacks. The coordinator registers an in-flight attempt before calling `Publish`, ignores duplicate or late-after-disposal callbacks, contains publisher exceptions, and never publishes before the earned/outbox transition is durable. Initialization reconciles every catalog-known earned ID once per coordinator session, not only pending IDs; no per-frame retry loop exists.

DirectWindows composition uses `UnavailableAchievementPublicationSink`. Earning remains active and pending remains durable while an external publisher is unavailable.

## Deferred boundaries

- Gameplay and campaign-completion earning integration are not implemented in M7B-2G-A.
- `NORMAL_CAMPAIGN_COMPLETE` is only the first candidate; a durable receipt that distinguishes normal completion from DirectPlay or force-clear provenance must be decided before the Gameplay hook.
- Steam mapping, Steam publisher behavior, Actual AppID validation, and player builds are deferred.
- Achievement UI, localized presentation, toast behavior, Cloud allowlisting, and multi-device conflict policy are deferred.

`M7B2GB_DURABLE_NORMAL_COMPLETION_RECEIPT_DECISION_PENDING`
