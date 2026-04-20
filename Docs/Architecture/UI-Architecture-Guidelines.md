# UI Architecture Guidelines

This document is the canonical UI architecture spec for this project.

It defines the architecture contract for future UI implementation, architecture review, and maintenance. It extends the active gameplay architecture into the UI layer without redefining gameplay ownership, deterministic tick semantics, commit paths, or simulation truth.

The goal is simple: UI must remain a consumer of authoritative gameplay state and a forwarder of user intent. UI must not become a second domain owner.

## 1. Purpose

This document is the single source of truth for project UI architecture.

It governs UI boundaries, layering, naming, flow ownership, lifecycle ownership, and review expectations for screens, popups, HUD, presenters, viewmodels, and views.

It is written to be enforceable. Future implementation and PR review must treat its non-negotiable rules as architecture constraints, not as style guidance.

## 2. Canonical Relationship to Gameplay Architecture

This document is part of the active architecture truth-source chain in [README.md](./README.md).

Gameplay canonical documents remain authoritative for gameplay ownership, deterministic tick semantics, commit-path mutation, simulation truth, and gameplay vocabulary that is already canonically defined.

The current gameplay truth-source chain is:

- [Tick-Simulation-Canonical-Spec.md](./Tick-Simulation-Canonical-Spec.md)
- [Gameplay-Rules-Appendix.md](./Gameplay-Rules-Appendix.md)
- [ADR/ADR-001-Tick-Boundary-and-IR-Visibility.md](./ADR/ADR-001-Tick-Boundary-and-IR-Visibility.md)

This UI document derives UI constraints from those documents. It does not override, reinterpret, or replace them.

Current repository architecture facts that this document extends are:

- `WorldState` is the only authoritative write target.
- authoritative writes happen through commit paths only.
- gameplay flow is driven by deterministic fixed-tick simulation.
- presentation is separated from gameplay by `TickResult -> TickPresentationData -> ViewPresenter`.
- host and view code must not bind directly to phase-private mutable gameplay results.

Current code anchors for those facts are:

- `Assets/_Features/Gameplay/Gameplay_BoardState/Runtime/WorldState.cs`
- `Assets/_Features/Gameplay/Gameplay_Loop/Runtime/TickPipeline.cs`
- `Assets/_Features/Gameplay/Gameplay_Loop/Runtime/TickResult.cs`
- `Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickViewPresenter.cs`

Conflict rule:

- If this document and a gameplay canonical document appear to conflict, the gameplay canonical document wins.
- Implementation must follow the gameplay canonical document immediately.
- This UI document must then be updated to remove the inconsistency.
- A UI implementation must not use this document as justification to bypass gameplay ownership or tick rules.

## 3. Core Principles

- UI is not an authoritative gameplay owner.
- UI reads authoritative gameplay state and forwards user intent.
- UI flow is centrally coordinated.
- Presentation state is derived from authoritative outcomes.
- View logic stays local and non-domain.
- Screen, popup, and HUD are separate architectural concepts.
- Local convenience must not bypass canonical boundaries.

## 4. Rule Severity Model

This document uses three rule classes.

- `Non-Negotiable`
  - Absolute architecture constraint.
  - A violation requires an architecture change, not a local implementation choice.
- `Default Guidance`
  - Required default direction.
  - Deviation needs an explicit documented exception.
- `Optional / Local Choice`
  - Implementation-local choice allowed inside canonical boundaries.
  - Local choice must not weaken non-negotiable rules or redefine canonical vocabulary.

Severity must never be left implicit.

In sections that define mixed rules, each rule must be marked with one of these labels.

## 5. Non-Negotiable Rules

