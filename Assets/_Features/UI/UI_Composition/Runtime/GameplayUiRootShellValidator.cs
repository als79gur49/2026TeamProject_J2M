using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Game.Feature.UI.Composition
{
    internal static class GameplayUiRootShellValidator
    {
        internal const string ShellChromeName = "ShellChrome";
        internal const string HudLayerName = "HudLayer";
        internal const string ScreenLayerName = "ScreenLayer";
        internal const string PopupLayerName = "PopupLayer";

        private static readonly string[] RequiredTopLevelChildNames =
        {
            HudLayerName,
            ScreenLayerName,
            PopupLayerName,
        };

        private static readonly HashSet<string> AllowedTopLevelChildNames = new(StringComparer.Ordinal)
        {
            ShellChromeName,
            HudLayerName,
            ScreenLayerName,
            PopupLayerName,
        };

        internal static IReadOnlyList<string> AllowedTopLevelChildren => RequiredTopLevelChildNames
            .Concat(new[] { ShellChromeName })
            .ToArray();

        internal static IReadOnlyList<string> RequiredTopLevelChildren => RequiredTopLevelChildNames;

        internal static void Validate(GameplayUiCanvasRootView rootView)
        {
            if (rootView == null)
            {
                throw new ArgumentNullException(nameof(rootView));
            }

            var topLevelChildren = rootView.transform.Cast<Transform>().ToArray();
            foreach (var child in topLevelChildren)
            {
                if (!AllowedTopLevelChildNames.Contains(child.name))
                {
                    throw new InvalidOperationException(
                        $"Unexpected canonical root-shell child '{child.name}'. Only shell-level children are allowed.");
                }
            }

            ValidateRequiredLayer(rootView.HudLayer, HudLayerName, rootView.transform);
            ValidateRequiredLayer(rootView.ScreenLayer, ScreenLayerName, rootView.transform);
            ValidateRequiredLayer(rootView.PopupLayer, PopupLayerName, rootView.transform);
        }

        private static void ValidateRequiredLayer(RectTransform layer, string expectedName, Transform rootTransform)
        {
            if (layer == null)
            {
                throw new InvalidOperationException(
                    $"Canonical root shell is missing required top-level child '{expectedName}'.");
            }

            if (!string.Equals(layer.name, expectedName, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Canonical root shell expected child '{expectedName}' but resolved '{layer.name}'.");
            }

            if (layer.parent != rootTransform)
            {
                throw new InvalidOperationException(
                    $"Canonical root shell child '{expectedName}' must remain a direct child of the root shell.");
            }
        }
    }
}
