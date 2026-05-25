using System;
using System.Text;
using UnityEngine;

namespace Game.Feature.Gameplay.Vfx.Host
{
    public static class GameplayVfxLifetimeUnityTrace
    {
        public const string Prefix = GameplayVfxLifetimeTrace.Prefix;

        public static GameplayVfxCueId EntranceSpawnCue => GameplayVfxLifetimeTrace.EntranceSpawnCue;

        public static bool IsEntranceSpawn(GameplayVfxCueId cueId)
        {
            return GameplayVfxLifetimeTrace.IsEntranceSpawn(cueId);
        }

        public static void Log(
            string method,
            string reason,
            string details = null,
            UnityEngine.Object context = null,
            bool includeStackTrace = false)
        {
            UnityEngine.Debug.Log(
                $"{Prefix} {GameplayVfxLifetimeTrace.TimingFields()} method={method} reason={reason} {details ?? string.Empty}" +
                (includeStackTrace ? $"\n{Environment.StackTrace}" : string.Empty),
                context);
        }

        public static string DescribeRequest(in GameplayVfxRequest request)
        {
            return GameplayVfxLifetimeTrace.DescribeRequest(request);
        }

        public static string DescribePolicy(in VfxBindingRuntimePolicy policy)
        {
            return GameplayVfxLifetimeTrace.DescribePolicy(policy);
        }

        public static string DescribeCueName(GameplayVfxCueId cueId)
        {
            return GameplayVfxLifetimeTrace.DescribeCueName(cueId);
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
