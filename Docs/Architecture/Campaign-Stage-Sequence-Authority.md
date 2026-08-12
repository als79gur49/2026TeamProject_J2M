# Campaign Stage Sequence Authority

## Current authority

Campaign sequence의 유일한 production authoring source는 다음 ScriptableObject asset이다.

`Assets/_Features/Stages/Content/Campaigns/campaign-main/Catalog/CampaignMain_StageSequence.asset`

- canonical GUID: `bdeaa9a608b8dde4b6d0192853f857b0`
- Production CI와 prebuild는 이 path/GUID에서 `CampaignStageSequenceDefinition`을 typed load한다.
- sequence의 entry 수, StageId, 순서, level group은 이 asset이 소유한다.
- 캠페인의 exact order 승인은 validator에 복제한 golden list가 아니라 asset YAML diff review로 수행한다.
- 현재 entry count는 진단 값일 뿐 고정 계약이 아니다. 특히 `13`을 validation constant로 사용하지 않는다.

`CampaignStageSequenceDefinition`은 serialized entry container다. 각 entry는 canonical Stage ID와 Level Group ID만 소유하며, code canonical arrays/default initializer/runtime factory는 존재하지 않는다.

## Phase 2 runtime authority

Production runtime은 더 이상 transitional code mirror/factory를 읽지 않는다. 각 scene composition root가 serialized authoritative asset에서 resolver를 한 번 생성하고 같은 immutable instance를 하위 consumer에 전달한다.

```text
MainMenuUiFlowInstaller
  -> CampaignStageSequenceResolver (one immutable snapshot)
     -> MainMenuController
     -> SaveSlotValidationService
     -> standalone seed import
     -> ICampaignStageSequenceResolverProvider
        -> Product Achievement startup reconciliation

StageBackedGameplaySceneInstallerBase
  -> CampaignStageSequenceResolver (one immutable snapshot)
     -> GameplaySceneHostConfiguration
        -> GameplayHostPresentationFeed
           -> GameplayHostStageCompletionRuntime
              -> MinimalStageCompletionReadModelBuilder
        -> GameplayHostUiAccessContext
           -> GameplayUiFlowInstaller
              -> UIFlowCoordinator
     -> CampaignGameplayFlowController
        -> StageRetryChanceTracker
     -> CampaignPauseProgressionReadSource
     -> DemoStageControl campaign bridge
```

Resolver construction copies only canonical `StageId` and `LevelGroupId` values into readonly value snapshots and lookup maps. It retains neither the source ScriptableObject nor source entry/collection references.

Missing serialized sequence references are bootstrap configuration failures in both Main Menu and stage-backed gameplay composition. Achievement startup discovers exactly one provider from active-scene roots and fails closed with a warning when it is missing, ambiguous, or cannot produce a resolver. Player Production has no asset-null-to-code-default fallback or static sequence resolver.

Stage clear save advancement, retry/group routing, completion read-model Next, and `UIFlowCoordinator` final/Game Clear routing therefore use the same gameplay-composition resolver instance. The unused result-navigation static store was removed.

## Authoritative asset validation

장기 유지되는 authoritative validation은 code mirror와 독립적으로 다음을 검사한다.

- canonical path/GUID의 main asset이 존재하고 예상 ScriptableObject 타입으로 typed load되는가
- serialized entries collection이 존재하고 비어 있지 않은가
- 각 StageId가 non-empty canonical typed ID이고 중복되지 않는가
- production alias table의 deprecated source ID가 sequence에 사용되지 않았는가
- 각 sequence StageId가 production `StageCatalog` entry 정확히 하나로 해석되는가
- stage loading에 필요한 catalog entry의 gameplay definition identity가 완전한가
- level group이 non-empty canonical ID이고 동일 group이 sequence에서 연속 구간을 이루는가

level group은 UI 표시용 catalog grouping에서 추론하는 값이 아니다. retry 시 해당 group의 first stage를 찾고, checkpoint/progression 및 group 경계 chance restoration을 결정하는 runtime 의미다.

Sequence → Catalog 무결성과 Catalog → Sequence eligibility coverage는 모두 blocking error다. 각 production `StageContentEntry`는 `Campaign` 또는 `CatalogOnly`를 명시하며, `CatalogOnly`는 실제 repository에 필요한 exclusion reason을 함께 소유한다. Campaign entry 누락, CatalogOnly entry 포함, unset eligibility는 모두 CI/prebuild를 실패시킨다. sequence membership은 계속 sequence asset만 소유하며 catalog grouping/ordering, path/name, 별도 StageId allowlist로 membership을 추론하지 않는다.

