using System;
using System.Collections.Generic;

namespace Game.Feature.Stages
{
    public enum SceneTransitionIntent
    {
        Unknown = 0,
        StageAdvance = 1,
        DeathRetry = 2,
        ManualRetry = 3,
        GameplayEntry = 4,
        ReturnToMainMenu = 5,
        ComicIntroToGameplay = 6,
        ComicOutroToMainMenu = 7,
        DemoStageRelaunch = 8,
        EditorDirectSceneLoad = 100,
        TestInjectedSceneLoad = 101,
    }

    public enum SceneTransitionDestinationKind
    {
        Unknown = 0,
        Gameplay = 1,
        MainMenu = 2,
        ComicSequence = 3,
    }

    public enum SceneTransitionRouteClassification
    {
        Unknown = 0,
        Production = 1,
        EditorOnly = 2,
        TestOnly = 3,
    }

    public enum SceneTransitionRouteStatus
    {
        Unknown = 0,
        Canonical = 1,
        EditorOnlyException = 3,
        TestOnlyException = 4,
    }

    public enum SceneTransitionDestinationExecutorKind
    {
        Unknown = 0,
        GameplayEntry = 1,
        MainMenuEntry = 2,
    }

    public readonly struct SceneTransitionRoutePolicy
    {
        internal SceneTransitionRoutePolicy(
            SceneTransitionIntent intent,
            SceneTransitionDestinationKind destinationKind,
            SceneTransitionDestinationExecutorKind destinationExecutorKind,
            SceneTransitionRouteClassification classification,
            SceneTransitionRouteStatus status,
            StageTransitionKind primaryTransitionKind,
            StageTransitionKind secondaryTransitionKind,
            string owner,
            string currentExecution,
            string migrationMilestone,
            bool targetRequiresSceneTransitionSession,
            bool implementsSceneTransitionSession,
            bool targetRequiresDestinationReadiness,
            bool implementsDestinationReadiness,
            bool targetRequiresDestinationRenderAcknowledgement,
            bool implementsDestinationRenderAcknowledgement,
            bool targetRequiresInputAdmission,
            bool implementsInputAdmission,
            bool targetRequiresOpaqueOwnerTransfer,
            bool implementsOpaqueOwnerTransfer)
        {
            Intent = intent;
            DestinationKind = destinationKind;
            DestinationExecutorKind = destinationExecutorKind;
            Classification = classification;
            Status = status;
            PrimaryTransitionKind = primaryTransitionKind;
            SecondaryTransitionKind = secondaryTransitionKind;
            Owner = owner ?? string.Empty;
            CurrentExecution = currentExecution ?? string.Empty;
            MigrationMilestone = migrationMilestone ?? string.Empty;
            TargetRequiresSceneTransitionSession = targetRequiresSceneTransitionSession;
            ImplementsSceneTransitionSession = implementsSceneTransitionSession;
            TargetRequiresDestinationReadiness = targetRequiresDestinationReadiness;
            ImplementsDestinationReadiness = implementsDestinationReadiness;
            TargetRequiresDestinationRenderAcknowledgement = targetRequiresDestinationRenderAcknowledgement;
            ImplementsDestinationRenderAcknowledgement = implementsDestinationRenderAcknowledgement;
            TargetRequiresInputAdmission = targetRequiresInputAdmission;
            ImplementsInputAdmission = implementsInputAdmission;
            TargetRequiresOpaqueOwnerTransfer = targetRequiresOpaqueOwnerTransfer;
            ImplementsOpaqueOwnerTransfer = implementsOpaqueOwnerTransfer;
        }

        public SceneTransitionIntent Intent { get; }

        public SceneTransitionDestinationKind DestinationKind { get; }

        public SceneTransitionDestinationExecutorKind DestinationExecutorKind { get; }

        public SceneTransitionRouteClassification Classification { get; }

        public SceneTransitionRouteStatus Status { get; }

        public StageTransitionKind PrimaryTransitionKind { get; }

        public StageTransitionKind SecondaryTransitionKind { get; }

        public string Owner { get; }

        public string CurrentExecution { get; }

        public string MigrationMilestone { get; }

        public bool TargetRequiresSceneTransitionSession { get; }

        public bool ImplementsSceneTransitionSession { get; }

        public bool TargetRequiresDestinationReadiness { get; }

        public bool ImplementsDestinationReadiness { get; }

        public bool TargetRequiresDestinationRenderAcknowledgement { get; }

        public bool ImplementsDestinationRenderAcknowledgement { get; }

        public bool TargetRequiresInputAdmission { get; }

        public bool ImplementsInputAdmission { get; }

        public bool TargetRequiresOpaqueOwnerTransfer { get; }

        public bool ImplementsOpaqueOwnerTransfer { get; }

        public bool AllowsTransitionKind(StageTransitionKind kind)
        {
            return kind != StageTransitionKind.Unknown &&
                   (kind == PrimaryTransitionKind || kind == SecondaryTransitionKind);
        }
    }

