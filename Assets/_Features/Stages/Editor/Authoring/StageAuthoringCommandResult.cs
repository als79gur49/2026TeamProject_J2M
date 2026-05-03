using UnityEditor;

namespace Game.Feature.Stages.Editor
{
    internal readonly struct StageAuthoringCommandResult
    {
        private const int NoSelectionIndex = -1;

        private StageAuthoringCommandResult(
            bool changed,
            bool requiresApplyModifiedProperties,
            bool requiresSerializedObjectUpdate,
            bool requiresSetDirty,
            bool shouldRepaint,
            string message,
            MessageType messageType,
            int selectPlacementIndex,
            bool clearSelectedPlacement)
        {
            Changed = changed;
            RequiresApplyModifiedProperties = requiresApplyModifiedProperties;
            RequiresSerializedObjectUpdate = requiresSerializedObjectUpdate;
            RequiresSetDirty = requiresSetDirty;
            ShouldRepaint = shouldRepaint;
            Message = message;
            MessageType = messageType;
            SelectPlacementIndex = selectPlacementIndex;
            ClearSelectedPlacement = clearSelectedPlacement;
        }

        public bool Changed { get; }

        public bool RequiresApplyModifiedProperties { get; }

        public bool RequiresSerializedObjectUpdate { get; }

        public bool RequiresSetDirty { get; }

        public bool ShouldRepaint { get; }

        public string Message { get; }

        public MessageType MessageType { get; }

        public int SelectPlacementIndex { get; }

        public bool HasPlacementSelection => SelectPlacementIndex >= 0;

        public bool ClearSelectedPlacement { get; }

        public static StageAuthoringCommandResult NoOp()
        {
            return new StageAuthoringCommandResult(
                changed: false,
                requiresApplyModifiedProperties: false,
                requiresSerializedObjectUpdate: false,
                requiresSetDirty: false,
                shouldRepaint: false,
                message: null,
                MessageType.Info,
                NoSelectionIndex,
                clearSelectedPlacement: false);
        }

        public static StageAuthoringCommandResult ChangedResult(
            bool requiresApply = true,
            bool requiresUpdate = true,
            bool requiresSetDirty = false,
            bool shouldRepaint = true,
            string message = null,
            MessageType messageType = MessageType.Info,
            int selectPlacementIndex = NoSelectionIndex,
            bool clearSelectedPlacement = false)
        {
            return new StageAuthoringCommandResult(
                changed: true,
                requiresApply,
                requiresUpdate,
                requiresSetDirty,
                shouldRepaint,
                message,
                messageType,
                selectPlacementIndex,
                clearSelectedPlacement);
        }
    }
}