- `Non-Negotiable` UI never writes `WorldState` directly.
- `Non-Negotiable` UI never calls committers or other authoritative mutation paths directly.
- `Non-Negotiable` UI reads gameplay only through query, reader, facade, snapshot, or presentation access contracts that are explicitly safe for UI use.
- `Non-Negotiable` View code never contains gameplay rules.
- `Non-Negotiable` Presenter code is orchestration only. It is not a gameplay domain owner.
- `Non-Negotiable` UI screens do not directly open or close other screens.
- `Non-Negotiable` UI open and close behavior goes through flow controllers and coordinator-owned flow rules.
- `Non-Negotiable` Tick-based authoritative results drive gameplay-derived presentation state.
- `Non-Negotiable` Screen and popup do not share one mixed stack.
- `Non-Negotiable` HUD is not treated as a normal screen or popup.
- `Non-Negotiable` UI must not bind to uncommitted mid-tick mutable gameplay state.
- `Non-Negotiable` If a screen presenter grows across multiple responsibilities, it must be split before it becomes a hidden domain owner.

## 6. Default Guidance

- `Default Guidance` Keep UI decisions in the application and flow layers, not in Unity view scripts.
- `Default Guidance` Prefer read-only contracts that match gameplay vocabulary already used in the repo.
- `Default Guidance` Prefer small focused presenters over one presenter that owns mapping, navigation, policy, and feature logic together.
- `Default Guidance` Keep screen-local viewmodels narrow and presentation-shaped.
- `Default Guidance` Keep controller responsibilities aligned to one UI category or one bounded screen flow.
- `Default Guidance` Prefer explicit policy objects over ad hoc booleans spread across views.
- `Default Guidance` Prefer per-feature UI modules over a single monolithic UI bucket.
- `Default Guidance` Keep temporary local state local. Promote it only when flow or persistence rules require it.

## 7. Exceptions and Local Variations

Exceptions are allowed only when they preserve the canonical boundary and are explicitly documented.

An exception is valid only if all of the following are true:

- it has a clear justification
- it names the rule being deviated from
- it preserves gameplay authoritative ownership
- it is scoped to a feature or migration window
- it has an owner
- it has a removal condition if it is temporary

Required exception categories and limits:

- `Default Guidance` Speculative UI is allowed only as explicitly non-authoritative presentation state.
- `Non-Negotiable` Speculative UI must reconcile back to authoritative tick results.
- `Default Guidance` Temporary cached screen-local state is allowed for local presentation concerns such as selection highlight, scroll position, or local filter state.
- `Non-Negotiable` Cached screen-local state must not become gameplay truth.
- `Default Guidance` Non-modal overlays are allowed only through explicit flow and block policy.
- `Default Guidance` UI-local transient animation state is allowed in the view or viewmodel layer when it does not redefine gameplay state.
- `Default Guidance` Migration-time exceptions are allowed only with explicit owner, scope, and removal plan.
- `Non-Negotiable` Undocumented exceptions are violations.

`Optional / Local Choice` Local implementation may choose exact internal composition, helper types, or data carriers when those choices stay inside the canonical layer and boundary rules defined here.

## 8. Gameplay ↔ UI Boundary

This boundary is an extension of the gameplay architecture already defined in the canonical gameplay docs. UI must fit under that truth, not compete with it.

Allowed read path:

- `Non-Negotiable` UI reads gameplay through the gameplay access layer only.
- `Non-Negotiable` The gameplay access layer exposes read-only seams such as queries, readers, facades, snapshots, and presentation contracts.
- `Default Guidance` UI-safe read models should prefer committed state, `TickResult`-derived presentation contracts, or snapshots derived from committed state.

Allowed intent path:

- `Non-Negotiable` UI forwards user intent into the UI application layer.
- `Non-Negotiable` The UI application layer routes that intent into approved gameplay-facing entry points without granting UI direct write ownership.

Disallowed path:

- `Non-Negotiable` Views, viewmodels, presenters, coordinators, and controllers do not mutate `WorldState`.
- `Non-Negotiable` UI code does not call gameplay committers directly.
- `Non-Negotiable` UI code does not depend on phase-private mutable simulation internals.
- `Non-Negotiable` Gameplay modules do not depend on concrete UI view classes.

