using System;
using UnityEngine;

namespace Game.Feature.Stages
{
    public static class PlayerCaptureLaunchBootstrap
    {
        public const string CampaignTempSlotArgument = "--capture-campaign-temp-slot";
        public const string CampaignNormalSlotArgument = "--capture-campaign-normal-slot";
        public const string CampaignTempSlotChancesArgument =
            "--capture-campaign-temp-slot-chances";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void PrimeLaunchContextBeforeSceneLoad()
        {
            TryPrimeFromArguments(Environment.GetCommandLineArgs(), logErrors: true, out _);
        }

        public static bool TryPrimeFromArguments(
            string[] args,
            bool logErrors,
            out string error)
        {
            return TryPrimeFromArguments(
                args,
                logErrors,
                PlayerCaptureRuntimeEnvironment.Current,
                PlayerCapturePersistenceFactory.Instance,
                out error);
        }

        internal static bool TryPrimeFromArguments(
            string[] args,
            bool logErrors,
            PlayerCaptureRuntimeEnvironment environment,
            IPlayerCapturePersistenceFactory persistenceFactory,
            out string error)
        {
            if (persistenceFactory == null)
            {
                throw new ArgumentNullException(nameof(persistenceFactory));
            }

            if (!TryResolvePersistenceMode(args, out var persistenceMode, out error))
            {
                return Reject(error, logErrors);
            }

            if (!PlayerCaptureLaunchOptions.TryParse(args, out var options, out error))
            {
                return Reject(error, logErrors);
            }

            if (!options.HasCaptureStage)
            {
                if (persistenceMode != PlayerCapturePersistenceMode.None ||
                    CountArguments(args, CampaignTempSlotChancesArgument) > 0)
                {
                    error =
                        "Player capture persistence arguments require a valid --capture-stage value.";
                    return Reject(error, logErrors);
                }

                return true;
            }

            if (!TryResolveRemainingChances(args, persistenceMode, out var remainingChances, out error))
            {
                return Reject(error, logErrors);
            }

            if (persistenceMode == PlayerCapturePersistenceMode.NormalCampaignSlot)
            {
                var authorization = PlayerCapturePersistenceAuthorization.AuthorizeNormalSlot(
                    environment);
                if (!authorization.IsAuthorized)
                {
                    error =
                        $"Player capture normal-slot request rejected: {authorization.FailureReason}";
                    return Reject(error, logErrors);
                }
            }

            if (StageLaunchContextStore.TryPeek(out _))
            {
                error = "Player capture cannot replace an existing stage launch context.";
                return Reject(error, logErrors);
            }

            switch (persistenceMode)
            {
                case PlayerCapturePersistenceMode.NormalCampaignSlot:
                    return PrimeNormalCampaignCapture(
                        options.StageId,
                        persistenceFactory,
                        logErrors,
                        out error);

                case PlayerCapturePersistenceMode.CampaignTempSlot:
                    return PrimeTempCampaignCapture(
                        options.StageId,
                        remainingChances,
                        persistenceFactory,
                        logErrors,
                        out error);

                default:
                    return PrimeNonCampaignCapture(options.StageId, logErrors, out error);
            }
        }

        private static bool PrimeNormalCampaignCapture(
            StageId stageId,
            IPlayerCapturePersistenceFactory persistenceFactory,
            bool logErrors,
            out string error)
        {
            var request = new StageNavigationRequest(
                stageId,
                StageNavigationKind.Retry,
                "pause-retry");
            var launchContext = StageLaunchContext.CreatePendinglessReload(request);
            if (!StageLaunchContextStore.TrySetCurrent(launchContext))
            {
                error = "Player capture could not register the normal Campaign launch context.";
                return Reject(error, logErrors);
            }

            try
            {
                EditorDirectPlayContextStore.Clear();
                SeedFixture(
                    persistenceFactory.CreateNormalCampaignSlot(),
                    stageId,
                    "player-capture-bootstrap",
                    CampaignSaveSlotPolicy.DefaultRemainingChances);
            }
            catch
            {
                StageLaunchContextStore.TryClear(launchContext);
                throw;
            }

            error = string.Empty;
            Debug.Log(
                $"Player capture normal Campaign slot launch context primed with StageId " +
                $"'{stageId.Value}'.");
            return true;
        }

