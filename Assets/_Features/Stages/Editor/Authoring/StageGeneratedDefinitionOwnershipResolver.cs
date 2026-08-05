using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace Game.Feature.Stages.Editor
{
    internal enum StageDefinitionOwnershipKind
    {
        GeneratedOwned,
        Standalone,
        Ambiguous,
    }

    internal readonly struct StageGeneratedDefinitionOwner
    {
        public StageGeneratedDefinitionOwner(
            StageAuthoringDefinition authoring,
            string assetPath,
            string assetGuid)
        {
            Authoring = authoring;
            AssetPath = assetPath ?? string.Empty;
            AssetGuid = assetGuid ?? string.Empty;
        }

        public StageAuthoringDefinition Authoring { get; }

        public string AssetPath { get; }

        public string AssetGuid { get; }
    }

    internal sealed class StageGeneratedDefinitionOwnership
    {
        public StageGeneratedDefinitionOwnership(
            StageDefinition target,
            string targetPath,
            string targetGuid,
            IReadOnlyList<StageGeneratedDefinitionOwner> owners)
        {
            Target = target;
            TargetPath = targetPath ?? string.Empty;
            TargetGuid = targetGuid ?? string.Empty;
            Owners = owners ?? Array.Empty<StageGeneratedDefinitionOwner>();
            Kind = Owners.Count switch
            {
                0 => StageDefinitionOwnershipKind.Standalone,
                1 => StageDefinitionOwnershipKind.GeneratedOwned,
                _ => StageDefinitionOwnershipKind.Ambiguous,
            };
        }

        public StageDefinition Target { get; }

        public string TargetPath { get; }

        public string TargetGuid { get; }

        public StageDefinitionOwnershipKind Kind { get; }

        public IReadOnlyList<StageGeneratedDefinitionOwner> Owners { get; }

        public StageAuthoringDefinition Owner =>
            Kind == StageDefinitionOwnershipKind.GeneratedOwned ? Owners[0].Authoring : null;
    }

    internal static class StageGeneratedDefinitionOwnershipResolver
    {
        public static StageGeneratedDefinitionOwnership Resolve(StageDefinition target)
        {
            var targetPath = target != null ? AssetDatabase.GetAssetPath(target) : string.Empty;
            var targetGuid = string.IsNullOrEmpty(targetPath)
                ? string.Empty
                : AssetDatabase.AssetPathToGUID(targetPath);
            if (target == null)
            {
                return new StageGeneratedDefinitionOwnership(
                    null,
                    targetPath,
                    targetGuid,
                    Array.Empty<StageGeneratedDefinitionOwner>());
            }

            var owners = StageGeneratedDefinitionOwnershipIndex.GetOwners(target);

            return new StageGeneratedDefinitionOwnership(target, targetPath, targetGuid, owners);
        }
    }

    [InitializeOnLoad]
    internal static class StageGeneratedDefinitionOwnershipIndex
    {
        private static IReadOnlyDictionary<StageDefinition, StageGeneratedDefinitionOwner[]> ownersByTarget;
        private static int buildInvocationCount;

        static StageGeneratedDefinitionOwnershipIndex()
        {
            EditorApplication.projectChanged -= Invalidate;
            EditorApplication.projectChanged += Invalidate;
        }

        internal static int BuildInvocationCountForTests => buildInvocationCount;

        internal static IReadOnlyList<StageGeneratedDefinitionOwner> GetOwners(StageDefinition target)
        {
            EnsureBuilt();
            return target != null && ownersByTarget.TryGetValue(target, out var owners)
                ? owners
                : Array.Empty<StageGeneratedDefinitionOwner>();
        }

        internal static void Invalidate()
        {
            ownersByTarget = null;
        }

        internal static bool InvalidateIfGeneratedGameplayDefinitionChanged(
            StageDefinition previous,
            StageDefinition current)
        {
            if (ReferenceEquals(previous, current))
            {
                return false;
            }

            Invalidate();
            return true;
        }

        internal static void ResetForTests()
        {
            ownersByTarget = null;
            buildInvocationCount = 0;
        }

        internal static void NotifyProjectChangedForTests()
        {
            Invalidate();
        }

        private static void EnsureBuilt()
        {
            if (ownersByTarget != null)
            {
                return;
            }

            buildInvocationCount++;
            var mutableIndex = new Dictionary<StageDefinition, List<StageGeneratedDefinitionOwner>>();
            var paths = AssetDatabase
                .FindAssets("t:StageAuthoringDefinition")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => !string.IsNullOrEmpty(path))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(path => path, StringComparer.Ordinal);
            foreach (var path in paths)
            {
                var authoring = AssetDatabase.LoadAssetAtPath<StageAuthoringDefinition>(path);
                var target = authoring != null ? authoring.GeneratedGameplayDefinition : null;
                if (target == null)
                {
                    continue;
                }

                if (!mutableIndex.TryGetValue(target, out var owners))
                {
                    owners = new List<StageGeneratedDefinitionOwner>();
                    mutableIndex.Add(target, owners);
                }

                owners.Add(new StageGeneratedDefinitionOwner(
                    authoring,
                    path,
                    AssetDatabase.AssetPathToGUID(path)));
            }

            ownersByTarget = mutableIndex.ToDictionary(
                pair => pair.Key,
                pair => pair.Value.ToArray());
        }
    }

    internal sealed class StageGeneratedDefinitionOwnershipAssetPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            StageGeneratedDefinitionOwnershipIndex.Invalidate();
        }
    }

    internal sealed class StageGeneratedDefinitionOwnershipSaveProcessor : AssetModificationProcessor
    {
        private static string[] OnWillSaveAssets(string[] paths)
        {
            StageGeneratedDefinitionOwnershipIndex.Invalidate();
            return paths;
        }
    }
}
