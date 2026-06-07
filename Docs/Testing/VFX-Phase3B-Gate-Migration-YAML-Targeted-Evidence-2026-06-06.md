# VFX Phase 3B-Gate Migration YAML + Targeted Evidence 정리 보고서

## 1. 결론
- migration YAML residue 상태: current shell evidence인 `UIAudioScene`과 historical legacy scene evidence인 `CombinedGameplayShowcase`, `TutorialScene`에 `EnableGameplayVfx*Migration` scene YAML residue가 15개씩, 총 45개 남아 있다.
- field deletion 가능 여부: 이번 historical gate 작업에서는 삭제하지 않았다. `GameplayVfxProductionRuntime`의 serialized field/property 선언이 남아 있는 동안 scene YAML 단독 삭제는 stable cleanup gate가 아니므로, Phase 3B field/property deletion과 current retained shell reserialization을 같은 cleanup package로 처리해야 한다.
- test filter 정책: `core --filter X`는 lane-preserving으로 확정했다. broad `core` lane의 assembly/category/gate scope를 유지한 상태에서 `X`만 매치한다.
- PlayerMovementPlayModeTests 상태: `core --filter PlayerMovementPlayModeTests`는 core subset 13개 pass. `full --filter PlayerMovementPlayModeTests`는 fixture-wide 42개 중 15개 fail.
- Phase 3B 진행 가능 여부: field/property deletion 자체는 아직 보류. 다음 Phase 3B cleanup commit에서 정확한 삭제 대상과 scene reserialize 대상은 확정됐다.
- 보류: `EnableGameplayVfx*Migration` field/property declaration deletion, migration YAML removal, legacy naming consolidation, historical raw scene deletion review.
- 남은 위험: fixture-wide PlayerMovement red는 topology/player-movement targeted risk로 별도 추적해야 하며 broad core/ui green과 혼동하면 안 된다.

## 2. Baseline
| 명령 | 결과 | 비고 |
| --- | --- | --- |
| `git status --short --branch` | pass | 시작 시 clean, branch ahead 22 |
| `git diff --stat` | pass | 시작 시 empty |
| `git diff --name-status` | pass | 시작 시 empty |
| `./run_tests.sh core` | pass | core EditMode 160, core feature gate EditMode 52, core PlayMode 34 |
| `./run_tests.sh ui` | pass | ui EditMode 651 |

## 3. Test Runner Filter Policy
| 항목 | 기존 동작 | 새 정책 | 검증 |
| --- | --- | --- | --- |
| `--filter` 전달 | Unity bootstrap에 전달되지만 core filter가 broad core보다 넓은 fixture를 잡을 수 있었다. | `--filter`와 `--test-filter`는 모두 `-codexTestFilter`로 전달한다. | `./run_tests.sh --dry-run core --filter GameplayVfxFlagRolloutPolicyTests`, `./run_tests.sh --dry-run core --filter PlayerMovementPlayModeTests` |
| 0-test match | filter stage별 0개와 전체 0개 구분이 불명확했다. | filtered stage별 0개는 허용하고, lane aggregate match가 0이면 fail-fast한다. | VFX PlayMode targeted에서 EditMode stages 0개 후 PlayMode match pass |
| lane scope | `core --filter X`가 full fixture evidence처럼 보일 수 있었다. | lane-preserving. `core` scope의 assembly/category/gate를 풀지 않는다. | `core --filter PlayerMovementPlayModeTests`가 core PlayMode subset 13개만 실행 |
| PlayerMovement fixture | core targeted와 fixture-wide targeted evidence가 혼동됐다. | core subset은 `core --filter`, fixture-wide PlayMode evidence는 `full --filter`로 분리한다. | `core --filter PlayerMovementPlayModeTests` pass, `full --filter PlayerMovementPlayModeTests` red |

