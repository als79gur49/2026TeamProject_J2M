# Gameplay Architecture Docs

이 디렉터리의 canonical entrypoint는 아래 세 문서다.

- [Tick-Simulation-Canonical-Spec.md](./Tick-Simulation-Canonical-Spec.md)
  - tick simulation의 canonical architecture spec
- [Gameplay-Rules-Appendix.md](./Gameplay-Rules-Appendix.md)
  - Push/Flip 등 gameplay rule appendix
- [ADR/ADR-001-Tick-Boundary-and-IR-Visibility.md](./ADR/ADR-001-Tick-Boundary-and-IR-Visibility.md)
  - boundary/IR visibility 관련 현재 결정

현재 top-level에는 non-canonical historical docs도 함께 남아 있다. 이 문서들은 archive slice 전까지는 참고 기록으로만 취급한다.

- blueprint / implementation plan / prompt / legacy removal note
- test stratification governance note

검증 baseline과 실행 명령은 [Docs/Testing/Gameplay-Test-Automation-Guide.md](../Testing/Gameplay-Test-Automation-Guide.md) 및 [Docs/Testing/Full-EditMode-Baseline-2026-04-13.md](../Testing/Full-EditMode-Baseline-2026-04-13.md)를 따른다.