Repository traceability:

- `Tick-Simulation-Canonical-Spec.md` already defines `WorldState` as the authoritative write target and `TickResult -> TickPresentationData -> ViewPresenter` as the presentation boundary.
- This document extends that same boundary into the future UI architecture.
- `Assets/_Features/UI` and `Assets/_Shared/UI` are the current UI feature anchors in the repo and should grow under this boundary rather than around it.

Tick-result presentation mapping rule:

- `Non-Negotiable` `Gameplay.UIAccess` does not expose `TickResult` directly to UI.
- `Non-Negotiable` `IGameplayPresentationFeed` is consumed by one UI-application-owned presentation source/store only.
- `Non-Negotiable` `GameplayUiPresentationSource` is the sole application-owned bridge from gameplay presentation and gameplay-owned UI access reads into UI-facing state.
- `Non-Negotiable` `UITickEventRouter` interprets one authoritative frame into semantic UI events without cross-frame memory.
- `Non-Negotiable` `UIStateMapper` owns durable snapshot reduction, semantic notification retention, dedupe, and tick-based expiry.
- `Non-Negotiable` `UIPresentationSnapshot` stays bounded to durable cross-feature UI state. Screen-local viewmodels and presenter-local state remain feature-local.
- `Default Guidance` Presenters and controllers consume mapped snapshots or query reads triggered by that source/store, not raw frame deltas.
- `Default Guidance` The root presentation snapshot must stay bounded to durable cross-feature slices. Screen-local viewmodels remain feature-local.

Persistent HUD hardening rule:

- `Non-Negotiable` `HUDRootPresenter` is the sole HUD-side subscriber to mapped presentation state and owns shell-level fan-out only.
- `Non-Negotiable` `HUDRootPresenter` must not absorb command dispatch, gameplay queries, widget retention/history, notification policy, slot policy, or widget formatting.
- `Non-Negotiable` `HUDController` remains lifecycle, view binding, and bounded child-input relay only.
- `Non-Negotiable` `HUDRootViewModel` stays shell-only. Child collections, widget strings, and convenience mirrors remain child-local.
- `Non-Negotiable` Child HUD views bind only their own child-local viewmodels. Do not recreate `Bind(UIPresentationSnapshot)` through a composite root tree.
- `Default Guidance` For new HUD semantics, keep projection local to the owning child presenter first, expand a child-local viewmodel second, and request Stage 4 mapped contract growth only when the semantic is authoritative, non-derivable, stable, and needed by more than one consumer.
- `Default Guidance` Richer HUD read-only behavior must continue to flow through the mapped refresh seam. If multiple consumers need reason-specific behavior later, add one compact UI-safe interaction-mode field rather than parallel flow-owned HUD flags.

Composition root and gameplay-host bridge rule:

- `Non-Negotiable` `UI_Composition` is the sole runtime composition root and gameplay-host bridge for the UI runtime.
- `Non-Negotiable` `GameplayUiFlowInstaller` assembles the runtime from gameplay-owned `UIAccess` seams and remains the only UI boundary that depends on gameplay host initialization.
- `Non-Negotiable` `GameplayUiCanvasRootView` owns runtime canvas and layer composition only. It must not become a gameplay, flow, or feature-state owner.
- `Non-Negotiable` Audio settings bridging remains composition-owned. `UI.Application` knows only `IAudioSettingsPort`, and only `UI_Composition` may translate visible audio settings channels to shared-audio runtime channels.
- `Non-Negotiable` The visible-audio-channel mapping is centralized in one composition-owned mapper. Presenters, screens, and non-composition UI assemblies must not duplicate that mapping logic inline.
- `Default Guidance` Screen runtime factories, popup runtime factories, and composition-owned diagnostics wiring belong in `UI_Composition`, not in feature presenters or gameplay access contracts.

## 9. Final Layered Architecture

The canonical UI architecture uses five layers.