        private static bool PrimeTempCampaignCapture(
            StageId stageId,
            int remainingChances,
            IPlayerCapturePersistenceFactory persistenceFactory,
            bool logErrors,
            out string error)
        {
            var launchContext = StageLaunchContext.CreateDirectPlay(stageId);
            if (!StageLaunchContextStore.TrySetCurrent(launchContext))
            {
                error = "Player capture could not register the DirectPlay Campaign launch context.";
                return Reject(error, logErrors);
            }

            try
            {
                SeedFixture(
                    persistenceFactory.CreateTempCampaignSlot(),
                    stageId,
                    "level-01",
                    remainingChances);
                EditorDirectPlayContextStore.SetCurrent(
                    EditorDirectPlayContext.CreateCampaignTempSlot(stageId, remainingChances));
            }
            catch
            {
                StageLaunchContextStore.TryClear(launchContext);
                throw;
            }

            error = string.Empty;
            Debug.Log(
                $"Player capture campaign temp-slot launch context primed with StageId " +
                $"'{stageId.Value}' and remainingChances={remainingChances}.");
            return true;
        }

        private static bool PrimeNonCampaignCapture(
            StageId stageId,
            bool logErrors,
            out string error)
        {
            var launchContext = StageLaunchContext.CreateDirectPlay(stageId);
            if (!StageLaunchContextStore.TrySetCurrent(launchContext))
            {
                error = "Player capture could not register the non-Campaign launch context.";
                return Reject(error, logErrors);
            }

            EditorDirectPlayContextStore.SetCurrent(
                EditorDirectPlayContext.CreateNonCampaign(stageId));
            error = string.Empty;
            Debug.Log($"Player capture launch context primed with StageId '{stageId.Value}'.");
            return true;
        }

        private static void SeedFixture(
            IPlayerCaptureFixturePersistence persistence,
            StageId stageId,
            string levelGroupId,
            int remainingChances)
        {
            if (persistence == null)
            {
                throw new InvalidOperationException(
                    "Player capture persistence factory returned no fixture persistence.");
            }

            persistence.ClearAll();
            persistence.ClearActiveSlot();
            persistence.SaveSlot(new SaveSlotData
            {
                SlotNumber = 1,
                CurrentStageId = stageId,
                CurrentLevelGroupId = levelGroupId,
                RemainingChances = remainingChances,
                LastPlayedAt = DateTimeOffset.UtcNow.ToString("O"),
            });
            persistence.SetActiveSlot(1);
        }

        private static bool TryResolvePersistenceMode(
            string[] args,
            out PlayerCapturePersistenceMode mode,
            out string error)
        {
            mode = PlayerCapturePersistenceMode.None;
            error = string.Empty;

            var normalCount = CountArguments(args, CampaignNormalSlotArgument);
            var tempCount = CountArguments(args, CampaignTempSlotArgument);
            if (normalCount > 1 || tempCount > 1)
            {
                error = "Player capture persistence mode arguments cannot be repeated.";
                return false;
            }

            if (normalCount > 0 && tempCount > 0)
            {
                error =
                    "Player capture cannot request normal and DirectPlay Campaign slots together.";
                return false;
            }

            if (normalCount == 1)
            {
                mode = PlayerCapturePersistenceMode.NormalCampaignSlot;
            }
            else if (tempCount == 1)
            {
                mode = PlayerCapturePersistenceMode.CampaignTempSlot;
            }

            return true;
        }

