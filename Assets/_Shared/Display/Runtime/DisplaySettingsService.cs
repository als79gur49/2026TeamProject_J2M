using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;

namespace Game.Shared.Display
{
    public sealed class DisplaySettingsService : IDisplaySettingsService
    {
        private readonly IDisplayRuntimeGateway runtimeGateway;
        private readonly IDisplaySettingsStore store;
        private bool committedInitialized;
        private bool hasBootApplied;
        private bool isPreviewActive;
        private DisplaySettingsSnapshot committedSettings;
        private DisplaySettingsSnapshot previewAppliedSettings;
        private DisplaySettingsSnapshot previewOriginalRuntimeSettings;

        public DisplaySettingsService(IDisplaySettingsStore store)
            : this(store, new UnityDisplayRuntimeGateway())
        {
        }

        internal DisplaySettingsService(
            IDisplaySettingsStore store,
            IDisplayRuntimeGateway runtimeGateway)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.runtimeGateway = runtimeGateway ?? throw new ArgumentNullException(nameof(runtimeGateway));
        }

        public bool IsPreviewActive => isPreviewActive;

        public bool HasBootApplied => hasBootApplied;

        public DisplaySettingsSnapshot ReadCurrentDisplaySettings()
        {
            return runtimeGateway.ReadCurrentDisplaySettings();
        }

        public DisplaySettingsSnapshot ReadCommittedDisplaySettings()
        {
            EnsureCommittedInitialized();
            return committedSettings;
        }

        public IReadOnlyList<DisplayModeOption> ReadAvailableDisplayModes()
        {
            return BuildCatalog(ReadCurrentDisplaySettings(), runtimeGateway.ReadSupportedResolutions());
        }

        public bool ApplyBootSettingsOnce()
        {
            EnsureCommittedInitialized();
            if (hasBootApplied)
            {
                return false;
            }

            hasBootApplied = true;

            var currentRuntime = ReadCurrentDisplaySettings();
            var availableModes = ReadAvailableDisplayModes();
            var normalizedCurrent = NormalizeForComparison(currentRuntime, availableModes);
            if (normalizedCurrent.Equals(committedSettings))
            {
                return true;
            }

            runtimeGateway.ApplyDisplaySettings(committedSettings);
            return true;
        }

        public bool BeginPreview(DisplaySettingsSnapshot snapshot)
        {
            EnsureCommittedInitialized();
            if (isPreviewActive)
            {
                return false;
            }

            var currentRuntime = ReadCurrentDisplaySettings();
            var availableModes = ReadAvailableDisplayModes();
            var normalizedPreview = NormalizeRequestedSnapshot(snapshot, availableModes, currentRuntime);

            previewOriginalRuntimeSettings = currentRuntime;
            previewAppliedSettings = normalizedPreview;
            isPreviewActive = true;

            if (!NormalizeForComparison(currentRuntime, availableModes).Equals(normalizedPreview))
            {
                runtimeGateway.ApplyDisplaySettings(normalizedPreview);
            }

            return true;
        }

        public bool CommitPreview()
        {
            if (!isPreviewActive)
            {
                return false;
            }

            var availableModes = ReadAvailableDisplayModes();
            var currentRuntime = ReadCurrentDisplaySettings();
            committedSettings = NormalizeRequestedSnapshot(previewAppliedSettings, availableModes, currentRuntime);
            store.Save(committedSettings);
            ClearPreviewState();
            return true;
        }

        public bool RevertPreview()
        {
            if (!isPreviewActive)
            {
                return false;
            }

            var revertTarget = previewOriginalRuntimeSettings;
            var availableModes = ReadAvailableDisplayModes();
            var normalizedCurrent = NormalizeForComparison(ReadCurrentDisplaySettings(), availableModes);
            var normalizedTarget = NormalizeForComparison(revertTarget, availableModes);
            ClearPreviewState();

            if (!normalizedCurrent.Equals(normalizedTarget))
            {
                runtimeGateway.ApplyDisplaySettings(revertTarget);
            }

            return true;
        }