## 4. PlayerMovementPlayModeTests 조사
| 테스트/명령 | 결과 | 실패 수 | 대표 실패 | 분류 | Phase 3B blocker 여부 | 조치 |
| --- | --- | --- | --- | --- | --- | --- |
| `./run_tests.sh core --filter PlayerMovementPlayModeTests` | pass | 0/13 | 없음 | lane-preserving core subset green | no | broad core claim에 포함 가능 |
| `./run_tests.sh full --filter PlayerMovementPlayModeTests` | fail | 15/42 | `TopologyTransitionCameraShake_PlayMode_DirectAndCinemachinePaths_SharePulseTimingAndReset` tolerance failure, `GameplayInputHost_*` presentation/input timing failures | fixture-wide PlayMode red / unrelated topology-player-movement targeted risk | no, unless Phase 3B touches topology/player-movement cluster | 외부 검토 보고서와 follow-up issue에서 별도 추적 |

## 5. Migration YAML Residue Inventory
| Evidence context | Scene | Field count | Fields | Runtime read 여부 | Cleanup 가능 여부 |
| --- | --- | ---: | --- | --- | --- |
| current shell evidence | `Assets/Scenes/UIAudioScene.unity` | 15 | all `enableGameplayVfx*Migration`, value `1` | canonical migrated cue path does not branch on these values | Phase 3B field deletion + current shell reserialize에서 cleanup |
| historical legacy scene evidence | `Assets/Scenes/CombinedGameplayShowcase.unity` | 15 | all `enableGameplayVfx*Migration`, value `1` | canonical migrated cue path does not branch on these values | historical raw scene deletion/rewrite review에서 cleanup |
| historical legacy scene evidence | `Assets/Scenes/TutorialScene.unity` | 15 | all `enableGameplayVfx*Migration`, value `0` | canonical migrated cue path does not branch on these values | historical raw scene deletion/rewrite review에서 cleanup |

All historical residue entries are on `Game.Feature.Gameplay.Vfx.Host.GameplayVfxProductionRuntime` (`m_Script` guid `77f98ca183bf441ba81f70f521126c17`) under GameObject fileID `1179627480`.

## 6. Cleanup 조치
| 파일 | 조치 | 이유 | reserialize 여부 | 검증 |
| --- | --- | --- | --- | --- |
| `run_tests.sh` | core lane에 `core-feature-gate` EditMode stage 추가 | lane-preserving filter 유지 상태에서 Phase 3B gate tests를 broad core evidence에 포함 | n/a | broad core pass |
| `Assets/_Tools/Editor/TestRunnerCliBootstrap.cs` | `core-feature-gate` selection과 `Phase3BGate` category 추가, filtered core scope 보존 | `core --filter X`가 fixture-wide로 확장되지 않게 함 | n/a | dry-run 및 targeted pass |
| VFX/Stage targeted tests | `Core` + `Phase3BGate` category 추가 | VFX/Stage gate tests를 broad core feature gate에 명시 편입 | n/a | broad core feature gate 52 pass |
| Scene YAML | 수정하지 않음 | field declaration이 남은 상태의 YAML 단독 삭제는 stable cleanup이 아님 | no | historical inventory 확정, scene bootstrap targeted pass |
| `Docs/Architecture/Gameplay-VFX-Governance.md` | Phase 3B-Gate residue와 PlayerMovement red 분리 정책 추가 | 외부 검토용 gate 판단 근거 기록 | n/a | docs diff reviewed |
| `Docs/Testing/Gameplay-Test-Automation-Guide.md` | filter/lane policy 문서화 | core targeted와 full fixture evidence 혼동 방지 | n/a | docs diff reviewed |

## 7. Phase 3B Field Deletion Gate
| 조건 | 결과 | 비고 |
| --- | --- | --- |
| runtime branch residue 0 | pass | migrated cue path is canonical default-only; compatibility fields still declared |
| prefab residue 0 | pass | migration residue search found no prefab YAML residue |
| scene residue 0 또는 cleanup 대상 확정 | pass with deferral | historical residue 45개 target 확정, deletion commit에서 current shell reserialize와 historical raw scene review 필요 |
| active raw scene path 0 | pass | `Assets ProjectSettings Packages` direct path search has no active raw scene path reference |
| targeted tests reliable | pass | lane-preserving targeted tests green |
| broad core/ui green | pass | broad `core` and `ui` passed on this revision |
| PlayerMovement red 분류 완료 | pass | core subset green, full fixture red separated |