`Gameplay authoritative layer -> Gameplay access layer for UI -> UI application layer -> UI ViewModel / presentation model layer -> UI View layer`

Layer responsibilities:

- `Gameplay authoritative layer`
  - Owns `WorldState`, committers, tick pipeline, and deterministic gameplay rules.
- `Gameplay access layer for UI`
  - Owns UI-safe gameplay reads, presentation extraction, and read-only access seams.
- `UI application layer`
  - Owns UI use cases, flow decisions, transition requests, and interaction policy coordination.
- `UI ViewModel / presentation model layer`
  - Owns bindable derived state only.
- `UI View layer`
  - Owns rendering, animation, and forwarding user events upward.

Dependency rules:

- `Non-Negotiable` Upper layers depend on lower-layer contracts, not lower-layer concrete implementation details.
- `Non-Negotiable` UI views do not bypass the application layer to reach gameplay.
- `Non-Negotiable` The gameplay access layer remains read-focused for UI use.
- `Default Guidance` Keep Unity runtime object concerns in the view layer or in UI-specific controller code, not in gameplay access contracts.

Module note:

- `UI_Composition` is a runtime composition boundary that assembles controllers, presenters, views, runtime factories, and diagnostics from already-approved contracts.
- `UI_Composition` is not a sixth ownership layer. It must not absorb gameplay truth, screen/popup/HUD policy ownership, or feature-local presentation logic.

## 10. Vocabulary and Naming Rules

Architectural vocabulary is locked. One term must map to one meaning.

- `Coordinator`
  - Cross-controller flow authority.
  - Owns decisions that span screen, popup, HUD, or global navigation state.
- `Controller`
  - Lifecycle and state owner for one UI category or one bounded UI node.
  - Does not redefine global flow policy.
- `Presenter`
  - Orchestrates mapping from access-layer or application-layer inputs into presentation state and UI actions.
  - Does not own gameplay truth.
- `ViewModel`
  - Bindable presentation state for one screen, popup, HUD slice, or component slice.
  - Does not encode gameplay ownership.
- `View`
  - Renders UI and forwards user events.
  - Does not own gameplay rules or navigation policy.
- `Query`
  - Narrow read contract, usually request and return.
- `Reader`
  - Read-oriented service interface that exposes stable access methods.
- `Facade`
  - Read-oriented aggregation seam that hides multiple lower read sources behind one UI-safe contract.
- `Access Layer`
  - The layer that exposes UI-safe gameplay reads and presentation-facing access seams.
- `Application Layer`
  - The layer that owns UI use cases, flow decisions, and intent routing.
- `Composition Root`
  - The runtime-owned boundary that assembles the UI runtime from approved gameplay access seams, flow owners, presenters, views, and factories.
- `Gameplay-Host Bridge`
  - The composition-owned boundary that connects gameplay-owned `UIAccess` seams to the UI runtime without granting UI authoritative gameplay ownership.
- `Mapped Presentation Seam`
  - The single application-owned path from gameplay presentation frames and read models into bounded UI-facing snapshot state.
- `Flow`
  - The controlled transition model for screens, popups, HUD, and interaction state.
- `Screen Runtime`
  - The runtime object owned by screen flow code that binds one screen request, payload, and policy to a mounted screen instance.
- `Modal`
  - A UI state that blocks lower-layer interaction according to explicit policy.
- `Overlay`
  - A visual layer above another UI layer. It is not automatically modal.
- `Screen`
  - The main active navigable UI context with optional back stack.
- `Popup`
  - An explicit stacked overlay entry with controlled lifetime.
- `HUD`
  - A persistent layer that is not part of the screen stack or popup stack.

Naming intent:

- `Non-Negotiable` Use these terms consistently in code and documentation.
- `Non-Negotiable` Do not use different names for the same architecture role without updating this canonical document.
- `Default Guidance` Avoid generic `Manager` names when a canonical role such as `Coordinator`, `Controller`, or `Presenter` already fits.

