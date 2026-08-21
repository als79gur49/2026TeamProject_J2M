# Product Achievement Foundation

## Ownership and identity

Product achievements are VectorQuake product-domain facts. They are separate from store-specific achievement transport and configuration. The canonical product IDs are `campaign.complete`, `campaign.stage-1-2.clear`, and `campaign.stage-1-2.push-flip-within-25`; none is a Steam API Name.

The code-defined `GameAchievementCatalog` currently contains three one-shot definitions. The catalog intentionally has no localized copy, icon, AppID, provider metadata, or store mapping. Stage and action-count eligibility is owned by the Campaign integration rule catalog rather than embedded in the product ledger.

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

`CampaignSlotDocument.NormalCampaignCompletionReceipt` is a versioned slot fact, not an achievement flag. The current writer emits version 2, whose only semantic field is `CompletedStageId`. The serialized `StageRunId` and `ClearSource` fields remain in the physical shape solely so version-1 saves can be read losslessly; version 2 writes explicit legacy-absence values and does not interpret them as provenance. The receipt contains no product achievement ID, Steam API Name, AppID, publisher, or publication state.

Version 1 remains readable under its original persisted contract: canonical `CompletedStageId`, a nonblank legacy `StageRunId`, and the legacy Objective clear-source value. Unknown receipt versions, present-null receipts, invalid receipts, and a completed Campaign with no receipt fail closed. They are not repaired, inferred, or automatically rewritten to version 2. This dual-read/new-write policy does not change the profile root schema version 1 or the retained PlayerPrefs root schema version 2.

`CampaignGameplayFlowController` owns normal-completion eligibility. It creates the transient internal `NormalCampaignCompletionFact(StageId)` only after an accepted Victory terminal claim with a non-null `TickResult`, `ObjectiveResult.ClearedThisTick`, matching Stage and final tick, `EditorDirectPlayContext.Mode == None`, and serialized `CampaignStageSequenceResolver` membership/finality. Non-final stages, Force Clear, NonCampaign DirectPlay, CampaignTempSlot DirectPlay, and CampaignProductionSlot DirectPlay create no fact or receipt. Same-tick death remains higher priority than clear and therefore creates no completion fact.

`CampaignCompleted = true` and the first eligible receipt are assigned inside the same Campaign slot mutation and durable save invocation. A save failure leaves neither change durable. A valid existing receipt is preserved without overwrite, and an invalid existing receipt is retained but is not eligible and is not silently repaired. New Game starts with no receipt, Delete Slot removes that slot fact, and neither operation changes the product-global achievement document. Older schema-v1 profiles that omit the optional field load with a null receipt; the profile schema version remains 1 and unsupported forward versions remain fail-closed.

## Durable normal stage performance records

Each Campaign slot may contain versioned `NormalStagePerformanceRecords`. A record stores only a canonical `StageId` and that slot's best (lowest) combined Push+Flip use count for a normal Objective clear. It contains no achievement ID, Steam name, AppID, or publication status. Duplicate records normalize to the lowest count and deterministic Stage order. New Game starts empty; retry and death reset only the in-memory attempt tracker, while a later successful clear updates the durable best without replacing a better historical value.

The attempt tracker consumes canonical player-action presentation signals by `(player entity, active action sequence)`. Only executed, non-cancelled Push or Flip signals resolved as `Success` or `Impact` count. Blocked, cancelled, non-executed, fake-attempt, other-action, and other-entity signals do not count. Player death or respawn resets the attempt, and a recreated retry scene starts with a fresh tracker. The threshold is inclusive: a normal `stage-1-2` Objective clear at 25 combined uses qualifies; 26 does not.

`CampaignGameplayFlowController` persists a stage performance record only for the same normal accepted Victory boundary used for achievement eligibility: a real `TickResult`, `ObjectiveResult.ClearedThisTick`, matching Stage/final tick, serialized sequence membership, and no Editor DirectPlay mode. Force Clear, every DirectPlay mode, rejected or same-tick-losing Victory, and defeat write no record. The committed record is loaded before stage-achievement earning, so a failed Campaign save cannot create an achievement detached from its recovery fact.

## Production application composition

The canonical `ApplicationPersistentDataSavePathProvider.SaveRootPath` owns both `profile.json` and `achievements.json` under the product-global `Saves` directory. Product achievement composition does not read slot roots or Editor DirectPlay temporary namespaces.

`ProductAchievementRuntimeBootstrap` creates one plain-C# `ProductAchievementApplicationLifetimeOwner` before the first scene, initializes one `ProductAchievementApplicationHost`, and disposes it at application quit. Scene transitions and Stage retries do not recreate it. The coordinator references a store-neutral `SwitchableAchievementPublicationSink`, whose initial target is `UnavailableAchievementPublicationSink`; missing-file initialization keeps an in-memory empty document without eagerly creating `achievements.json`.

The application composition has no shared constructor root with the stage-backed scene composition. A single achievement-specific internal `ProductAchievementEarningSinkHandoff` therefore carries only `IProductAchievementEarningSink` across that boundary. The application owner registers one exact sink, a different second registration fails closed, subsystem registration and application disposal clear it, and only scene composition consumes it. Gameplay runtime logic receives plain `INormalCampaignCompletionAchievementIntegration` and `ICampaignStageAchievementIntegration` constructor dependencies and never performs a static lookup or owns/disposes the application sink.

## Receipt-based earning and recovery

