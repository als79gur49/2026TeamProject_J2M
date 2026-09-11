# Product Achievement Foundation

## Ownership and identity

Product achievements are VectorQuake product-domain facts. They are separate from store-specific achievement transport and configuration. The canonical product IDs are `campaign.level-0.clear` through `campaign.level-4.clear`; none is a Steam API Name.

The code-defined `GameAchievementCatalog` currently contains exactly five one-shot definitions. The catalog intentionally has no localized copy, icon, AppID, provider metadata, or store mapping. Level-final stage eligibility is owned by the Campaign integration rather than embedded in the product ledger.

## Product-global durable state

The product-global document is `Saves/achievements.json`, schema version 1:

```text
SchemaVersion
EarnedAchievementIds[]
PendingAchievementPublicationIds[]
```

`EarnedAchievementIds` records durable product truth. `PendingAchievementPublicationIds` is the external-publication outbox and fresh-application confirmation set, and pending is always a subset of earned. All achievements derived from one committed gameplay fact are added to both sets in one atomic document write before any publisher call. A same-application `Submitted` result keeps pending durable; only a fresh-application pre-read that returns `AlreadySatisfied` removes pending values, with every satisfied item from that publication batch removed in one later atomic write.

The document has no save-slot identity. Delete Slot, New Game, campaign-slot clearing, active-slot changes, and profile migration do not reset product achievements. Unknown but syntactically valid product IDs are preserved for forward compatibility and are excluded from publication until the catalog knows them. This includes the three inactive pre-release IDs (`campaign.complete`, `campaign.stage-1-2.clear`, and `campaign.stage-1-2.push-flip-within-25`): neither earned nor pending values are automatically deleted, startup performs no cleanup write for them, direct earn requests are rejected, and no replacement achievement is granted from an old ID. A valid backup is restored before a missing primary is treated as a new document, closing the atomic fallback interruption window. Corrupt, excessively nested, schema-invalid, and unsupported documents fail closed; after quarantine, the artifact keeps later loads fail-closed until explicit repair, so they are not silently replaced with an empty ledger.

## Dependency and publication boundaries

`Game.Product.Achievements.Domain` has no Stage, Gameplay, UI, Platform, or store transport reference. `FileProductAchievementRepository` depends on the product-owned `IAchievementTextStore`. The composition-only `StageAtomicAchievementTextStoreAdapter` wraps the existing atomic file implementation without moving or reversing the Stage save module.

`IAchievementPublicationSink` is store-neutral and accepts an immutable achievement batch with an immutable per-item result. `Submitted` means the current application sent and observed the store-specific exact-name completion path but does not clear the durable pending confirmation; `AlreadySatisfied` means the attached publisher observed the achievement satisfied before mutation and is the only result that clears pending. The coordinator registers every ID in one batch attempt before calling `PublishBatch`, ignores unknown, duplicate, or late-after-disposal result items, contains publisher exceptions, and never publishes before the earned/outbox transition is durable. Initialization reconciles every catalog-known earned ID once per coordinator session in at most one batch, not only pending IDs; no per-frame retry loop exists.

DirectWindows composition uses `UnavailableAchievementPublicationSink`. Earning remains active and pending remains durable while an external publisher is unavailable.

## Durable normal Campaign completion receipt

`CampaignSlotDocument.NormalCampaignCompletionReceipt` is a versioned slot fact, not an achievement flag. The current writer emits version 2, whose only semantic field is `CompletedStageId`. The serialized `StageRunId` and `ClearSource` fields remain in the physical shape solely so version-1 saves can be read losslessly; version 2 writes explicit legacy-absence values and does not interpret them as provenance. The receipt contains no product achievement ID, Steam API Name, AppID, publisher, or publication state.

Version 1 remains readable inside the current profile schema under its original receipt contract: canonical `CompletedStageId`, a nonblank legacy `StageRunId`, and the legacy Objective clear-source value. Unknown receipt versions, present-null receipts, invalid receipts, and a completed Campaign with no receipt fail closed. They are not repaired, inferred, or automatically rewritten to version 2. This dual-read/new-write receipt policy does not change `CampaignProfileDocument.SchemaVersion = 2`; no PlayerPrefs campaign root is part of the production contract.

`CampaignGameplayFlowController` owns normal-completion eligibility. It obtains a validated nullable `StageId` for the completion receipt only after an accepted Victory terminal claim with a non-null `TickResult`, `ObjectiveResult.ClearedThisTick`, matching Stage and final tick, `EditorDirectPlayContext.Mode == None`, and serialized `CampaignStageSequenceResolver` membership/finality. Non-final stages, Force Clear, NonCampaign DirectPlay, CampaignTempSlot DirectPlay, and CampaignProductionSlot DirectPlay create no fact or receipt. Same-tick death remains higher priority than clear and therefore creates no completion fact.

`CampaignCompleted = true` and the first eligible receipt are assigned inside the same Campaign slot mutation and durable save invocation. A save failure leaves neither change durable. A valid existing receipt is preserved without overwrite, and an invalid existing receipt is retained but is not eligible and is not silently repaired. New Game starts with no receipt, Delete Slot removes that slot fact, and neither operation changes the product-global achievement document. Current schema-2 profiles that omit the optional field load with a null receipt. Profile schema 1 and unsupported forward versions remain fail-closed.