## 11. Flow Management Structure

The canonical UI flow structure is:

- `UIFlowCoordinator`
- `ScreenController`
- `PopupController`
- `HUDController`
- `UIBlockPolicy`

Responsibilities:

- `UIFlowCoordinator`
  - Cross-layer flow authority.
  - Owns routing, popup-first-back handling, and cross-layer sequencing for screen, popup, HUD, and input-blocking state.
  - Does not own feature-local presentation logic, popup payload formatting, or screen-internal presenter behavior.
- `ScreenController`
  - Owns current screen, optional back stack, reuse/restore semantics, and screen runtime lifecycle.
- `PopupController`
  - Owns the explicit identity-based popup stack, topmost state, and popup lifetime transitions.
- `HUDController`
  - Owns persistent HUD lifecycle, child-view binding, and bounded input relay to the owning HUD presenter.
- `UIBlockPolicy`
  - Owns interaction blocking and modal policy evaluation.

Interaction rules:

- `Non-Negotiable` Cross-cutting flow decisions go through `UIFlowCoordinator`.
- `Non-Negotiable` Popup-first back handling is centralized in `UIFlowCoordinator`.
- `Non-Negotiable` Screens do not directly open, close, or replace other screens.
- `Non-Negotiable` Screens do not directly manage popup stack state.
- `Non-Negotiable` HUD does not manage screen navigation.
- `Non-Negotiable` Pause remains popup-owned. Gameplay-root back may open the pause popup, but pause is not a screen taxonomy example.
- `Non-Negotiable` `UIBlockPolicy` decides interaction blocking. Visual hierarchy alone does not.
- `Default Guidance` Keep controllers narrow. Put cross-controller rules in the coordinator, not duplicated in each controller.

Composition note:

- `GameplayUiFlowInstaller` and composition-owned runtime factories assemble the flow runtime around these owners but are not additional flow authorities.

## 12. Screen / Popup / HUD Policy

Definitions:

- `Screen`
  - One current screen plus optional back stack.
- `Popup`
  - Explicit stack.
- `HUD`
  - Persistent layer.

Policy rules:

- `Non-Negotiable` Screen and popup do not share a mixed stack.
- `Non-Negotiable` HUD is not stored in the screen stack.
- `Non-Negotiable` HUD is not stored in the popup stack.
- `Non-Negotiable` A popup is opened and closed through popup flow control, not by ad hoc scene activation.
- `Non-Negotiable` `PopupController` owns popup identity, stack order, lifetime, and completion routing rather than popup prefabs or popup views.
- `Non-Negotiable` `ScreenController` owns the explicit screen runtime layer and current-screen/back-stack semantics.
- `Non-Negotiable` Pause remains popup-owned and must not be reclassified as a screen just because gameplay-root back can route into it.
- `Default Guidance` Screen transitions use replace, push, and pop semantics that are explicit in flow code.
- `Default Guidance` Popup transitions use explicit push and pop semantics.
- `Default Guidance` HUD persists across screen changes unless the coordinator intentionally reconfigures it.
- `Optional / Local Choice` A feature may define multiple HUD regions or multiple popup presentation styles as long as the canonical ownership model does not change.

## 13. Presenter / View / ViewModel Responsibility Rules

Responsibility split:

- `Presenter`
  - Reads approved access-layer or application-layer inputs.
  - Maps them into presentation state.
  - Routes UI events upward into application or flow actions.
- `ViewModel`
  - Holds presentation-ready state only.
- `View`
  - Binds, renders, animates, and forwards events.

Rules:

