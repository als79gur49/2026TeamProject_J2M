using System.Collections.Generic;

namespace Game.Feature.Stages.Editor
{
    internal static class StageDefinitionInspectorPolicy
    {
        public static bool IsSelectionEditable(
            IReadOnlyList<StageGeneratedDefinitionOwnership> ownerships)
        {
            if (ownerships == null || ownerships.Count == 0)
            {
                return false;
            }

            for (var i = 0; i < ownerships.Count; i++)
            {
                if (ownerships[i] == null ||
                    ownerships[i].Kind != StageDefinitionOwnershipKind.Standalone)
                {
                    return false;
                }
            }

            return true;
        }

        public static bool CanUseCanonicalActions(
            IReadOnlyList<StageGeneratedDefinitionOwnership> ownerships)
        {
            return ownerships != null &&
                   ownerships.Count == 1 &&
                   ownerships[0] != null &&
                   ownerships[0].Kind == StageDefinitionOwnershipKind.GeneratedOwned &&
                   ownerships[0].Owner != null;
        }
    }
}
