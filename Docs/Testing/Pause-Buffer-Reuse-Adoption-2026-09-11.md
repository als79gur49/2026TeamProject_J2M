# Pause 등록 버퍼 재사용 선택 반영

사용자가 2026-09-11 A/B/C 실험 검토 후 B(List 재사용)를 최종 선택했다. 현재 개발 기준 `261f2df07ccb03c8e840bb02f7b1519c2a0f7d84`에 B의 버퍼 관리와 동작 회귀 테스트만 이식한다.

## 반영 범위

- registry별 MonoBehaviour/Animator/ParticleSystem List를 재사용한다.
- 최상위 호출은 List, 재진입은 기존 배열 경로를 사용한다.
- inactive 포함 검색과 MB 검색→등록→Animator 검색→등록→Particle 검색→등록 순서를 유지한다.
- 소유 호출의 finally에서 버퍼 참조와 사용 표시를 정리한다. Clear는 활성 버퍼를 건드리지 않는다.
- List 용량은 유지한다. 큰 root 처리 후 용량 보유와 중첩 호출의 배열 사용을 허용하는 설계다.

실험용 비교 계측/schema/writer/validator, C의 Component 통합 검색, root 캐시와 등록 시점 변경은 포함하지 않는다. 현재 개발 기준에는 실험 기준의 reflection 메서드 캐시도 없으므로, 이 변경은 기존 reflection 구현을 유지한다. 실험 B 브랜치 전체의 병합이나 실험 B 커밋의 단독 cherry-pick이 아니다.

## 성능 해석

원본 실험은 16개 독립 Player 실행, 실행당 100 Tick이며 stage-4-3 neutral workload에 한정된다. A→B에서 pause 등록 관측 평균은 약 10.97% 낮았지만 네 인접 쌍 중 한 쌍은 반대 방향이었고 95% 신뢰구간이 0을 포함했다. 사전 통계 규칙의 선택 A는 그대로 보존하고, 사용자의 최종 선택 B를 별도 기록했다.

이 이식본은 실험 기준과 코드·계측 구성이 다르다. 따라서 실험의 11%를 이식본에 보장된 개선으로 사용하지 않는다. 전체 Tick/FPS 개선과 pause 전용 할당 감소도 입증하지 않았다.

- 원본 실험: `/mnt/d/J2M/evidence/pause-comparison-run-20260911-135755/Result.md`
- 선택 검토: `/mnt/d/J2M/evidence/pause-comparison-run-20260911-135755/B-selection-review.md`
- 이식 검증: `/mnt/d/J2M/evidence/pause-buffer-integration-20260911`

## 회귀 검증 범위

별도 GameplayPauseBufferReuseTests fixture에서 바깥 세 대상과 재진입 순서, Clear·직접/reflection 예외 후 정상 등록, 콜백·Equals 중 display 추가, Equals 중 Clear·재진입, inactive/늦은 대상의 중복 방지, 분리·pool 재부착·파괴 후 수명을 검증한다. 기존 GameplayTickPresentationCoordinatorTests와 관련 pause PlayMode도 함께 실행한다.

UI와 UI 계측 writer는 변경하지 않으므로 UI lane은 이번 이식에서 실행하지 않는다. broad unfiltered full과 새 Player 성능 비교도 실행하지 않으며 해당 범위의 통과를 주장하지 않는다. 구체적인 실행 결과는 아래 검증 기록에 기재한다.

### 이식 상태 검증 기록

- `./run_tests.sh full --filter '<새 버퍼 fixture + 기존 pause 16개 + PlayMode 2개>'`: Windows dotnet full build 통과, EditMode 25 passed / 0 failed, PlayMode 2 passed / 0 failed. 정확한 필터는 외부 증거의 `pause-focused.json`에 보존한다.
- coordinator 전체 fixture를 함께 선택한 최초 실행: EditMode 254 total / 246 passed / 8 failed. 실패 후 PlayMode는 실행되지 않았다.
- 위 8개 실패를 이식 전 HEAD의 원본 런타임으로 재실행하여 테스트 이름과 오류 메시지가 모두 동일함을 확인했다(`baseline-comparison.json`). 기존 표시 설정/중력장/애니메이션 fixture 실패이며 이 변경에서 수정하지 않았다.
- core는 필수 커밋 훅으로 검증한다. 최종 커밋·훅 결과·대상 브랜치 반영·사용자 변경 보존 결과는 외부 증거의 `Result.md`에 기록한다.
