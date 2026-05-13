using System;
using System.Reflection;
using UnityEngine.EventSystems;

namespace Game.Feature.UI.Composition
{
    internal static class UiEventSystemNavigationActionUtility
    {
        internal static Type RequireInputSystemUiModuleType()
        {
            var inputSystemUiModuleType = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (inputSystemUiModuleType == null)
            {
                throw new InvalidOperationException(
                    "Unity Input System UI module is unavailable. Verify that the Input System package is installed.");
            }

            return inputSystemUiModuleType;
        }

        internal static void DisableNavigationActions(EventSystem eventSystem, Type inputSystemUiModuleType)
        {
            if (eventSystem == null || inputSystemUiModuleType == null)
            {
                return;
            }

            var module = eventSystem.GetComponent(inputSystemUiModuleType);
            if (module == null)
            {
                return;
            }

            ClearActionReference(module, inputSystemUiModuleType, "move");
            ClearActionReference(module, inputSystemUiModuleType, "moveAction");
            ClearActionReference(module, inputSystemUiModuleType, "m_MoveAction");
            ClearActionReference(module, inputSystemUiModuleType, "submit");
            ClearActionReference(module, inputSystemUiModuleType, "submitAction");
            ClearActionReference(module, inputSystemUiModuleType, "m_SubmitAction");
            ClearActionReference(module, inputSystemUiModuleType, "cancel");
            ClearActionReference(module, inputSystemUiModuleType, "cancelAction");
            ClearActionReference(module, inputSystemUiModuleType, "m_CancelAction");
        }

        private static void ClearActionReference(object module, Type moduleType, string memberName)
        {
            var property = moduleType.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null && property.CanWrite)
            {
                property.SetValue(module, null);
                return;
            }

            var field = moduleType.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null)
            {
                field.SetValue(module, null);
            }
        }
    }
}