        private void EnsureCommittedInitialized()
        {
            if (committedInitialized)
            {
                return;
            }

            var currentRuntime = ReadCurrentDisplaySettings();
            var availableModes = ReadAvailableDisplayModes();
            committedSettings = store.TryLoad(out var savedSettings)
                ? NormalizeRequestedSnapshot(savedSettings, availableModes, currentRuntime)
                : NormalizeRequestedSnapshot(currentRuntime, availableModes, currentRuntime);
            committedInitialized = true;
        }

        private void ClearPreviewState()
        {
            isPreviewActive = false;
            previewAppliedSettings = default;
            previewOriginalRuntimeSettings = default;
        }

        private static DisplaySettingsSnapshot NormalizeForComparison(
            DisplaySettingsSnapshot snapshot,
            IReadOnlyList<DisplayModeOption> catalog)
        {
            return NormalizeRequestedSnapshot(snapshot, catalog, snapshot);
        }

        private static DisplaySettingsSnapshot NormalizeRequestedSnapshot(
            DisplaySettingsSnapshot snapshot,
            IReadOnlyList<DisplayModeOption> catalog,
            DisplaySettingsSnapshot currentRuntime)
        {
            var effectiveCatalog = catalog == null || catalog.Count == 0
                ? new[] { new DisplayModeOption(currentRuntime.Width, currentRuntime.Height, currentRuntime.RefreshRateNumerator, currentRuntime.RefreshRateDenominator) }
                : catalog;

            var normalizedWindowMode = NormalizeVisibleWindowMode(snapshot.WindowMode);
            if (TryFindBySize(effectiveCatalog, snapshot.Width, snapshot.Height, out var matchedOption))
            {
                return matchedOption.ToSnapshot(normalizedWindowMode);
            }

            if (TryFindBySize(effectiveCatalog, currentRuntime.Width, currentRuntime.Height, out var currentOption))
            {
                return currentOption.ToSnapshot(normalizedWindowMode);
            }

            return effectiveCatalog[0].ToSnapshot(normalizedWindowMode);
        }

        internal static DisplayModeOption[] BuildCatalog(
            DisplaySettingsSnapshot currentRuntime,
            IReadOnlyList<Resolution> supportedResolutions)
        {
            if (supportedResolutions == null || supportedResolutions.Count == 0)
            {
                return new[]
                {
                    new DisplayModeOption(
                        currentRuntime.Width,
                        currentRuntime.Height,
                        currentRuntime.RefreshRateNumerator,
                        currentRuntime.RefreshRateDenominator),
                };
            }

            var ordered = new List<DisplayModeOption>(supportedResolutions.Count);
            var visibleIndexBySize = new Dictionary<long, int>();

            for (var i = 0; i < supportedResolutions.Count; i++)
            {
                var resolution = supportedResolutions[i];
                var refreshRate = DisplayRefreshRateReflection.Read(resolution);
                var option = new DisplayModeOption(
                    resolution.width,
                    resolution.height,
                    refreshRate.Numerator,
                    refreshRate.Denominator);
                var sizeKey = BuildSizeKey(option.Width, option.Height);

                if (!visibleIndexBySize.TryGetValue(sizeKey, out var existingIndex))
                {
                    visibleIndexBySize[sizeKey] = ordered.Count;
                    ordered.Add(option);
                    continue;
                }

                if (CompareRefreshRate(option, ordered[existingIndex]) > 0)
                {
                    ordered[existingIndex] = option;
                }
            }

            if (ordered.Count == 0)
            {
                ordered.Add(new DisplayModeOption(
                    currentRuntime.Width,
                    currentRuntime.Height,
                    currentRuntime.RefreshRateNumerator,
                    currentRuntime.RefreshRateDenominator));
            }

            return ordered.ToArray();
        }

        private static int CompareRefreshRate(DisplayModeOption left, DisplayModeOption right)
        {
            long leftNumerator = left.RefreshRateNumerator;
            long leftDenominator = Math.Max(1, left.RefreshRateDenominator);
            long rightNumerator = right.RefreshRateNumerator;
            long rightDenominator = Math.Max(1, right.RefreshRateDenominator);

            var comparison = (leftNumerator * rightDenominator).CompareTo(rightNumerator * leftDenominator);
            if (comparison != 0)
            {
                return comparison;
            }

            return leftNumerator.CompareTo(rightNumerator);
        }