## Durable normal stage performance records

Each Campaign slot may contain versioned `NormalStagePerformanceRecords`. A record stores only a canonical `StageId` and that slot's best (lowest) combined Push+Flip use count for a normal Objective clear. It contains no achievement ID, Steam name, AppID, or publication status. Runtime upsert and in-memory normalization retain the lowest count for each canonical `StageId` and produce deterministic Stage order. A persisted current-schema profile containing duplicate `StageId` performance records is invalid and fails closed before normalization. New Game starts empty; retry and death reset only the in-memory attempt tracker, while a later successful clear updates the durable best without replacing a better historical value.

The attempt tracker consumes canonical player-action presentation signals by `(player entity, active action sequence)`. Only executed, non-cancelled Push or Flip signals resolved as `Success` or `Impact` count. Blocked, cancelled, non-executed, fake-attempt, other-action, and other-entity signals do not count. Player death or respawn resets the attempt, and a recreated retry scene starts with a fresh tracker. These metrics remain campaign performance data; achievement eligibility has no action-count threshold.

After the committed record is available, `CampaignStageAchievementIntegration` evaluates all saved normal stage records against the last entry of each `level-0` through `level-4` group in the injected serialized sequence. It submits all matching IDs through one `EarnBatch` call. Stage names, numeric suffixes, total campaign finality, and Push/Flip counts do not determine level-final eligibility. The scan uses the last occurrence of each group even if a caller bypasses authoring validation with a noncontiguous group.

`CampaignGameplayFlowController` persists a stage performance record only for the same normal accepted Victory boundary used for achievement eligibility: a real `TickResult`, `ObjectiveResult.ClearedThisTick`, matching Stage/final tick, serialized sequence membership, and no Editor DirectPlay mode. Force Clear, every DirectPlay mode, rejected or same-tick-losing Victory, and defeat write no record. The committed record is loaded before stage-achievement earning, so a failed Campaign save cannot create an achievement detached from its recovery fact.

## Production application composition

The canonical `ApplicationPersistentDataSavePathProvider.SaveRootPath` owns both `profile.json` and `achievements.json` under the product-global `Saves` directory. Product achievement composition does not read slot roots or Editor DirectPlay temporary namespaces.

`ProductAchievementRuntimeBootstrap` creates one plain-C# `ProductAchievementApplicationLifetimeOwner` before the first scene, initializes one `ProductAchievementApplicationHost`, and disposes it at application quit. Scene transitions and Stage retries do not recreate it. The coordinator references a store-neutral `SwitchableAchievementPublicationSink`, whose initial target is `UnavailableAchievementPublicationSink`; missing-file initialization keeps an in-memory empty document without eagerly creating `achievements.json`.

The application composition has no shared constructor root with the stage-backed scene composition. A single achievement-specific internal `ProductAchievementEarningSinkHandoff` therefore carries only `IProductAchievementEarningSink` across that boundary. The application owner registers one exact sink, a different second registration fails closed, subsystem registration and application disposal clear it, and only scene composition consumes it. Gameplay runtime logic receives a plain `ICampaignStageAchievementIntegration` constructor dependency and never performs a static lookup or owns/disposes the application sink.

## Level-clear earning and recovery

`CampaignStageAchievementIntegration` is the only Campaign-to-product mapping owner. The normal clear record must be committed before earning. The current authored endings are Level0 `stage-0-3`, Level1 `stage-1-2`, Level2 `stage-2-2`, Level3 `stage-3-3`, and Level4 `stage-4-3`. Every level achievement requires only its last stage's normal clear record; other stages need not be present. `CampaignCompleted`, the progress cursor, and completion receipts alone never grant achievements. The retired receipt achievement integration and all grandfather conversion paths are absent.

`CampaignStageAchievementStartupReconciler` runs once per normal, non-batch application session after product initialization and the first scene load. DirectPlay modes are excluded before resolver or profile access. Composition obtains exactly one `ICampaignStageSequenceResolverProvider` under the active scene roots. Missing or ambiguous providers and unusable product/profile state make reconciliation a no-op; there is no same-session polling retry. The canonical `CampaignSaveCompositionProvider` query uses `LoadAllWithReport`; only Loaded or BackupRecovered profiles are evaluated, across every nonempty slot. Product-ledger idempotence deduplicates clears across slots and repeated launches.

Immediate earning and startup replay use the same current-sequence rule. Appending/reordering content can change eligibility of unawarded historical records; earned level achievements remain earned. The current save schema has no historical sequence snapshot. Campaign receipt persistence remains a save contract, but receipts are not an achievement recovery source.

Product persistence failures and unexpected earning exceptions remain contained after the Campaign save and do not block terminal/GameClear flow. Normal stage performance records allow another evaluation on a later eligible normal launch.

## Expected Steam mapping and publication session