- `Non-Negotiable` Presenter is orchestration only, not domain owner.
- `Non-Negotiable` View never contains gameplay rules.
- `Non-Negotiable` ViewModel never mutates gameplay state directly.
- `Non-Negotiable` Presenter does not call gameplay committers directly.
- `Non-Negotiable` Presenter must be split by responsibility when a screen grows across multiple concerns.
- `Non-Negotiable` Complex screen decomposition remains screen-internal. It must not widen Stage 4 mapped presentation, Stage 5 HUD, Stage 6 popup, or Stage 7 screen-runtime seams.
- `Non-Negotiable` A complex screen root presenter owns only root orchestration and canonical shared selection. Child presenters keep local projection, local viewmodels, and bounded child-local behavior.
- `Non-Negotiable` Child presenters must not form sibling meshes or absorb popup, HUD, flow, or gameplay-authoritative ownership.
- `Default Guidance` Split by bounded responsibility such as flow orchestration, feature-specific mapping, or persistent HUD slice ownership before one presenter accumulates all three.
- `Default Guidance` Keep viewmodels shaped for binding, not for domain reuse.

## 14. Lifecycle Rules

Lifecycle is controller-owned and coordinator-governed.

Screen lifecycle:

- `Non-Negotiable` Screen creation, show, hide, suspend, resume, and dispose are owned by screen flow code.
- `Default Guidance` Back-stack restore should resume screen state through the screen controller rather than through direct view self-activation.

Popup lifecycle:

- `Non-Negotiable` Popup push, focus, unfocus, pop, and dispose are owned by popup flow code.
- `Default Guidance` Underlying popup or screen reactivation after popup close should be policy-driven, not hard-coded in views.

HUD lifecycle:

- `Non-Negotiable` HUD lifecycle is owned separately from screen and popup lifetime.
- `Default Guidance` HUD stays resident unless coordinator flow changes require reconfiguration.

Composition lifecycle:

- `Non-Negotiable` `UI_Composition` owns runtime bootstrap, gameplay-host binding, runtime factory selection, canvas/layer assembly, and composition-only diagnostics registration.
- `Non-Negotiable` `GameplayUiFlowInstaller` assembles `GameplayUiPresentationSource`, controllers, coordinator, presenters, and root view binding from gameplay-owned `UIAccess` seams.
- `Default Guidance` `GameplayScreenRuntimeFactory` and `GameplayPopupRuntimeFactory` stay composition-owned because they translate flow/runtime requests into mounted Unity runtime objects.

Subscription rules:

- `Non-Negotiable` UI subscriptions to gameplay access contracts must be registered and removed in lifecycle-safe places.
- `Non-Negotiable` Destroyed or hidden UI must not continue mutating presentation state through stale subscriptions.
- `Default Guidance` Keep transient UI-local state disposable unless application-level persistence is explicitly required.

Diagnostics lifecycle:

- `Non-Negotiable` Stage 9 diagnostics remain composition-only, read-only, and non-owning.
- `Non-Negotiable` Diagnostics must not be exposed as gameplay access seams, flow-owner APIs, presenter contracts, or feature query services.
- `Default Guidance` Diagnostics may summarize current screen, popup, HUD, block, and mapped-event state for development visibility, but they must not become runtime aggregation or decision paths.

## 15. Tick-Based Presentation Rules

This project already uses `TickResult -> TickPresentationData -> ViewPresenter` to separate gameplay and presentation. UI architecture must preserve that direction.

Rules:

- `Non-Negotiable` Gameplay-derived UI state must come from authoritative tick results, committed snapshots, or approved UI-safe read contracts derived from them.
- `Non-Negotiable` UI must not read uncommitted mid-tick mutable state to decide authoritative presentation.
- `Non-Negotiable` UI must not infer hidden gameplay truth that is not exposed by approved access contracts.
- `Default Guidance` Presenters should translate authoritative outcomes into viewmodels rather than pass raw gameplay objects into views.
- `Default Guidance` If speculative visual feedback is needed, keep it visibly non-authoritative and reconcile it when authoritative tick results arrive.

## 16. Input Blocking and Modal Policy

`UIBlockPolicy` is the canonical owner of blocking and modal rules.

Rules:

- `Non-Negotiable` Modal behavior is defined by policy, not by draw order alone.
- `Non-Negotiable` A modal popup blocks lower interactive layers according to `UIBlockPolicy`.
- `Non-Negotiable` Gameplay-facing input must be blockable by UI policy when modal UI requires it.
- `Default Guidance` Non-modal overlays should declare what they block and what they allow.
- `Default Guidance` HUD interactability under overlay should be an explicit policy decision, not an implicit default.
- `Optional / Local Choice` Exact internal implementation of the blocking mechanism may vary as long as policy remains centralized and enforceable.

## 17. Folder and Naming Guidelines

The repo already has these UI anchors:

- `Assets/_Features/UI`
- `Assets/_Shared/UI`

The canonical folder direction is:

```text
Assets/
  _Features/
    UI/
      UI_Composition/
        Runtime/
      UI_Flow/
        Runtime/
      UI_Application/
        Runtime/
      UI_Screens/
        Runtime/
      UI_Popups/
        Runtime/
      UI_HUD/
        Runtime/
  _Shared/
    UI/
      Runtime/
```

Folder intent:

- `UI_Composition`
  - runtime composition root, gameplay-host bridge, installer/bootstrap, canvas/layer assembly, runtime factories, and composition-only diagnostics
- `UI_Flow`
  - coordinator, controllers, policy, and flow state
- `UI_Application`
  - UI use cases and UI-facing intent orchestration
- `UI_Screens`
  - screen-specific presenters, viewmodels, views, and screen composition
- `UI_Popups`
  - popup-specific presenters, viewmodels, views, and popup composition
- `UI_HUD`
  - persistent HUD-specific presenters, viewmodels, views, and HUD composition
- `Shared/UI`
  - reusable UI contracts, shared widgets, and shared UI helpers that do not own feature flow

Naming rules:

- `Non-Negotiable` Flow authority types use `Coordinator` or `Controller` according to the vocabulary in this document.
- `Non-Negotiable` Presentation orchestration types use `Presenter`.
- `Non-Negotiable` Bindable state types use `ViewModel`.
- `Non-Negotiable` Render components use `View`.
- `Non-Negotiable` Gameplay-facing read seams use names such as `Query`, `Reader`, or `Facade` that do not imply write ownership.
- `Default Guidance` Prefer namespaces under `Game.Feature.UI.*` and `Game.Shared.UI.*`.
- `Default Guidance` Avoid `UIManager` unless this document is updated to define a manager role that is not already covered by the canonical vocabulary.

## 18. Testing Guidance

Future UI implementation must be tested against architecture behavior, not only visible rendering.

Required test directions:

- `Non-Negotiable` Add unit tests for coordinator and controller flow ownership.
- `Non-Negotiable` Add unit tests for `UIBlockPolicy`.
- `Non-Negotiable` Add presenter or viewmodel tests for authoritative read-model to presentation mapping.
- `Non-Negotiable` Add architecture guard tests for dependency direction, bounded public surfaces, and composition-only diagnostics boundaries.
- `Default Guidance` Add integration tests for screen, popup, and HUD interaction boundaries.

Required scenarios:

- `Non-Negotiable` Screen replace and back-stack behavior.
- `Non-Negotiable` Popup push and pop ordering.
- `Non-Negotiable` HUD persistence across screen changes.
- `Non-Negotiable` Modal popup input blocking.
- `Non-Negotiable` Popup-owned pause behavior and popup-first-back routing.
- `Non-Negotiable` No direct screen-to-screen open or close path.
- `Non-Negotiable` No direct UI-driven authoritative gameplay mutation path.
- `Non-Negotiable` Tick-result-driven presentation refresh.
- `Non-Negotiable` Representative complex screen decomposition remaining bounded and screen-internal.
- `Non-Negotiable` Composition-only diagnostics remaining read-only and non-reusable as runtime state aggregation.

## 19. Anti-Patterns / Forbidden Patterns