## 8. Docs/Test Docs 변경
| 문서 | 변경 내용 |
| --- | --- |
| `Docs/Architecture/Gameplay-VFX-Governance.md` | Phase 3A/3B-Gate compatibility residue, exact historical scene residue inventory, field deletion + reserialize requirement, PlayerMovement full fixture red separation |
| `Docs/Testing/Gameplay-Test-Automation-Guide.md` | `--filter` / `--test-filter` alias, lane-preserving core filter, aggregate 0-test fail-fast, `full --filter PlayerMovementPlayModeTests` fixture-wide evidence command |
| `Docs/Testing/VFX-Phase3B-Gate-Migration-YAML-Targeted-Evidence-2026-06-06.md` | Gate report added |

## 9. 제거 금지 항목 재확인
- Gameplay_Vfx runtime: retained.
- Gameplay_VfxHost pool/lifecycle/production: retained.
- `GameplayVfxRuntimeRoot.cs`: retained.
- `GameplayVfxHostDefaultCueMap.asset`: retained.
- `GameplayVfxCommonEmptyHost.prefab`: retained.
- topology bridge/post-fx/camera shake: retained and explicitly out of VFX cleanup scope.
- stage companion assets: retained.

## 10. 최종 검증
| 명령 | 결과 | 실패 시 원인 | baseline/touched 구분 |
| --- | --- | --- | --- |
| `git diff --check` | pass | n/a | touched |
| `./run_tests.sh --dry-run core --filter GameplayVfxFlagRolloutPolicyTests` | pass | n/a | touched |
| `./run_tests.sh --dry-run core --filter PlayerMovementPlayModeTests` | pass | n/a | touched |
| `./run_tests.sh core --filter GameplayVfxFlagRolloutPolicyTests` | pass | n/a | touched |
| `./run_tests.sh core --filter GameplayVfxSceneRuntimeRootPlayModeTests` | pass | n/a | touched |
| `./run_tests.sh core --filter GameplayVfxBindingPolicyTests` | pass | n/a | touched |
| `./run_tests.sh core --filter StageDefaultStageIdPolicyTests` | pass | n/a | touched |
| `./run_tests.sh core --filter StageSceneBootstrapValidatorTests` | pass | n/a | touched |
| `./run_tests.sh core --filter ActualSceneBootstrapSmokePlayModeTests` | pass | n/a | touched |
| `./run_tests.sh core --filter SaveSlotValidationAndDirectPlayTests` | pass | n/a | touched |
| `./run_tests.sh ui --filter GameplayShellUiAudioContractTests` | pass | n/a | touched |
| `./run_tests.sh core --filter PlayerMovementPlayModeTests` | pass | n/a | core subset |
| `./run_tests.sh full --filter PlayerMovementPlayModeTests` | fail | fixture-wide PlayerMovement/topology/input timing failures, 15/42 failed | existing isolated/full fixture red |
| `./run_tests.sh core` | pass | n/a | broad baseline |
| `./run_tests.sh ui` | pass | n/a | broad baseline |

## 11. 다음 단계
Gate 통과 시:
- Phase 3B: `EnableGameplayVfx*Migration` field/property declaration deletion.
- `UIAudioScene` current shell reserialization cleanup plus historical `CombinedGameplayShowcase` / `TutorialScene` raw scene deletion or rewrite review.
- tests/docs update.
- final `rg` validation for migration residue 0 in active runtime/scene cleanup scope.

Gate 미통과 시:
- fixture-wide PlayerMovement red는 topology/player-movement owner lane에서 해결한다.
- scene YAML cleanup은 field declaration deletion과 분리하지 않는다.