The optional Steam Product Achievement integration owns these canonical expected mappings:

- `campaign.level-0.clear` → `VQ_LEVEL_0_CLEAR`
- `campaign.level-1.clear` → `VQ_LEVEL_1_CLEAR`
- `campaign.level-2.clear` → `VQ_LEVEL_2_CLEAR`
- `campaign.level-3.clear` → `VQ_LEVEL_3_CLEAR`
- `campaign.level-4.clear` → `VQ_LEVEL_4_CLEAR`

Their status is `EXPECTED_NOT_PUBLISHED`: the repository requires those exact ordinal API Names, but no actual Steamworks App Admin achievement or published schema is configured or verified by this milestone. The Product Achievement Domain, Gameplay, Campaign records, ledger, and save schema do not know the Steam API Names.

After the canonical Steam runtime has initialized with a nonzero observed AppID, valid SteamID, logged-on state, and one active achievement callback pair, a strongly typed achievement-only handoff attaches `SteamAchievementPublisher` to the switchable sink. Product-first and Steam-first startup orders converge on the same attach. The same Steam runtime session is idempotent, but one Product Achievement application lifetime consumes at most one distinct Steam publication session. Detach does not permit another distinct Steam publisher to confirm state in the same application lifetime; a new application lifetime is required. Scene reload, Main Menu entry, Stage retry, and Steam ticks do not create confirmation sessions.

The first attached publication session reconciles every catalog-known earned product ID once in one batch, whether or not it is currently pending. Existing per-ID in-flight protection remains active, and there is no per-frame retry. `Submitted`, unavailable, deferred, rejected, failed, and shutdown completion leave pending durable. On the next application lifetime, `AlreadySatisfied` pre-reads remove all matching pending IDs through one atomic repository save; locked pre-reads submit again and keep pending for later confirmation.

The Steam publisher executes one batch at a time and queues concurrent batches in FIFO order for the attached session. Each item validates exact runtime schema presence and performs a pre-read; every locked valid candidate is set before the batch makes exactly one `StoreStats` call. An item becomes `Submitted` only from its exact AppID/API Name full-unlock `UserAchievementStored_t`. `UserStatsStored_t`, whether OK or non-OK, has no achievement identifier and remains registered as part of the atomic callback pair, but its receiver records no diagnostics and has no side effects; it never completes, fails, or quarantines the current batch. Completion reserves the next queued batch before invoking observers, preventing reentrant publication from overtaking the queue.

`StoreStats(false)` fails only that batch's store candidates and continues with the next queued batch because no ambiguous callback wait was entered. After `StoreStats(true)`, the batch waits for every expected named callback. A 30-second J2M monotonic timeout preserves already observed `Submitted` items, fails unresolved items, quarantines the attached publisher session, completes queued batches as unavailable, and disposes callback handles. Every batch performs its runtime readiness check once at the mutation-admission boundary. Readiness loss completes the active batch as unavailable, while an exception from `GetAppId`, `IsSteamIdValid`, or `IsLoggedOn` completes it as failed; both paths quarantine the session before any Achievement API call. An exception from the Achievement schema, pre-read, set, or store API quarantines at its point of failure, preserves exact item results already known, fails unresolved items, and makes queued batches unavailable. Disposal overwrites every active and queued item as unavailable, including items whose partial batch result was already known. Durable pending IDs remain for the next application lifetime, and the same process cannot attach a replacement Product Achievement publisher.

`SteamPlatformRuntime.Tick` remains the sole timeout tick owner after its canonical callback pump. The publisher never initializes, pumps, or shuts down Steam. If the callback pump faults, the runtime first becomes unavailable, then detaches Product publication and disposes its publisher without shutting down native Steam; active and queued items complete uniformly as unavailable, coordinator in-flight state clears, and durable pending remains. Final shutdown reuses the same idempotent publication-stop path, disposing the achievement callback pair before native shutdown even when callback cleanup throws. Steam smoke execution and its product-start blocking branch are retired. The existing single-application-lifetime publication session limit remains; participant reset and automatic relaunch are separate unimplemented plans.

## Deferred boundaries

- Actual AppID schema configuration, Steamworks App Admin publication, real account unlock validation, and player builds are deferred.
- Achievement UI, localized presentation, toast behavior, Cloud allowlisting, and multi-device conflict policy are deferred.

`M7B2GB0_DURABLE_NORMAL_COMPLETION_RECEIPT_READY`

The earlier receipt-based campaign achievement milestone is retired by the unreleased five-level achievement replacement.

### Exhibition startup and participant reset

Normal Windows participant reset is composed without an experiment flag. Product
startup and campaign access are held while a Pending reset is resumed or a completed
reset return is validated. Steam native initialization remains active so the existing
host can pump the maintenance callback pair. Publication starts only after the lease
is released successfully. The regular publisher's StatsStored receiver remains inert;
the exhibition reset protocol separately requires StatsStored OK and post-read false.
The earned/pending document, its recovery companions, and campaign progress are reset
by their owning storage APIs. A Ready journal and a validated completed-return request
control service resumption; a helper log or Overlay report never controls it.
