# 2026TeamProject_J2M
2026 팀프로젝트 김종우(D) 권민혁(P) 진현우(G)


## Structure

1. 시스템 전체 구조
 ```mermaid
flowchart LR
    Player["플레이어"]
    Developer["개발자, 스테이지 제작자"]
    Steam["Steam Platform<br/>선택적 외부 시스템"]

    subgraph System["VectorQuake 시스템"]
        Client["Windows Game Client<br/>Unity 6, C#"]
        Save[("Local Save Store<br/>JSON")]
        Editor["Unity Editor<br/>Stage Authoring Tools"]
        Build["Build, Test<br/>Release Toolchain"]
    end

    Player -->|"게임 플레이"| Client
    Client -->|"캠페인 진행도, 설정 저장"| Save

    Developer -->|"스테이지 제작"| Editor
    Editor -->|"StageContentEntry 생성"| Build
    Build -->|"검증, 빌드"| Client

    Client -.->|"Steam 빌드에서만<br/>업적, 플랫폼 기능"| Steam
```
2. 게임 클라이언트 내부 구조
```mermaid
flowchart LR
    Input["플레이어 입력"]
    Steam["Steamworks"]
    Output["화면, 소리, 피드백"]

    subgraph Client["VectorQuake Windows Game Client"]
        UI["UI, Navigation<br/>Screen, Popup, HUD"]

        Campaign["Campaign, Stage Flow<br/>슬롯, 재시도, 스테이지 전환"]

        Stage["Stage Content<br/>Resolver, Runtime Builder"]

        Host["Gameplay Scene Host<br/>실행 구성 및 조율"]

        Tick["Deterministic Tick Simulation<br/>Movement, Attack, Cleanup, Respawn"]

        World[("WorldState<br/>유일한 변경 가능 게임 상태")]

        Snapshot["WorldSnapshot<br/>읽기 전용 상태"]

        Result["TickResult<br/>PresentationData"]

        Presentation["Presentation<br/>View, Animation, Audio, VFX, Camera"]

        Achievement["Achievement System<br/>판정, 로컬 기록, 게시"]

        Platform["Platform Runtime<br/>Local, Steam Provider"]

        Persistence["Persistence Infrastructure<br/>원자적 JSON 저장, 복구"]
    end

    Input --> UI
    UI --> Campaign
    Campaign --> Stage
    Stage --> Host
    Host --> Tick

    Tick -->|"Commit 경로로만 변경"| World
    World --> Snapshot
    Snapshot --> Tick
    Tick --> Result
    Result --> Presentation
    Presentation --> Output

    Campaign --> Persistence
    Result --> Achievement
    Achievement --> Platform
    Platform -.->|"선택적 연동"| Steam
```
3. TickPipeline 구조
```mermaid
flowchart LR
    Host["GameplaySceneHost"]
    Runner["TickRunner"]
    World[("WorldState<br/>게임의 실제 상태")]
    Presenter["Presenter<br/>화면, 애니메이션, 오디오"]

    Host --> Runner

    subgraph Pipeline["TickPipeline, 한 Tick 처리"]
        Plan["1. Plan<br/>입력, AI 행동 계획"]
        Movement["2. Movement<br/>이동, 충돌 계산"]
        Attack["3. Attack<br/>공격, 피해 계산"]
        Finalize["4. Finalize<br/>계산 결과 적용"]
        Cleanup["5. Cleanup, Respawn<br/>정리, 재생성"]
        Result["6. TickResult<br/>최종 상태, 화면 표현 정보"]

        Plan --> Movement
        Movement --> Attack
        Attack --> Finalize
        Finalize --> Cleanup
        Cleanup --> Result
    end

    Runner --> Plan
    World -.->|"현재 상태 읽기"| Plan

    Finalize -->|"상태 변경"| World
    Cleanup -->|"정리, 재생성 반영"| World

    Result --> Presenter
```

   
## Docs
- Start with [Docs/Architecture/README.md](Docs/Architecture/README.md) for the current gameplay architecture truth-source.
- Use [Docs/Testing/Gameplay-Test-Automation-Guide.md](Docs/Testing/Gameplay-Test-Automation-Guide.md) for runner usage, governance, and developer workflow.
- Use [Docs/Testing/Full-EditMode-Baseline-2026-04-13.md](Docs/Testing/Full-EditMode-Baseline-2026-04-13.md) for the pinned validation baseline and touched-cluster readout.
