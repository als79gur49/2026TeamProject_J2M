# VFX Gap3 Production Non-default Visibility Policy Coverage 보고서

## 1. 결론

- production asset 변경 여부: 변경함. `ForwardCellProjectileFlight_Binding.asset`만 `VisibleSurfaceAllowed`로 전환한다.
- test-only 여부: 아님. test-first coverage를 추가한 뒤 production authored binding 1개를 변경한다.
- candidate policy: `VisibleSurfaceAllowed`.
- family: Gameplay request VFX.
- known separate red: 별도 baseline cluster로만 보고한다. touched-cluster pass와 합산하지 않는다.
- 다음 단계: 필요 시 Gap3B에서 direct presentation family non-default policy를 별도 설계한다.

## 2. Candidate Inventory

| Candidate | family | current policy | proposed policy | risk | decision |
| --- | --- | --- | --- | --- | --- |
| `ForwardCellProjectileFlight_Binding.asset` | Gameplay request VFX | `DefaultGameplay` implicit | `VisibleSurfaceAllowed` | source-gate 약화 위험 | test-first 후 production 변경 |
| `BoxSlideSolidStop_Binding.asset` | Gameplay request VFX | `DefaultGameplay` implicit | deferred | stopper/source projection 의미는 있으나 asset 전환 이유가 아직 좁지 않음 | 보류 |
| motion attached follower bindings | Gameplay request VFX | `DefaultGameplay` | deferred | entity semantic gate 중심이라 inactive target projection policy와 다름 | 보류 |
| `BoxDestroySmoke_Binding.asset` | Split / mixed path | `DefaultGameplay` implicit | deferred | immediate/delayed path admission owner가 다름 | 보류 |
| `EnemyDeathMotion`, `BoxDestroyShrink`, OutOfBounds exit | EntityExit / Death direct presentation VFX | `DefaultGameplay` | deferred | Gap2 admission contract와 충돌 가능 | 보류 |
| `FlipDestroySelfMotion`, `ImpactTransientBreak` | Impact / disposition direct presentation VFX | `DefaultGameplay` | deferred | impact/disposition fact admission과 혼동 가능 | 보류 |
| topology helper | Topology helper | presentation helper policy | deferred | PresentationOnly helper lane과 gameplay visibility policy 혼동 위험 | 보류 |

## 3. Test Coverage

| 테스트 | policy | path | source gate | target gate | result |
| --- | --- | --- | --- | --- | --- |
| `ProjectileVfx_VisibleSurfaceAllowedTarget_AllowedOnlyWhenOptIn` | `VisibleSurfaceAllowed` | ForwardCellProjectile runtime release | unchanged | inactive target allowed only with opt-in | baseline pass |
| `ProjectileVfx_VisibleSurfaceAllowed_DoesNotBypassSourceSemanticGate` | `VisibleSurfaceAllowed` | ForwardCellProjectile runtime release | FrontFaceInactive source blocks | inactive target not reached as bypass | added |
| `ProjectileVfx_InactiveFaceExplicitlyAllowed_AllowsInactiveTargetAfterSourceGate` | `InactiveFaceExplicitlyAllowed` | ForwardCellProjectile runtime release | active source allowed | inactive target allowed | added |
| `ProjectileVfx_InactiveFaceExplicitlyAllowed_DoesNotBypassSourceSemanticGate` | `InactiveFaceExplicitlyAllowed` | ForwardCellProjectile runtime release | FrontFaceInactive source blocks | inactive target does not bypass source | baseline pass |
| `ForwardCellProjectileFlight_ProductionBinding_UsesVisibleSurfaceAllowed` | `VisibleSurfaceAllowed` | production binding asset | binding policy preserved | runtime policy preserved | added |

## 4. Production Changes

| asset | old policy | new policy | reason |
| --- | --- | --- | --- |
| `Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Bindings/ForwardCellProjectileFlight_Binding.asset` | implicit `DefaultGameplay` | `VisibleSurfaceAllowed` | ForwardCellProjectile flight is a Gameplay request VFX path that uses the common source/target/anchor visibility gate; visible-surface target projection is meaningful and source semantic gate remains protected. |

## 5. Deferred Families

| family | reason |
| --- | --- |
| EntityExit / Death direct presentation | Gap2 admission contract, non-default policy needs separate design |
| Impact / disposition direct presentation | Gap2 admission contract, separate design |
| Topology helper | PresentationOnly helper lane |

## 6. 검증 결과

| 명령 | 결과 | evidence |
| --- | --- | --- |
| `git diff --check` | pass | no whitespace errors |
| `./run_tests.sh core --filter GameplayVfxBindingPolicyTests` | pass | core EditMode 32/0, core PlayMode 0/0 |
| `./run_tests.sh full --filter GameplayVfxForwardCellProjectileTests` | pass | full EditMode 33/0, full PlayMode 0/0 |
| `./run_tests.sh full --filter GameplayVfxBoxSlideSolidStopPlannerTests` | pass | full EditMode 12/0, full PlayMode 0/0 |
| `./run_tests.sh full --filter GameplayVfxEnemyMotionAttachedFollowerTests` | pass | full EditMode 60/0, full PlayMode 0/0 |
| `./run_tests.sh full --filter GameplayVfxLifecycleTests` | pass | full EditMode 55/0, full PlayMode 0/0 |
| `./run_tests.sh full --filter GameplayVfxArchitectureTests` | pass | full EditMode 32/0, full PlayMode 0/0 |
| `./run_tests.sh full --filter PlayerMovementPlayModeTests` | known separate red | full EditMode 0/0, full PlayMode 42 total / 14 failed |
| `./run_tests.sh full --filter WorldSnapshotAndPresentationTests` | known separate red | full EditMode 100 total / 3 failed; `WorldSnapshot` constructor `MissingMethodException` |
| `./run_tests.sh full --filter TickReplayDeterminismTests` | pass | full EditMode 65/0, full PlayMode 0/0 |

## 7. 다음 단계

- 필요 시 Gap3B: Direct presentation family non-default policy design.
