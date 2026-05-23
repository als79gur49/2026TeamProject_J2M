using System;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

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
            UnityEngine.Object context = null,
            bool includeStackTrace = false)
        {
            UnityEngine.Debug.Log(
                $"{Prefix} {TimingFields()} method={method} reason={reason} {details ?? string.Empty}" +
                (includeStackTrace ? $"\n{Environment.StackTrace}" : string.Empty),
                context);
        }

        public static string TimingFields()
        {
            return $"frame={Time.frameCount} time={Time.time:F3} unscaled={Time.unscaledTime:F3} realtime={Time.realtimeSinceStartup:F3} scene={SceneManager.GetActiveScene().name}";
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

        public static string DescribeGameObject(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return "gameObject=<null>";
            }

            var parent = gameObject.transform.parent;
            return $"gameObjectPath={GetPath(gameObject.transform)} instanceId={gameObject.GetInstanceID()} activeSelf={gameObject.activeSelf} activeInHierarchy={gameObject.activeInHierarchy} parentPath={GetPath(parent)} localScale={gameObject.transform.localScale}";
        }

        public static string GetPath(Transform transform)
        {
            if (transform == null)
            {
                return "<null>";
            }

            var builder = new StringBuilder(transform.name);
            var current = transform.parent;
            while (current != null)
            {
                builder.Insert(0, current.name + "/");
                current = current.parent;
            }

            return builder.ToString();
        }

        public static string DescribeUnityObject(UnityEngine.Object value)
        {
            return value == null
                ? "<null>"
                : $"{value.name}#{value.GetInstanceID()}";
        }

        public static string DescribeObject(object value)
        {
            if (value == null)
            {
                return "<null>";
            }

            return value is UnityEngine.Object unityObject
                ? DescribeUnityObject(unityObject)
                : $"{value.GetType().Name}#{value.GetHashCode()}";
        }

        public static string DescribeParticles(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return "particleSystems=<null>";
            }

            var systems = gameObject.GetComponentsInChildren<ParticleSystem>(includeInactive: true);
            var renderers = gameObject.GetComponentsInChildren<Renderer>(includeInactive: true);
            var builder = new StringBuilder();
            builder.Append("particleSystemCount=").Append(systems.Length);
            for (var i = 0; i < systems.Length; i++)
            {
                var system = systems[i];
                if (system == null)
                {
                    continue;
                }

                var main = system.main;
                builder
                    .Append(" | ps=").Append(GetPath(system.transform))
                    .Append(" duration=").Append(main.duration.ToString("F3"))
                    .Append(" startLifetime=").Append(DescribeCurve(main.startLifetime))
                    .Append(" loop=").Append(main.loop)
                    .Append(" playOnAwake=").Append(main.playOnAwake)
                    .Append(" isPlaying=").Append(system.isPlaying)
                    .Append(" isEmitting=").Append(system.isEmitting)
                    .Append(" isStopped=").Append(system.isStopped)
                    .Append(" psTime=").Append(system.time.ToString("F3"))
                    .Append(" stopAction=").Append(main.stopAction);
            }

            builder.Append(" rendererCount=").Append(renderers.Length);
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                builder
                    .Append(" | renderer=").Append(GetPath(renderer.transform))
                    .Append(" enabled=").Append(renderer.enabled)
                    .Append(" materialValid=").Append(renderer.sharedMaterial != null)
                    .Append(" bounds=").Append(renderer.bounds);
            }

            return builder.ToString();
        }

        private static string DescribeCurve(ParticleSystem.MinMaxCurve curve)
        {
            return curve.mode switch
            {
                ParticleSystemCurveMode.Constant => curve.constant.ToString("F3"),
                ParticleSystemCurveMode.TwoConstants => $"{curve.constantMin:F3}-{curve.constantMax:F3}",
                _ => curve.mode.ToString()
            };
        }
    }
}