`StageContentEntry`의 current catalog 역할은 StageId identity, loading/presentation/audio/authoring companion references, Campaign eligibility/exclusion reason, Demo initial availability다. Migration-era raw grouping/ordering fields와 launch-summary query layer는 current schema/runtime contract가 아니다.

## CI, prebuild, and Player inclusion

`StageCatalogCiValidationEntryPoint`는 production catalog validation과 같은 failure/exit 경로에서 `Campaign Sequence — Authoritative Asset Contract`를 실행한다. Error가 발생하면 catalog CI result는 실패한다. report는 source path/GUID, typed-load 상태, entry count, structural/StageId/catalog/level-group/alias 결과, eligibility coverage, final error/warning count를 기록한다.

`StageCatalogBuildValidationHook`은 동일 loader와 authoritative validator를 prebuild에서 재사용한다. 또한 enabled build scenes의 실제 component serialized field를 read-only로 검사하여 다음 scene이 정확히 authoritative asset을 참조하는지 확인한다.

- `Assets/Scenes/MainMenuScene.unity` — `MainMenuUiFlowInstaller._campaignStageSequenceDefinition`
- `Assets/Scenes/UIAudioScene.unity` — `StageBackedGameplaySceneInstallerBase.campaignStageSequenceDefinition`

null 또는 다른 sequence asset reference는 blocking error다. validator는 scene을 additive/read-only로 열고, 이미 열린 scene과 active scene 상태를 보존하며 저장하지 않는다. Player inclusion은 Resources/Addressables가 아니라 이 enabled scene serialized dependency 계약을 사용한다.

## Phase 4 Player verification

Phase 4는 2026-08-09에 current dirty Phase 1~3 worktree를 대상으로 완료했다. 공식 release wrapper는 clean detached commit만 허용하므로, wrapper가 호출하는 동일한 canonical entrypoint를 현재 worktree에서 직접 실행했다.

- Unity: `6000.3.11f1` (`3000ef702840`)
- target: `StandaloneWindows64`, x86_64, Mono
- configuration: `Windows-x64-Store-Mono-LogOn`, non-development, `BuildOptions.None`
- entrypoint: `WindowsReleaseBuildCli.BuildWindowsX64NonDevelopment`
- enabled scenes: `MainMenuScene`, then `UIAudioScene`
- result: `Succeeded`, errors `0`, warnings `1`, zero-error gate passed; `391211569` bytes in `33.3894849` seconds
- output: `/mnt/d/J2M/builds/p0-phase4/p0-phase4-final-20260809T132507Z/.staging-direct-windows`
- private evidence: `/mnt/d/J2M/evidence/p0-phase4/p0-phase4-final-20260809T132507Z/private`

The build log's packed-asset list includes the authoritative sequence asset, Stage Catalog provider, alias table, all campaign Stage Content entries, and the catalog-only legacy entry. Both enabled scenes serialize the sequence GUID `bdeaa9a608b8dde4b6d0192853f857b0`; no Resources/Addressables sequence lookup was introduced.

`StageCatalogCiValidationEntryPoint.RunFromCommandLine` passed independently with `EntryCount=13`, `AuthoritativeAssetContract=Passed`, `CampaignEligibilityCoverage=Passed`, authoritative errors `0`, and authoritative warnings `0`. The entry count is evidence for this revision, not a hard-coded product contract.

Actual executable smoke used the existing Development Mono Player harness with an isolated product/save namespace. Expected New Game and Continue Stage IDs were parsed from the authoritative asset before launch rather than duplicated as code constants. The standard matrix produced 27/27 PASS runtime logs and no failure marker.

| Player flow | Evidence |
| --- | --- |
| New Game | Resolver first `stage-0-1` -> intro cinematic production route -> gameplay `stage-0-1`; save cursor/group `stage-0-1` / `level-0`; Main Menu and gameplay resolver contracts passed. |
| Continue | Seeded `stage-0-2` -> Continue -> gameplay/save `stage-0-2`; group `level-0`; non-empty localization key. |
| Clear -> Next | Source `stage-4-1`; resolver next, saved cursor, and result Next all `stage-4-2`. |
| Retry/group | Source `stage-2-2`; resolver group `level-2`; group-first, saved cursor, and restart destination all `stage-2-1`. |
| Final -> Game Clear | Final `stage-4-3`; no result Next; saved cursor `stage-4-3`; `CampaignCompleted=true`; Game Clear -> Main Menu route passed. |