`NormalCampaignCompletionAchievementIntegration` is the only mapping owner from `NormalCampaignCompletionFact` to `GameAchievementIds.NormalCampaignComplete`. For an immediate earn it revalidates the fact's Stage against the injected serialized resolver and requires the post-save committed slot to have `CampaignCompleted`, receipt presence, a non-null receipt for the same Stage, and a receipt that passes persisted validation. The Campaign slot update completes first; only then is the committed slot loaded and the product sink called. An existing valid receipt is eligible after a new normal final objective completion, while invalid and present-null receipts remain fail-closed and are not repaired.

`NormalCampaignCompletionAchievementStartupReconciler` runs once per normal, non-batch application session after the product host is initialized and the first scene is loaded. Batch test/capture processes and every Editor DirectPlay mode are excluded before resolver or profile access. Composition discovers exactly one `ICampaignStageSequenceResolverProvider` under the active scene roots and obtains its resolver from the scene's serialized `CampaignStageSequenceDefinition`. Missing, ambiguous, or null providers warn once and make reconciliation a no-op; there is no global-scene scan, hard-coded final Stage, or code-created canonical fallback.

After resolver acquisition, reconciliation obtains the canonical production Campaign store through `CampaignSaveCompositionProvider`, whose factory completes profile migration before the read, and uses `LoadAllWithReport` rather than reading `profile.json` or backup files. Missing/empty profiles are a no-op; blocked or failed load states earn nothing. A backup-recovered canonical result is eligible. Version-1 and version-2 receipts must still name a serialized-sequence final Stage. Multiple valid slot receipts collapse to one product-ledger earn, and bare `CampaignCompleted` never earns.

The same one-shot startup profile scan replays eligible normal stage performance records for all slots. `campaign.stage-1-2.clear` requires any persisted normal `stage-1-2` clear record; `campaign.stage-1-2.push-flip-within-25` additionally requires a best combined count no greater than 25. Product-ledger idempotence collapses records across slots. Every `EditorDirectPlayMode` skips startup profile reading and immediate earning, including `CampaignProductionSlot`. Product persistence failures, unavailable state, invalid configuration results, and unexpected earning exceptions are contained after the Campaign save; they do not roll back Campaign completion or block terminal/GameClear flow. Durable receipts and performance records remain the next normal startup's recovery sources.

## Expected Steam mapping and publication session

The optional Steam Product Achievement integration owns these canonical expected mappings:

- `campaign.complete` → `VQ_CAMPAIGN_COMPLETE`
- `campaign.stage-1-2.clear` → `VQ_STAGE_1_2_CLEAR`
- `campaign.stage-1-2.push-flip-within-25` → `VQ_STAGE_1_2_PUSH_FLIP_LE_25`

Their status is `EXPECTED_NOT_PUBLISHED`: the repository requires those exact ordinal API Names, but no actual Steamworks App Admin achievement or published schema is configured or verified by this milestone. The Product Achievement Domain, Gameplay, Campaign records, ledger, and save schema do not know the Steam API Names.

After the canonical Steam runtime has initialized with a nonzero observed AppID, valid SteamID, logged-on state, and one active achievement callback pair, a strongly typed achievement-only handoff attaches `SteamAchievementPublisher` to the switchable sink. Product-first and Steam-first startup orders converge on the same attach. The same Steam runtime session is idempotent; detach followed by a new Steam runtime session opens one new reconciliation opportunity. Scene reload, Main Menu entry, and Steam ticks do not create publication sessions.

Each new attached publication session reconciles every catalog-known earned product ID once, whether or not it is currently pending. Existing per-ID in-flight protection remains active, and there is no per-frame retry. Accepted and already-satisfied results remove pending through the existing atomic repository save; unavailable, deferred, rejected, failed, and shutdown completion leave pending durable.

The Steam publisher executes one mutation at a time and queues concurrent product publications in FIFO order for the attached session. Each item validates exact runtime schema presence before mutation, performs a pre-read, calls Set then Store once, accepts either callback order only after matching AppID plus successful stats and exact full-unlock achievement callbacks, and performs an unlocked post-read. Completion reserves the next queued item before invoking observers, preventing reentrant publication from overtaking the queue. Disposal completes both the active and queued items as unavailable. A 30-second J2M monotonic callback timeout or non-OK stats-store callback quarantines the attached publisher session: the active item fails, queued items become unavailable immediately, callback handles are disposed, and the same process does not auto-register a replacement session. Durable pending IDs remain for the next attached Steam publication session. Multi-achievement StoreStats batching remains deferred.

`SteamPlatformRuntime.Tick` remains the sole timeout tick owner after its canonical callback pump. The publisher never initializes, pumps, or shuts down Steam. Publisher callbacks are disposed and the product session is detached before overlay callback disposal and native shutdown. `-j2mSteamAchievementSmoke` exclusively selects the Spacewar smoke callback owner and prevents Product publisher creation; base `-j2mSteamSmoke` alone does not own achievement callbacks and does not exclude Product publication.

## Deferred boundaries

- Actual AppID schema configuration, Steamworks App Admin publication, real account unlock validation, and player builds are deferred.
- Achievement UI, localized presentation, toast behavior, Cloud allowlisting, and multi-device conflict policy are deferred.

`M7B2GB0_DURABLE_NORMAL_COMPLETION_RECEIPT_READY`

`M7B2GB_RECEIPT_BASED_CAMPAIGN_COMPLETE_EARNING_READY`
