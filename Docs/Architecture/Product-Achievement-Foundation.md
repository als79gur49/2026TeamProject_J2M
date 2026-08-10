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

The document has no save-slot identity. Delete Slot, New Game, campaign-slot clearing, active-slot changes, and profile migration do not reset product achievements. Unknown but syntactically valid product IDs are preserved for forward compatibility and excluded from publication until the current catalog knows them. A valid backup is restored before a missing primary is treated as a new document, closing the atomic fallback interruption window. Corrupt, excessively nested, schema-invalid, and unsupported documents fail closed; after quarantine, the artifact keeps later loads fail-closed until explicit repair, so they are not silently replaced with an empty ledger.

## Dependency and publication boundaries

`Game.Product.Achievements.Domain` has no Stage, Gameplay, UI, Platform, or store transport reference. `FileProductAchievementRepository` depends on the product-owned `IAchievementTextStore`. The composition-only `StageAtomicAchievementTextStoreAdapter` wraps the existing atomic file implementation without moving or reversing the Stage save module.

`IAchievementPublicationSink` is store-neutral and supports synchronous or asynchronous exactly-once callbacks. The coordinator registers an in-flight attempt before calling `Publish`, ignores duplicate or late-after-disposal callbacks, contains publisher exceptions, and never publishes before the earned/outbox transition is durable. Initialization reconciles every catalog-known earned ID once per coordinator session, not only pending IDs; no per-frame retry loop exists.

DirectWindows composition uses `UnavailableAchievementPublicationSink`. Earning remains active and pending remains durable while an external publisher is unavailable.

## Durable normal Campaign completion receipt

`CampaignSlotDocument.NormalCampaignCompletionReceipt` is a versioned slot fact, not an achievement flag. Version 1 stores only `CompletedStageId`, the authoritative `StageRunId`, and `ClearSource`. It contains no product achievement ID, Steam API Name, AppID, publisher, or publication state, and it is never consumed after product earning.

The receipt is created only after an accepted Victory terminal claim for a cleared objective result in the normal Campaign context. The completed Stage must be a canonical member of the Campaign sequence and its final Stage, the run ID must be valid, `StageClearSource` must be `Objective`, and `EditorDirectPlayContext.Mode` must be `None`. Non-final/custom stages, Force Clear, NonCampaign DirectPlay, CampaignTempSlot DirectPlay, and CampaignProductionSlot DirectPlay create no receipt. Same-tick death remains higher priority than clear and therefore creates no completion receipt.

`CampaignCompleted = true` and the first eligible receipt are assigned inside the same Campaign slot mutation and durable save invocation. A save failure leaves neither change durable. A valid existing receipt is preserved without overwrite, and an invalid existing receipt is retained but is not eligible and is not silently repaired. New Game starts with no receipt, Delete Slot removes that slot fact, and neither operation changes the product-global achievement document. Older schema-v1 profiles that omit the optional field load with a null receipt; the profile schema version remains 1 and unsupported forward versions remain fail-closed.

## Production application composition

The canonical `ApplicationPersistentDataSavePathProvider.SaveRootPath` owns both `profile.json` and `achievements.json` under the product-global `Saves` directory. Product achievement composition does not read slot roots or Editor DirectPlay temporary namespaces.

`ProductAchievementRuntimeBootstrap` creates one plain-C# `ProductAchievementApplicationLifetimeOwner` before the first scene, initializes one `ProductAchievementApplicationHost`, and disposes it at application quit. Scene transitions and Stage retries do not recreate it. The default publisher remains `UnavailableAchievementPublicationSink`; missing-file initialization keeps an in-memory empty document without eagerly creating `achievements.json`.

The application composition has no shared constructor root with the stage-backed scene composition. A single achievement-specific internal `ProductAchievementEarningSinkHandoff` therefore carries only `IProductAchievementEarningSink` across that boundary. The application owner registers one exact sink, a different second registration fails closed, subsystem registration and application disposal clear it, and only scene composition consumes it. Gameplay runtime logic receives a plain `INormalCampaignCompletionAchievementIntegration` constructor dependency and never performs a static lookup or owns/disposes the application sink.

## Receipt-based earning and recovery

`NormalCampaignCompletionAchievementIntegration` is the only mapping owner from the Stage fact to `GameAchievementIds.NormalCampaignComplete`. For an immediate earn it requires both the current completion to pass `NormalCampaignCompletionReceiptPolicy.Evaluate` and the post-save committed slot to have `CampaignCompleted`, receipt presence, a non-null receipt, and a receipt that passes persisted validation. The Campaign slot update completes first; only then is the committed slot loaded and the product sink called. An existing valid receipt is eligible after a new normal final Objective completion, while invalid and present-null receipts remain fail-closed and are not repaired.

`NormalCampaignCompletionAchievementStartupReconciler` runs once per normal, non-batch application session after the product host is initialized and the first scene is loaded. Batch test/capture processes are excluded before profile access. It obtains the canonical production Campaign store through `CampaignSaveCompositionProvider`, whose factory completes profile migration before the read, and uses `LoadAllWithReport` rather than reading `profile.json` or backup files. Missing/empty profiles are a no-op; blocked or failed load states earn nothing. A backup-recovered canonical result is eligible. Multiple valid slot receipts collapse to one sink invocation, and bare `CampaignCompleted` never earns.

Every `EditorDirectPlayMode` skips startup profile reading and immediate current-completion earning, including `CampaignProductionSlot`. Product persistence failures, unavailable state, invalid configuration results, and unexpected earning exceptions are contained after the Campaign save; they do not roll back Campaign completion or block terminal/GameClear flow. The durable receipt remains the next normal startup's recovery source.

## Deferred boundaries

- Steam mapping, Steam publisher behavior, Actual AppID validation, and player builds are deferred.
- Achievement UI, localized presentation, toast behavior, Cloud allowlisting, and multi-device conflict policy are deferred.

Steam publisher and product-to-Steam mapping are deferred to M7B-2G-C.

`M7B2GB0_DURABLE_NORMAL_COMPLETION_RECEIPT_READY`

`M7B2GB_RECEIPT_BASED_CAMPAIGN_COMPLETE_EARNING_READY`