    public static class SceneTransitionRoutePolicyCatalog
    {
        private static readonly SceneTransitionRoutePolicy[] Policies =
        {
            Production(
                SceneTransitionIntent.StageAdvance,
                SceneTransitionDestinationKind.Gameplay,
                SceneTransitionRouteStatus.Canonical,
                StageTransitionKind.StageClearNext,
                owner: "StageResult / StageResultAutoNextDriver",
                currentExecution: "Persistent blue cover -> gameplay readiness -> rendered closed Entry Iris -> reveal",
                migrationMilestone: "Maintain",
                implementsSession: true,
                implementsReadiness: true,
                implementsRenderAcknowledgement: true,
                implementsInputAdmission: true),
            Production(
                SceneTransitionIntent.DeathRetry,
                SceneTransitionDestinationKind.Gameplay,
                SceneTransitionRouteStatus.Canonical,
                StageTransitionKind.DeathRetryChanceLost,
                owner: "CampaignGameplayFlowController",
                currentExecution: "Defeat Iris -> persistent black hold -> strong gameplay readiness -> rendered black Entry Iris -> opening",
                migrationMilestone: "M2 implemented",
                implementsSession: true,
                implementsReadiness: true,
                implementsRenderAcknowledgement: true,
                implementsInputAdmission: true),
            Production(
                SceneTransitionIntent.ManualRetry,
                SceneTransitionDestinationKind.Gameplay,
                SceneTransitionRouteStatus.Canonical,
                StageTransitionKind.StageRetryManual,
                owner: "StageResult / LevelFailed / Pause",
                currentExecution: "Retry Iris -> persistent retry hold -> strong gameplay readiness -> rendered Entry Iris -> opening",
                migrationMilestone: "M2 implemented",
                implementsSession: true,
                implementsReadiness: true,
                implementsRenderAcknowledgement: true,
                implementsInputAdmission: true,
                secondaryTransitionKind: StageTransitionKind.LevelFailedRestart),
            Production(
                SceneTransitionIntent.GameplayEntry,
                SceneTransitionDestinationKind.Gameplay,
                SceneTransitionRouteStatus.Canonical,
                StageTransitionKind.MainToGameplay,
                owner: "MainMenuController / MainMenuUiFlowInstaller",
                currentExecution: "Main Menu blue Iris -> persistent blue hold -> strong gameplay readiness -> rendered player Entry Iris -> opening",
                migrationMilestone: "M3 complete",
                implementsSession: true,
                implementsReadiness: true,
                implementsRenderAcknowledgement: true,
                implementsInputAdmission: true),
            Production(
                SceneTransitionIntent.ReturnToMainMenu,
                SceneTransitionDestinationKind.MainMenu,
                SceneTransitionRouteStatus.Canonical,
                StageTransitionKind.GameplayToMain,
                owner: "UIFlowCoordinator / MainMenuUiFlowInstaller",
                currentExecution: "Screen-center neutral Iris -> persistent neutral hold -> Main Menu readiness -> rendered closed menu Iris -> opening",
                migrationMilestone: "M4 complete",
                implementsSession: true,
                implementsReadiness: true,
                implementsRenderAcknowledgement: true,
                implementsInputAdmission: true),
            Production(
                SceneTransitionIntent.ComicIntroToGameplay,
                SceneTransitionDestinationKind.Gameplay,
                SceneTransitionRouteStatus.Canonical,
                StageTransitionKind.MainToGameplay,
                owner: "ComicIntroStageLaunchRouter / GameplayUiFlowInstaller",
                currentExecution: "Rendered comic sequence black -> rendered persistent black -> gameplay readiness -> rendered black Entry Iris -> opening",
                migrationMilestone: "M4 complete",
                implementsSession: true,
                implementsReadiness: true,
                implementsRenderAcknowledgement: true,
                implementsInputAdmission: true,
                requiresOpaqueOwnerTransfer: true,
                implementsOpaqueOwnerTransfer: true),
            Production(
                SceneTransitionIntent.ComicOutroToMainMenu,
                SceneTransitionDestinationKind.MainMenu,
                SceneTransitionRouteStatus.Canonical,
                StageTransitionKind.GameplayToMain,
                owner: "ComicOutroMainMenuReturnRouter / MainMenuUiFlowInstaller",
                currentExecution: "Rendered comic sequence black -> rendered persistent black -> Main Menu readiness -> rendered closed menu Iris -> opening",
                migrationMilestone: "M4 complete",
                implementsSession: true,
                implementsReadiness: true,
                implementsRenderAcknowledgement: true,
                implementsInputAdmission: true,
                requiresOpaqueOwnerTransfer: true,
                implementsOpaqueOwnerTransfer: true),
            Production(
                SceneTransitionIntent.DemoStageRelaunch,
                SceneTransitionDestinationKind.Gameplay,
                SceneTransitionRouteStatus.Canonical,
                StageTransitionKind.StageRetryManual,
                owner: "DemoStageControlLaunchBridge",
                currentExecution: "Retry Iris -> shared gameplay-entry hold/readiness/render/opening executor",
                migrationMilestone: "M2 shared seam implemented",
                implementsSession: true,
                implementsReadiness: true,
                implementsRenderAcknowledgement: true,
                implementsInputAdmission: true),
            Exception(
                SceneTransitionIntent.EditorDirectSceneLoad,
                SceneTransitionRouteClassification.EditorOnly,
                SceneTransitionRouteStatus.EditorOnlyException,
                "Configured routers in non-playing editor paths",
                "Editor direct-play scene load without production visual lifecycle"),
            Exception(
                SceneTransitionIntent.TestInjectedSceneLoad,
                SceneTransitionRouteClassification.TestOnly,
                SceneTransitionRouteStatus.TestOnlyException,
                "Injected ISceneLoadPort and SceneNameStageLaunchRouter",
                "Test fake/callback scene load without production visual lifecycle"),
        };