        private static bool TryFindBySize(
            IReadOnlyList<DisplayModeOption> catalog,
            int width,
            int height,
            out DisplayModeOption option)
        {
            for (var i = 0; i < catalog.Count; i++)
            {
                if (catalog[i].Width != width || catalog[i].Height != height)
                {
                    continue;
                }

                option = catalog[i];
                return true;
            }

            option = default;
            return false;
        }

        private static long BuildSizeKey(int width, int height)
        {
            return ((long)width << 32) | (uint)height;
        }

        private static DisplayWindowMode NormalizeVisibleWindowMode(DisplayWindowMode windowMode)
        {
            return windowMode == DisplayWindowMode.Windowed
                ? DisplayWindowMode.Windowed
                : DisplayWindowMode.FullScreenWindow;
        }
    }

    internal interface IDisplayRuntimeGateway
    {
        DisplaySettingsSnapshot ReadCurrentDisplaySettings();

        Resolution[] ReadSupportedResolutions();

        void ApplyDisplaySettings(DisplaySettingsSnapshot snapshot);
    }

    internal sealed class UnityDisplayRuntimeGateway : IDisplayRuntimeGateway
    {
        public DisplaySettingsSnapshot ReadCurrentDisplaySettings()
        {
            var refreshRate = DisplayRefreshRateReflection.Read(Screen.currentResolution);
            return new DisplaySettingsSnapshot(
                Screen.width,
                Screen.height,
                MapVisibleWindowMode(Screen.fullScreenMode),
                refreshRate.Numerator,
                refreshRate.Denominator);
        }

        public Resolution[] ReadSupportedResolutions()
        {
            return Screen.resolutions ?? Array.Empty<Resolution>();
        }

        public void ApplyDisplaySettings(DisplaySettingsSnapshot snapshot)
        {
            var unityWindowMode = snapshot.WindowMode == DisplayWindowMode.Windowed
                ? FullScreenMode.Windowed
                : FullScreenMode.FullScreenWindow;
            if (DisplayRefreshRateReflection.TryApplyResolution(snapshot, unityWindowMode))
            {
                return;
            }

            Screen.SetResolution(snapshot.Width, snapshot.Height, unityWindowMode);
        }

        private static DisplayWindowMode MapVisibleWindowMode(FullScreenMode windowMode)
        {
            return windowMode == FullScreenMode.Windowed
                ? DisplayWindowMode.Windowed
                : DisplayWindowMode.FullScreenWindow;
        }
    }

    internal readonly struct DisplayRefreshRate
    {
        public DisplayRefreshRate(int numerator, int denominator)
        {
            Numerator = Math.Max(0, numerator);
            Denominator = Math.Max(1, denominator);
        }

        public int Numerator { get; }

        public int Denominator { get; }
    }

    internal static class DisplayRefreshRateReflection
    {
        private static readonly PropertyInfo RefreshRateProperty =
            typeof(Resolution).GetProperty("refreshRate", BindingFlags.Instance | BindingFlags.Public);

        private static readonly PropertyInfo RefreshRateRatioProperty =
            typeof(Resolution).GetProperty("refreshRateRatio", BindingFlags.Instance | BindingFlags.Public);

        private static readonly PropertyInfo RatioNumeratorProperty =
            RefreshRateRatioProperty?.PropertyType.GetProperty("numerator", BindingFlags.Instance | BindingFlags.Public);

        private static readonly PropertyInfo RatioDenominatorProperty =
            RefreshRateRatioProperty?.PropertyType.GetProperty("denominator", BindingFlags.Instance | BindingFlags.Public);

        private static readonly FieldInfo RatioNumeratorField =
            RefreshRateRatioProperty?.PropertyType.GetField("numerator", BindingFlags.Instance | BindingFlags.Public);

        private static readonly FieldInfo RatioDenominatorField =
            RefreshRateRatioProperty?.PropertyType.GetField("denominator", BindingFlags.Instance | BindingFlags.Public);

        private static readonly MethodInfo SetResolutionWithRefreshRateMethod =
            RefreshRateRatioProperty == null
                ? null
                : typeof(Screen).GetMethod(
                    "SetResolution",
                    BindingFlags.Static | BindingFlags.Public,
                    null,
                    new[]
                    {
                        typeof(int),
                        typeof(int),
                        typeof(FullScreenMode),
                        RefreshRateRatioProperty.PropertyType,
                    },
                    null);

        public static DisplayRefreshRate Read(Resolution resolution)
        {
            if (RefreshRateRatioProperty != null &&
                RatioNumeratorProperty != null &&
                RatioDenominatorProperty != null)
            {
                var ratio = RefreshRateRatioProperty.GetValue(resolution, null);
                if (ratio != null)
                {
                    var numerator = ConvertToInt(RatioNumeratorProperty.GetValue(ratio, null));
                    var denominator = ConvertToInt(RatioDenominatorProperty.GetValue(ratio, null));
                    if (numerator > 0)
                    {
                        return new DisplayRefreshRate(numerator, Math.Max(1, denominator));
                    }
                }
            }

            if (RefreshRateProperty != null)
            {
                var refreshRate = ConvertToInt(RefreshRateProperty.GetValue(resolution, null));
                if (refreshRate > 0)
                {
                    return new DisplayRefreshRate(refreshRate, 1);
                }
            }

            return new DisplayRefreshRate(0, 1);
        }

        public static bool TryApplyResolution(DisplaySettingsSnapshot snapshot, FullScreenMode windowMode)
        {
            var preferredRefreshRate = snapshot.PreferredRefreshRate;
            if (preferredRefreshRate <= 0)
            {
                return false;
            }

            if (SetResolutionWithRefreshRateMethod != null &&
                TryCreateRefreshRateRatio(snapshot.RefreshRateNumerator, snapshot.RefreshRateDenominator, out var refreshRateRatio))
            {
                SetResolutionWithRefreshRateMethod.Invoke(
                    null,
                    new object[] { snapshot.Width, snapshot.Height, windowMode, refreshRateRatio });
                return true;
            }

#pragma warning disable CS0618
            Screen.SetResolution(snapshot.Width, snapshot.Height, windowMode, preferredRefreshRate);
#pragma warning restore CS0618
            return true;
        }

        private static int ConvertToInt(object value)
        {
            switch (value)
            {
                case byte byteValue:
                    return byteValue;
                case sbyte sbyteValue:
                    return sbyteValue;
                case short shortValue:
                    return shortValue;
                case ushort ushortValue:
                    return ushortValue;
                case int intValue:
                    return intValue;
                case uint uintValue:
                    return uintValue > int.MaxValue ? int.MaxValue : (int)uintValue;
                case long longValue:
                    return longValue > int.MaxValue ? int.MaxValue : (int)longValue;
                case ulong ulongValue:
                    return ulongValue > int.MaxValue ? int.MaxValue : (int)ulongValue;
                default:
                    return 0;
            }
        }

        private static bool TryCreateRefreshRateRatio(int numerator, int denominator, out object refreshRateRatio)
        {
            refreshRateRatio = null;
            if (RefreshRateRatioProperty == null)
            {
                return false;
            }

            var boxedValue = Activator.CreateInstance(RefreshRateRatioProperty.PropertyType);
            if (boxedValue == null)
            {
                return false;
            }

            if (!TrySetRatioMember(ref boxedValue, RatioNumeratorProperty, RatioNumeratorField, Math.Max(0, numerator)))
            {
                return false;
            }

            if (!TrySetRatioMember(ref boxedValue, RatioDenominatorProperty, RatioDenominatorField, Math.Max(1, denominator)))
            {
                return false;
            }

            refreshRateRatio = boxedValue;
            return true;
        }

        private static bool TrySetRatioMember(ref object boxedValue, PropertyInfo property, FieldInfo field, int value)
        {
            if (field != null)
            {
                field.SetValue(boxedValue, CoerceRatioMemberValue(field.FieldType, value));
                return true;
            }

            if (property != null && property.CanWrite)
            {
                property.SetValue(boxedValue, CoerceRatioMemberValue(property.PropertyType, value), null);
                return true;
            }

            return false;
        }

        private static object CoerceRatioMemberValue(Type memberType, int value)
        {
            var targetType = Nullable.GetUnderlyingType(memberType) ?? memberType;
            if (targetType == typeof(int))
            {
                return value;
            }

            return Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
        }
    }
}
