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

            var owners = AssetDatabase
                .FindAssets("t:StageAuthoringDefinition")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => !string.IsNullOrEmpty(path))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(path => new
                {
                    Path = path,
                    Authoring = AssetDatabase.LoadAssetAtPath<StageAuthoringDefinition>(path),
                })
                .Where(candidate =>
                    candidate.Authoring != null &&
                    candidate.Authoring.GeneratedGameplayDefinition == target)
                .Select(candidate => new StageGeneratedDefinitionOwner(
                    candidate.Authoring,
                    candidate.Path,
                    AssetDatabase.AssetPathToGUID(candidate.Path)))
                .ToArray();

            return new StageGeneratedDefinitionOwnership(target, targetPath, targetGuid, owners);
        }
    }
}