        private static readonly Dictionary<SceneTransitionIntent, SceneTransitionRoutePolicy> ByIntent =
            BuildIndex();

        public static IReadOnlyList<SceneTransitionRoutePolicy> All { get; } =
            Array.AsReadOnly(Policies);

        public static SceneTransitionRoutePolicy ResolveProduction(SceneTransitionIntent intent)
        {
            if (intent == SceneTransitionIntent.Unknown || !ByIntent.TryGetValue(intent, out var policy))
            {
                throw new InvalidOperationException(
                    $"Unregistered production scene transition intent '{intent}' has no canonical executor.");
            }

            if (policy.Classification != SceneTransitionRouteClassification.Production)
            {
                throw new InvalidOperationException(
                    $"Scene transition intent '{intent}' is classified as {policy.Classification} and cannot execute as production.");
            }

            return policy;
        }

        public static SceneTransitionRoutePolicy ResolveException(
            SceneTransitionIntent intent,
            SceneTransitionRouteClassification classification)
        {
            if (!ByIntent.TryGetValue(intent, out var policy) ||
                classification == SceneTransitionRouteClassification.Production ||
                classification == SceneTransitionRouteClassification.Unknown ||
                policy.Classification != classification)
            {
                throw new InvalidOperationException(
                    $"Scene transition exception '{intent}' is not registered for classification '{classification}'.");
            }

            return policy;
        }

        public static SceneTransitionRoutePolicy RequireDestination(
            SceneTransitionRoutePolicy policy,
            SceneTransitionDestinationKind destinationKind)
        {
            if (policy.DestinationKind != destinationKind)
            {
                throw new InvalidOperationException(
                    $"Scene transition intent '{policy.Intent}' targets {policy.DestinationKind}, not required destination {destinationKind}.");
            }

            return policy;
        }

        private static SceneTransitionRoutePolicy Production(
            SceneTransitionIntent intent,
            SceneTransitionDestinationKind destinationKind,
            SceneTransitionRouteStatus status,
            StageTransitionKind primaryTransitionKind,
            string owner,
            string currentExecution,
            string migrationMilestone,
            bool implementsSession,
            bool implementsReadiness,
            bool implementsRenderAcknowledgement,
            bool implementsInputAdmission,
            StageTransitionKind secondaryTransitionKind = StageTransitionKind.Unknown,
            bool requiresOpaqueOwnerTransfer = false,
            bool implementsOpaqueOwnerTransfer = false)
        {
            return new SceneTransitionRoutePolicy(
                intent,
                destinationKind,
                destinationKind == SceneTransitionDestinationKind.Gameplay
                    ? SceneTransitionDestinationExecutorKind.GameplayEntry
                    : SceneTransitionDestinationExecutorKind.MainMenuEntry,
                SceneTransitionRouteClassification.Production,
                status,
                primaryTransitionKind,
                secondaryTransitionKind,
                owner,
                currentExecution,
                migrationMilestone,
                targetRequiresSceneTransitionSession: true,
                implementsSceneTransitionSession: implementsSession,
                targetRequiresDestinationReadiness: true,
                implementsDestinationReadiness: implementsReadiness,
                targetRequiresDestinationRenderAcknowledgement: true,
                implementsDestinationRenderAcknowledgement: implementsRenderAcknowledgement,
                targetRequiresInputAdmission: true,
                implementsInputAdmission: implementsInputAdmission,
                targetRequiresOpaqueOwnerTransfer: requiresOpaqueOwnerTransfer,
                implementsOpaqueOwnerTransfer: implementsOpaqueOwnerTransfer);
        }

