# Architecture Docs

이 디렉터리의 canonical architecture entrypoint는 아래 네 문서다.

- [Tick-Simulation-Canonical-Spec.md](./Tick-Simulation-Canonical-Spec.md)
  - tick simulation의 canonical architecture spec
- [UI-Architecture-Guidelines.md](./UI-Architecture-Guidelines.md)
  - gameplay authoritative boundary를 UI layer까지 확장한 canonical UI architecture spec
- [Gameplay-Rules-Appendix.md](./Gameplay-Rules-Appendix.md)
  - Push/Flip 등 gameplay rule appendix
- [ADR/ADR-001-Tick-Boundary-and-IR-Visibility.md](./ADR/ADR-001-Tick-Boundary-and-IR-Visibility.md)
  - boundary/IR visibility 관련 현재 결정

읽는 순서는 아래를 기준으로 고정한다.

1. [Tick-Simulation-Canonical-Spec.md](./Tick-Simulation-Canonical-Spec.md)
2. [UI-Architecture-Guidelines.md](./UI-Architecture-Guidelines.md)
3. [Gameplay-Rules-Appendix.md](./Gameplay-Rules-Appendix.md)
4. [ADR/ADR-001-Tick-Boundary-and-IR-Visibility.md](./ADR/ADR-001-Tick-Boundary-and-IR-Visibility.md)

운영 가이드와 baseline은 별도 supporting truth-source다. 이 문서들은 canonical architecture spec을 대체하지 않지만, 현재 runner/governance/evidence 기준을 고정하는 active truth-source로 함께 읽어야 한다.

- [Docs/Testing/Gameplay-Test-Automation-Guide.md](../Testing/Gameplay-Test-Automation-Guide.md)
  - current runner/governance truth for `./run_tests.sh core`, `./run_tests.sh ui`, and PlayMode escalation expectations
- [Audio-Architecture-Guidelines.md](./Audio-Architecture-Guidelines.md)
  - current supporting truth for 2D non-spatial audio contracts, runtime ownership, and audio seam vocabulary
- [Gameplay-Audio-Governance.md](./Gameplay-Audio-Governance.md)
  - current supporting truth for gameplay audio semantic-family governance, host one-shot controller scope, and safe semantic expansion protocol
- [Docs/Testing/UI-EditMode-Baseline-2026-04-15.md](../Testing/UI-EditMode-Baseline-2026-04-15.md)
  - pinned UI evidence truth for the completed Stage 1–9 UI architecture baseline
- [Docs/Testing/Full-EditMode-Baseline-2026-04-13.md](../Testing/Full-EditMode-Baseline-2026-04-13.md)
  - broader full-suite baseline context, not the defining truth-source for the Stage 1–9 UI freeze baseline

historical/non-canonical 문서는 더 이상 이 디렉터리의 active truth-source가 아니다.

- archive index: [Docs/Archive/README.md](../Archive/README.md)
- archived architecture docs: [Docs/Archive/Architecture](../Archive/Architecture)