        private static bool TryResolveRemainingChances(
            string[] args,
            PlayerCapturePersistenceMode mode,
            out int remainingChances,
            out string error)
        {
            remainingChances = 2;
            error = string.Empty;

            var chanceArgumentCount = CountArguments(args, CampaignTempSlotChancesArgument);
            if (chanceArgumentCount == 0)
            {
                return true;
            }

            if (chanceArgumentCount > 1)
            {
                error = $"{CampaignTempSlotChancesArgument} cannot be repeated.";
                return false;
            }

            if (mode != PlayerCapturePersistenceMode.CampaignTempSlot)
            {
                error =
                    $"{CampaignTempSlotChancesArgument} requires {CampaignTempSlotArgument}.";
                return false;
            }

            for (var i = 0; i < args.Length; i++)
            {
                if (!string.Equals(
                        args[i],
                        CampaignTempSlotChancesArgument,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                if (i + 1 >= args.Length ||
                    !int.TryParse(args[i + 1], out remainingChances) ||
                    remainingChances < 1 ||
                    remainingChances > CampaignSaveSlotPolicy.DefaultRemainingChances)
                {
                    error =
                        $"{CampaignTempSlotChancesArgument} must be between 1 and " +
                        $"{CampaignSaveSlotPolicy.DefaultRemainingChances}.";
                    return false;
                }

                return true;
            }

            return true;
        }

        private static int CountArguments(string[] args, string expected)
        {
            if (args == null)
            {
                return 0;
            }

            var count = 0;
            for (var i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], expected, StringComparison.Ordinal))
                {
                    count++;
                }
            }

            return count;
        }