        private static SceneTransitionRoutePolicy Exception(
            SceneTransitionIntent intent,
            SceneTransitionRouteClassification classification,
            SceneTransitionRouteStatus status,
            string owner,
            string currentExecution)
        {
            return new SceneTransitionRoutePolicy(
                intent,
                SceneTransitionDestinationKind.Unknown,
                SceneTransitionDestinationExecutorKind.Unknown,
                classification,
                status,
                StageTransitionKind.Unknown,
                StageTransitionKind.Unknown,
                owner,
                currentExecution,
                "Explicit exception",
                targetRequiresSceneTransitionSession: false,
                implementsSceneTransitionSession: false,
                targetRequiresDestinationReadiness: false,
                implementsDestinationReadiness: false,
                targetRequiresDestinationRenderAcknowledgement: false,
                implementsDestinationRenderAcknowledgement: false,
                targetRequiresInputAdmission: false,
                implementsInputAdmission: false,
                targetRequiresOpaqueOwnerTransfer: false,
                implementsOpaqueOwnerTransfer: false);
        }

        private static Dictionary<SceneTransitionIntent, SceneTransitionRoutePolicy> BuildIndex()
        {
            var result = new Dictionary<SceneTransitionIntent, SceneTransitionRoutePolicy>();
            for (var i = 0; i < Policies.Length; i++)
            {
                var policy = Policies[i];
                Validate(policy);
                if (result.ContainsKey(policy.Intent))
                {
                    throw new InvalidOperationException(
                        $"Duplicate scene transition route policy intent '{policy.Intent}'.");
                }

                result.Add(policy.Intent, policy);
            }

            return result;
        }

        private static void Validate(SceneTransitionRoutePolicy policy)
        {
            if (policy.Intent == SceneTransitionIntent.Unknown ||
                policy.Classification == SceneTransitionRouteClassification.Unknown ||
                policy.Status == SceneTransitionRouteStatus.Unknown ||
                string.IsNullOrWhiteSpace(policy.Owner) ||
                string.IsNullOrWhiteSpace(policy.CurrentExecution) ||
                string.IsNullOrWhiteSpace(policy.MigrationMilestone))
            {
                throw new InvalidOperationException(
                    $"Scene transition route policy '{policy.Intent}' has incomplete required metadata.");
            }

            if (policy.Classification == SceneTransitionRouteClassification.Production)
            {
                if (policy.DestinationKind == SceneTransitionDestinationKind.Unknown ||
                    policy.DestinationExecutorKind ==
                    SceneTransitionDestinationExecutorKind.Unknown ||
                    policy.PrimaryTransitionKind == StageTransitionKind.Unknown ||
                    policy.Status != SceneTransitionRouteStatus.Canonical)
                {
                    throw new InvalidOperationException(
                        $"Production scene transition route policy '{policy.Intent}' is incomplete.");
                }

                if (policy.TargetRequiresOpaqueOwnerTransfer !=
                    policy.ImplementsOpaqueOwnerTransfer)
                {
                    throw new InvalidOperationException(
                        $"Scene transition route policy '{policy.Intent}' has incomplete opaque-owner transfer metadata.");
                }

                var expectedExecutor =
                    policy.DestinationKind == SceneTransitionDestinationKind.Gameplay
                        ? SceneTransitionDestinationExecutorKind.GameplayEntry
                        : SceneTransitionDestinationExecutorKind.MainMenuEntry;
                if (policy.DestinationExecutorKind != expectedExecutor)
                {
                    throw new InvalidOperationException(
                        $"Scene transition route policy '{policy.Intent}' maps destination " +
                        $"{policy.DestinationKind} to executor {policy.DestinationExecutorKind}, " +
                        $"expected {expectedExecutor}.");
                }

                return;
            }

            var expectedStatus = policy.Classification == SceneTransitionRouteClassification.EditorOnly
                ? SceneTransitionRouteStatus.EditorOnlyException
                : SceneTransitionRouteStatus.TestOnlyException;
            if (policy.Status != expectedStatus)
            {
                throw new InvalidOperationException(
                    $"Scene transition exception '{policy.Intent}' has status {policy.Status}, expected {expectedStatus}.");
            }
        }
    }

    public static class SceneTransitionRouteDiagnostic
    {
        public static string Format(
            SceneTransitionRoutePolicy policy,
            string source,
            StageTransitionKind transitionProfile)
        {
            return
                $"SceneTransitionRoute|Intent={policy.Intent}|Destination={policy.DestinationKind}|" +
                $"Status={policy.Status}|Source={Normalize(source)}|Profile={transitionProfile}|" +
                $"Migration={policy.MigrationMilestone}|Owner={policy.Owner}";
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "<missing>" : value.Trim();
        }
    }
}