The same Player assertions reject `legacy-stage-5-1` sequence membership and require a non-empty `StagePresentationDefinition` localization key. Release `ScriptingAssemblies.json` contains no Editor/Test assembly, and the compiled Stage/UI composition/gameplay-host runtime assemblies contain no `UnityEditor` or `AssetDatabase` reference.

Release regression evidence on the final Phase 4 source includes:

- physical SSOT architecture: 1 passed / 0 failed
- authoritative sequence validator: 9 passed / 0 failed
- save/direct-play and retired-save migration: 32 passed / 0 failed
- Stage Catalog CI entrypoint: passed
- UI: Windows build passed; Unity EditMode 1363 passed / 0 failed / 0 skipped
- Core: EditMode 197 passed / 0 failed; PlayMode 103 passed / 4 documented skipped / 0 failed
- Climate SDF final SHA-256: `66193afe72fe9e2c4de11596ed68c1eb038fffb7f0f71b8605669f68922c459f`

The project-wide full lane was not run or claimed. A broader `Campaign`-substring diagnostic filter was run and reported six failures outside the Phase 4 touched gate; it is not used as the targeted closeout gate or as project-wide evidence.

## Display name boundary

Sequence entry에는 display name field가 없다. Presentation 권위는 `StagePresentationDefinition` 및 localization table/key 계층에 있다.

- display name을 authoritative asset required-field로 승격하지 않는다.
- presentation/localization과 새 삼자 equality 계약을 만들지 않는다.
- sequence schema에 UI text를 다시 추가하지 않는다.

## Phase boundaries

- Phase 1: completed — actual production asset validation, catalog CI/prebuild integration, exact scene reference validation, content smoke
- Phase 2: implemented — runtime provider/resolver instance 통합, immutable snapshot, static `Lazy<CampaignStageSequenceResolver>` 제거, silent runtime fallback 제거
- Phase 3: implemented — code mirror/factory 제거, sequence display-name 제거, CatalogOnly/eligibility metadata 및 exclusion reason 도입, Catalog → Sequence reverse coverage blocking
- Phase 4: completed — canonical Player build/dependency evidence, actual executable Campaign flow smoke, regression and serialization closeout

Phase 3는 save schema와 Stage ID cursor, completed-slot migration, StageId rename/removal migration, direct-play catalog membership을 변경하지 않는다. Retired `stage-5-1` source ID는 save compatibility policy가 소유하고 destination final stage/group은 injected authoritative resolver에서 파생한다.

## Final authority matrix

| Concept | Final authority | Player verified |
| --- | --- | --- |
| Membership/order | `CampaignMain_StageSequence.asset` | Yes |
| Eligibility | `StageContentEntry.CampaignParticipation` | Yes, prebuild/CI reverse coverage |
| Identity/loading | Stage Catalog | Yes |
| Display | `StagePresentationDefinition` + localization | Yes |
| Level group | Campaign sequence entry | Yes |
| Alias | `CampaignMain_StageIdAliasTable.asset` | Yes, prebuild/CI and packed dependency |
| Legacy save | `RetiredCampaignSaveCompatibilityPolicy` | Yes, compiled Player contract plus targeted migration regression |
| Player inclusion | Enabled-scene serialized dependency | Yes, packed asset list plus runtime bootstrap |

## Documentation reconciliation

- `CampaignMainStageContentMigrationReport.md` is a historical snapshot and is labeled as such; its 10-catalog/9-sequence counts are not current authority.
- `Stage-Content-P3-Sunset-2026-04-22.md` already identifies itself as bounded historical close evidence. Its alias/known-warning ledger statements remain historical evidence, not the current Campaign Sequence authority.
- Editor direct-play documents continue to describe their own two-entry launcher catalog. Direct-play catalog coverage is not Campaign membership authority and was not expanded for Player smoke.
- ADR-006's TileFeature `PresentationKey` future work is a separate follow-up and was not implemented in Campaign Sequence Phase 4.

Campaign Sequence P0 closeout status:

```text
P0_PHASE4_PLAYER_RUNTIME_AND_RELEASE_CLOSEOUT_COMPLETE
CAMPAIGN_SEQUENCE_SSOT_FULLY_CLOSED
PROJECT_WIDE_FULL_LANE_NOT_CLAIMED
```