        private static bool Reject(string error, bool logErrors)
        {
            if (logErrors)
            {
                Debug.LogError(error);
            }

            return false;
        }
    }

    internal enum PlayerCapturePersistenceMode
    {
        None = 0,
        NormalCampaignSlot = 1,
        CampaignTempSlot = 2,
    }

    internal readonly struct PlayerCaptureRuntimeEnvironment
    {
        public PlayerCaptureRuntimeEnvironment(
            bool isDevelopmentBuild,
            bool hasCaptureBuildCapability,
            string productName,
            string persistentDataPath)
        {
            IsDevelopmentBuild = isDevelopmentBuild;
            HasCaptureBuildCapability = hasCaptureBuildCapability;
            ProductName = productName ?? string.Empty;
            PersistentDataPath = persistentDataPath ?? string.Empty;
        }

        public bool IsDevelopmentBuild { get; }

        public bool HasCaptureBuildCapability { get; }

        public string ProductName { get; }

        public string PersistentDataPath { get; }

        public static PlayerCaptureRuntimeEnvironment Current =>
            new(
                Debug.isDebugBuild,
                PlayerCapturePersistenceAuthorization.HasBuildCapability,
                Application.productName,
                Application.persistentDataPath);
    }

    internal readonly struct PlayerCaptureAuthorizationResult
    {
        private PlayerCaptureAuthorizationResult(bool isAuthorized, string failureReason)
        {
            IsAuthorized = isAuthorized;
            FailureReason = failureReason ?? string.Empty;
        }

        public bool IsAuthorized { get; }

        public string FailureReason { get; }

        public static PlayerCaptureAuthorizationResult Authorized()
        {
            return new PlayerCaptureAuthorizationResult(true, string.Empty);
        }

        public static PlayerCaptureAuthorizationResult Rejected(string reason)
        {
            return new PlayerCaptureAuthorizationResult(false, reason);
        }
    }

    internal static class PlayerCapturePersistenceAuthorization
    {
        internal const string BuildCapabilityDefine = "VECTORQUAKE_CAPTURE_BUILD";
        internal const string IsolatedProductNamePrefix = "VectorQuake-P0Phase4Smoke-";

        internal static bool HasBuildCapability
        {
            get
            {
#if VECTORQUAKE_CAPTURE_BUILD
                return true;
#else
                return false;
#endif
            }
        }

        internal static PlayerCaptureAuthorizationResult AuthorizeNormalSlot(
            PlayerCaptureRuntimeEnvironment environment)
        {
            if (!environment.IsDevelopmentBuild)
            {
                return PlayerCaptureAuthorizationResult.Rejected(
                    "not a development build");
            }

            if (!environment.HasCaptureBuildCapability)
            {
                return PlayerCaptureAuthorizationResult.Rejected(
                    "capture build capability is absent");
            }

            if (!HasIsolatedPersistenceIdentity(
                    environment.ProductName,
                    environment.PersistentDataPath))
            {
                return PlayerCaptureAuthorizationResult.Rejected(
                    "persistence identity is not isolated");
            }

            return PlayerCaptureAuthorizationResult.Authorized();
        }

        internal static bool HasIsolatedPersistenceIdentity(
            string productName,
            string persistentDataPath)
        {
            if (string.IsNullOrWhiteSpace(productName) ||
                productName.Length <= IsolatedProductNamePrefix.Length ||
                !productName.StartsWith(
                    IsolatedProductNamePrefix,
                    StringComparison.Ordinal))
            {
                return false;
            }

            var normalizedPath = (persistentDataPath ?? string.Empty)
                .Replace('\\', '/')
                .TrimEnd('/');
            if (normalizedPath.Length == 0)
            {
                return false;
            }

            var lastSeparator = normalizedPath.LastIndexOf('/');
            var leaf = lastSeparator >= 0
                ? normalizedPath.Substring(lastSeparator + 1)
                : normalizedPath;
            return string.Equals(leaf, productName, StringComparison.OrdinalIgnoreCase);
        }
    }

    internal interface IPlayerCapturePersistenceFactory
    {
        IPlayerCaptureFixturePersistence CreateNormalCampaignSlot();

        IPlayerCaptureFixturePersistence CreateTempCampaignSlot();
    }

    internal interface IPlayerCaptureFixturePersistence
    {
        void ClearAll();

        void ClearActiveSlot();

        void SaveSlot(SaveSlotData slot);

        void SetActiveSlot(int slotNumber);
    }

    internal sealed class PlayerCapturePersistenceFactory : IPlayerCapturePersistenceFactory
    {
        internal static readonly PlayerCapturePersistenceFactory Instance = new();

        private PlayerCapturePersistenceFactory()
        {
        }

        public IPlayerCaptureFixturePersistence CreateNormalCampaignSlot()
        {
            var saveStore = CampaignSaveCompositionProvider.CreateProductionProfileBacked();
            var activeSlot = CampaignSaveCompositionProvider.CreateProductionActiveSlotProvider(
                saveStore);
            return new PlayerCaptureFixturePersistence(saveStore, activeSlot);
        }

        public IPlayerCaptureFixturePersistence CreateTempCampaignSlot()
        {
            var saveStore = CampaignSaveCompositionProvider.CreateTemporaryProfileBacked();
            var activeSlot = CampaignSaveCompositionProvider.CreateTemporaryActiveSlotProvider(
                saveStore);
            return new PlayerCaptureFixturePersistence(saveStore, activeSlot);
        }
    }

    internal sealed class PlayerCaptureFixturePersistence : IPlayerCaptureFixturePersistence
    {
        private readonly ICampaignSaveSlotStore saveStore;
        private readonly ActiveSlotProvider activeSlot;

        internal PlayerCaptureFixturePersistence(
            ICampaignSaveSlotStore saveStore,
            ActiveSlotProvider activeSlot)
        {
            this.saveStore = saveStore ?? throw new ArgumentNullException(nameof(saveStore));
            this.activeSlot = activeSlot ?? throw new ArgumentNullException(nameof(activeSlot));
        }

        public void ClearAll()
        {
            saveStore.ClearAll();
        }

        public void ClearActiveSlot()
        {
            activeSlot.ClearActiveSlot();
        }

        public void SaveSlot(SaveSlotData slot)
        {
            saveStore.SaveSlot(slot);
        }

        public void SetActiveSlot(int slotNumber)
        {
            activeSlot.SetActiveSlot(slotNumber);
        }
    }
}
