using System;

namespace Game.Feature.Gameplay.Vfx
{
    public static class GameplayVfxLifetimeTrace
    {
        public const string Prefix = "[VFX_LIFETIME_TRACE]";

        public static GameplayVfxCueId EntranceSpawnCue { get; } =
            GameplayVfxCueId.From(TileFeatureVfxCue.EntranceSpawn);

        public static bool IsEntranceSpawn(GameplayVfxCueId cueId)
        {
            return cueId == EntranceSpawnCue;
        }

        public static void Log(
            string method,
            string reason,
            string details = null,
            bool includeStackTrace = false)
        {
            UnityEngine.Debug.Log(
                $"{Prefix} {TimingFields()} method={method} reason={reason} {details ?? string.Empty}" +
                (includeStackTrace ? $"\n{Environment.StackTrace}" : string.Empty));
        }

        public static string TimingFields()
        {
            return $"frame={UnityEngine.Time.frameCount} time={UnityEngine.Time.time:F3} unscaled={UnityEngine.Time.unscaledTime:F3} realtime={UnityEngine.Time.realtimeSinceStartup:F3} scene={UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}";
        }

        public static string DescribeRequest(in GameplayVfxRequest request)
        {
            return $"cueFamily={request.CueId.Family} cueCode={request.CueId.Code} cueName={DescribeCueName(request.CueId)} tick={request.TickIndex} sequence={request.SequenceId} seed={request.PresentationSeed} sourceEntity={request.SourceEntityId} requestKey={request.TickIndex}:{request.SequenceId}:{request.SourceEntityId}:{request.CueId.Family}:{request.CueId.Code} isPersistent={request.IsPersistent} timing={request.Timing} anchorKind={request.Anchor.Kind} anchorSlot={request.Anchor.Slot} anchorCell={request.Anchor.Cell} delay={request.DelaySeconds:F3} topologyStop={request.TopologyStopMode} topologySpawn={request.TopologySpawnMode}";
        }

        public static string DescribePolicy(in VfxBindingRuntimePolicy policy)
        {
            return $"stopPolicy={policy.StopPolicy} defaultLifetimeSeconds={policy.DefaultLifetimeSeconds:F3} tailSeconds={policy.TailSeconds:F3} effectiveLifetimeSeconds={(policy.DefaultLifetimeSeconds + policy.TailSeconds):F3} maxConcurrentInstances={policy.MaxConcurrentInstances} playbackMode={policy.PlaybackMode} visibilityMode={policy.VisibilityMode}";
        }

        public static string DescribeCueName(GameplayVfxCueId cueId)
        {
            if (cueId.Family == GameplayVfxFamily.TileFeature &&
                Enum.IsDefined(typeof(TileFeatureVfxCue), cueId.Code))
            {
                return ((TileFeatureVfxCue)cueId.Code).ToString();
            }

            return cueId.ToString();
        }
    }
}