- `Non-Negotiable` View code mutating `WorldState`.
- `Non-Negotiable` Presenter code calling committers directly.
- `Non-Negotiable` Button handlers implementing gameplay legality rules.
- `Non-Negotiable` One mixed stack for screens, popups, and HUD.
- `Non-Negotiable` Treating HUD as a disguised popup.
- `Non-Negotiable` One screen directly opening or closing another screen.
- `Non-Negotiable` Using draw order or scene hierarchy alone as the blocking model.
- `Non-Negotiable` Passing raw mutable gameplay internals directly into views.
- `Non-Negotiable` A monolithic presenter owning navigation, gameplay interpretation, modal policy, and rendering state together.
- `Non-Negotiable` Undocumented migration exceptions.
- `Non-Negotiable` Mixed terminology where multiple names refer to one canonical role.

## 20. Maintenance / Update Policy

This document must stay aligned with implementation and with the gameplay canonical docs it extends.

Update this document when:

- UI architecture behavior changes materially
- flow ownership changes
- vocabulary changes
- folder or module boundaries change materially
- exception patterns become repeated or permanent
- gameplay canonical docs change in a way that affects UI constraints

PR policy:

- `Non-Negotiable` A PR that materially changes canonical UI architecture must update this document in the same PR.
- `Non-Negotiable` A PR must not merge with architecture-changing UI behavior while leaving this document stale.
- `Default Guidance` If the canonical reading chain changes, update [README.md](./README.md) in the same PR.

Maintenance rule:

- If implementation and this document diverge, the mismatch is a defect.
- The fix is to update implementation, update this document, or both in one reviewed change.

## 21. Implementation Order Summary

1. Define gameplay access contracts for UI reads and approved UI-facing intent entry points.
2. Implement `UIFlowCoordinator`, `ScreenController`, `PopupController`, `HUDController`, and `UIBlockPolicy`.
3. Establish presenter, viewmodel, and view base conventions under the canonical vocabulary.
4. Implement one vertical slice with one screen, one popup, and one HUD element under these rules.
5. Add tests for flow ownership, blocking policy, and authoritative tick-result-driven presentation.
6. Expand UI breadth only after the shared seams and policy rules are stable.

## 22. Acceptance Checklist

- [ ] The section order matches the canonical structure defined for this document.
- [ ] The document clearly states that it is the canonical UI architecture spec.
- [ ] The document defines its relationship to the gameplay canonical docs.
- [ ] The document states that gameplay canonical docs remain authoritative for gameplay ownership, tick semantics, commit paths, and simulation truth.
- [ ] The document includes an explicit conflict rule and conflict resolution behavior.
- [ ] The document defines the rule severity model.
- [ ] The document separates hard constraints from default guidance.
- [ ] The document defines an explicit exception policy.
- [ ] The document defines `UIFlowCoordinator`, `ScreenController`, `PopupController`, `HUDController`, and `UIBlockPolicy`.
- [ ] The document defines `UI_Composition` as the sole runtime composition root and gameplay-host bridge.
- [ ] The document defines `Screen`, `Popup`, `HUD`, `Presenter`, `View`, `ViewModel`, `Query`, `Reader`, `Facade`, `Access Layer`, and `Application Layer`.
- [ ] The document locks vocabulary and naming intent tightly enough to prevent mixed terminology drift.
- [ ] The document forbids direct UI-driven authoritative gameplay mutation.
- [ ] The document preserves tick-result-based presentation separation.
- [ ] The document states that pause remains popup-owned and is not a screen taxonomy exception.
- [ ] The document states that Stage 9 diagnostics remain composition-only, read-only, and non-owning.
- [ ] The document anchors major rules to current repo architecture facts and current folder boundaries.
- [ ] The document includes folder and naming guidance concrete enough to drive implementation prompts.
- [ ] The document includes testing guidance for flow, blocking, and authoritative presentation behavior.
- [ ] The document includes anti-patterns and forbidden patterns.
- [ ] The document includes maintenance and update policy.
- [ ] The document is operational and implementation-facing rather than theoretical.
