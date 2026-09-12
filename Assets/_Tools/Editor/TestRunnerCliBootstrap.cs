using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security;
using System.Xml;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

public static class TestRunnerCliBootstrap
{
    private const string SelectionArg = "-codexSelection";
    private const string ResultPathArg = "-codexResultPath";
    private const string TestFilterArg = "-codexTestFilter";
    private const string TestTimeoutArg = "-codexTestTimeoutSeconds";

    private const string CoreSelection = "core";
    private const string CoreFeatureGateSelection = "core-feature-gate";
    private const string FullSelection = "full";
    private const string TerminalIrisCaptureSelection = "terminal-iris-capture";
    private const string UiSelection = "ui";
    private const string IntegrationSimulationSelection = "integration-simulation";
    private const string IntegrationReplaySelection = "integration-replay";
    private const string IntegrationFuzzSelection = "integration-fuzz";
    private const string CoreCategory = "Core";
    private const string TerminalIrisCaptureCategory = "TerminalIrisCapture";
    private const string CoreEditModeAssemblyName = "Game.Core.Tests";
    private const string CoreFeatureGateEditModeAssemblyName = "Game.Feature.Gameplay.Tests";
    private const string PlayModeAssemblyName = "Game.Feature.Gameplay.PlayModeTests";
    private const string IntegrationSimulationAssemblyName = "Game.Integration.Simulation.Tests";
    private const string IntegrationReplayAssemblyName = "Game.Integration.Replay.Tests";
    private const string IntegrationFuzzAssemblyName = "Game.Integration.Fuzz.Tests";
    private const string UiEditModeAssemblyName = "Game.Feature.UI.Tests";
    private const int DefaultWatchdogTimeoutSeconds = 285;
    private const int MaximumWatchdogTimeoutSeconds = int.MaxValue - 15;

    private const string SessionPrefix = "Codex.TestRunnerCliBootstrap.";
    private const string ActiveKey = SessionPrefix + "Active";
    private const string StartedKey = SessionPrefix + "Started";
    private const string ModeKey = SessionPrefix + "Mode";
    private const string SelectionKey = SessionPrefix + "Selection";
    private const string OutputPathKey = SessionPrefix + "OutputPath";
    private const string TestFilterKey = SessionPrefix + "TestFilter";
    private const string WatchdogDeadlineKey = SessionPrefix + "WatchdogDeadlineUtcTicks";
    private const string WatchdogTimeoutKey = SessionPrefix + "WatchdogTimeoutSeconds";

    private static bool _hasRun;
    private static bool _scheduled;
    private static bool _watchdogAttached;
    private static bool _exitRequested;

    private static TestMode _testMode;
    private static string _selection = string.Empty;
    private static string _outputPath = string.Empty;
    private static string _testFilter = string.Empty;
    private static DateTime _watchdogDeadlineUtc = DateTime.MinValue;
    private static int _watchdogTimeoutSeconds = DefaultWatchdogTimeoutSeconds;

    private static TestRunnerApi _api;
    private static BatchCallbacks _callbackInstance;

    public static void RunEditMode()
    {
        Start(TestMode.EditMode);
    }

    public static void RunPlayMode()
    {
        Start(TestMode.PlayMode);
    }

    [DidReloadScripts]
    private static void ResumeAfterDomainReload()
    {
        if (!Application.isBatchMode || !SessionState.GetBool(ActiveKey, false))
        {
            return;
        }

        if (!TryRestoreSessionState(out var error))
        {
            Debug.LogError(error);
            WriteFailureXml(error);
            ExitWithCode(2);
            return;
        }

        _hasRun = true;

        if (SessionState.GetBool(StartedKey, false))
        {
            EnsureCallbackRegistration();
            EnsureWatchdog();
            return;
        }

        ScheduleRun();
    }

    private static void Start(TestMode testMode)
    {
        if (_hasRun)
        {
            return;
        }

        _hasRun = true;
        _testMode = testMode;

        if (!Application.isBatchMode)
        {
            _outputPath = GetFallbackOutputPath(testMode);
            Debug.LogError("TestRunnerCliBootstrap only supports batch mode execution.");
            WriteFailureXml("Batch mode is required");
            ExitWithCode(2);
            return;
        }

        if (!TryLoadCommandLineState(testMode, out var error))
        {
            Debug.LogError(error);
            WriteFailureXml(error);
            ExitWithCode(2);
            return;
        }

        if (SessionState.GetBool(ActiveKey, false))
        {
            if (!TryRestoreSessionState(out error))
            {
                Debug.LogError(error);
                WriteFailureXml(error);
                ExitWithCode(2);
                return;
            }

            if (SessionState.GetBool(StartedKey, false))
            {
                EnsureCallbackRegistration();
                EnsureWatchdog();
                return;
            }
        }
        else
        {
            PersistSessionState(started: false);
        }

        ScheduleRun();
    }

    private static void ScheduleRun()
    {
        if (_scheduled)
        {
            return;
        }

        _scheduled = true;
        EditorApplication.update += RunOnce;
    }

    private static void RunOnce()
    {
        EditorApplication.update -= RunOnce;
        _scheduled = false;

        if (!TryRestoreSessionState(out var error))
        {
            Debug.LogError(error);
            WriteFailureXml(error);
            ExitWithCode(2);
            return;
        }

        string[] selectedAssemblyNames;
        string[] selectedCategories;
        try
        {
            ResolveSelection(out selectedAssemblyNames, out selectedCategories);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            WriteFailureXml(exception.Message);
            ExitWithCode(2);
            return;
        }

        EnsureCallbackRegistration();
        if (_exitRequested)
        {
            return;
        }

        var filter = new Filter
        {
            testMode = _testMode,
        };

        if (selectedAssemblyNames.Length > 0)
        {
            filter.assemblyNames = selectedAssemblyNames;
        }

        if (selectedCategories.Length > 0)
        {
            filter.categoryNames = selectedCategories;
        }

        var selectedTestFilters = ParseTestFilters(_testFilter);
        if (selectedTestFilters.Length > 0)
        {
            filter.testNames = BuildTestNameFilters(selectedTestFilters);
            filter.groupNames = BuildGroupNameFilters(selectedTestFilters);
        }

        var executionSettings = new ExecutionSettings(filter)
        {
            runSynchronously = _testMode == TestMode.EditMode &&
                !Environment.GetCommandLineArgs().Contains("-codexAsyncEditMode"),
        };

        try
        {
            SessionState.SetBool(StartedKey, true);
            StartWatchdog();
            Debug.Log(
                "Test run started: " +
                $"selection={_selection}, mode={_testMode}, " +
                $"assemblies={FormatFilterValues(filter.assemblyNames)}, " +
                $"categories={FormatFilterValues(filter.categoryNames)}, " +
                $"testFilter={(_testFilter.Length > 0 ? _testFilter : "<none>")}");
            _api.Execute(executionSettings);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            WriteFailureXml(exception.Message);
            ExitWithCode(2);
        }
    }

    private static void EnsureCallbackRegistration()
    {
        if (_callbackInstance != null)
        {
            return;
        }

        try
        {
            _api = ScriptableObject.CreateInstance<TestRunnerApi>();
            _callbackInstance = new BatchCallbacks();
            _api.RegisterCallbacks(_callbackInstance);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            WriteFailureXml(exception.Message);
            ExitWithCode(2);
        }
    }

    private static void StartWatchdog()
    {
        _watchdogDeadlineUtc = DateTime.UtcNow.AddSeconds(_watchdogTimeoutSeconds);
        SessionState.SetString(
            WatchdogDeadlineKey,
            _watchdogDeadlineUtc.Ticks.ToString(CultureInfo.InvariantCulture));

        if (_watchdogAttached)
        {
            return;
        }

        _watchdogAttached = true;
        EditorApplication.update += WatchdogUpdate;
    }

    private static void EnsureWatchdog()
    {
        if (_watchdogDeadlineUtc == DateTime.MinValue)
        {
            var rawDeadline = SessionState.GetString(WatchdogDeadlineKey, string.Empty);
            if (!long.TryParse(rawDeadline, NumberStyles.Integer, CultureInfo.InvariantCulture, out var deadlineTicks))
            {
                _watchdogDeadlineUtc = DateTime.UtcNow.AddSeconds(_watchdogTimeoutSeconds);
                SessionState.SetString(
                    WatchdogDeadlineKey,
                    _watchdogDeadlineUtc.Ticks.ToString(CultureInfo.InvariantCulture));
            }
            else
            {
                _watchdogDeadlineUtc = new DateTime(deadlineTicks, DateTimeKind.Utc);
            }
        }

        if (_watchdogAttached)
        {
            return;
        }

        _watchdogAttached = true;
        EditorApplication.update += WatchdogUpdate;
    }

    private static void WatchdogUpdate()
    {
        if (_watchdogDeadlineUtc == DateTime.MinValue || DateTime.UtcNow < _watchdogDeadlineUtc)
        {
            return;
        }

        Debug.LogError("Test run watchdog timed out before completion.");
        WriteFailureXml("Test run watchdog timed out before completion");
        ExitWithCode(2);
    }

    private static void HandleRunFinished(ITestResultAdaptor result)
    {
        if (result == null)
        {
            Debug.LogError("Test runner returned a null result.");
            WriteFailureXml("Test runner returned a null result");
            ExitWithCode(2);
            return;
        }

        Debug.Log($"Test run finished: {result.TestStatus}");

        var xmlNode = result.ToXml();
        if (xmlNode == null)
        {
            Debug.LogError("Test result XML is null");
            WriteFailureXml("Result XML is null");
            ExitWithCode(2);
            return;
        }

        var xmlDocument = new XmlDocument();
        try
        {
            xmlDocument.LoadXml(xmlNode.OuterXml);
            xmlDocument = NormalizeResultXml(xmlDocument);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            WriteFailureXml("Failed to parse result XML");
            ExitWithCode(2);
            return;
        }

        var xml = xmlDocument.OuterXml;
        var totalAttr = xmlDocument.DocumentElement?.Attributes?["total"];
        if (totalAttr == null)
        {
            Debug.LogError("Missing total attribute in XML");
            WriteFailureXml("Missing total attribute");
            ExitWithCode(2);
            return;
        }

        Debug.Log($"Total tests: {totalAttr.Value}");

        if (totalAttr.Value == "0" && string.IsNullOrWhiteSpace(_testFilter))
        {
            Debug.LogError("No tests executed (total=0)");
            WriteFailureXml("No tests executed");
            ExitWithCode(2);
            return;
        }

        try
        {
            EnsureOutputDirectoryExists();
            File.WriteAllText(_outputPath, xml);
            Debug.Log($"XML written: {_outputPath}");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            WriteFailureXml("Failed to write result XML");
            ExitWithCode(2);
            return;
        }

        ExitWithCode(result.FailCount > 0 ? 1 : 0);
    }

    private static XmlDocument NormalizeResultXml(XmlDocument xmlDocument)
    {
        var sourceRoot = xmlDocument.DocumentElement;
        if (sourceRoot == null)
        {
            throw new InvalidOperationException("Result XML has no document element.");
        }

        if (string.Equals(sourceRoot.Name, "test-run", StringComparison.Ordinal))
        {
            return xmlDocument;
        }

        var normalizedDocument = new XmlDocument();
        var testRunElement = normalizedDocument.CreateElement("test-run");

        if (sourceRoot.Attributes != null)
        {
            for (var i = 0; i < sourceRoot.Attributes.Count; i++)
            {
                var sourceAttribute = sourceRoot.Attributes[i];
                var targetAttribute = normalizedDocument.CreateAttribute(sourceAttribute.Name);
                targetAttribute.Value = sourceAttribute.Value;
                testRunElement.Attributes.Append(targetAttribute);
            }
        }

        normalizedDocument.AppendChild(testRunElement);
        testRunElement.AppendChild(normalizedDocument.ImportNode(sourceRoot, deep: true));
        return normalizedDocument;
    }

    private static void ExitWithCode(int code)
    {
        if (_exitRequested)
        {
            return;
        }

        _exitRequested = true;

        EditorApplication.update -= RunOnce;
        if (_watchdogAttached)
        {
            EditorApplication.update -= WatchdogUpdate;
        }

        _scheduled = false;
        _watchdogAttached = false;
        _watchdogDeadlineUtc = DateTime.MinValue;

        if (_api != null && _callbackInstance != null)
        {
            _api.UnregisterCallbacks(_callbackInstance);
        }

        if (_api != null)
        {
            UnityEngine.Object.DestroyImmediate(_api);
        }

        _api = null;
        _callbackInstance = null;

        ClearSessionState();
        EditorApplication.Exit(code);
    }

    private static void WriteFailureXml(string message)
    {
        try
        {
            EnsureOutputDirectoryExists();

            var escapedMessage = SecurityElement.Escape(message) ?? string.Empty;
            var xml =
$@"<?xml version=""1.0"" encoding=""utf-8""?>
<test-run total=""0"" passed=""0"" failed=""1"">
  <failure>
    <message>{escapedMessage}</message>
  </failure>
</test-run>";

            File.WriteAllText(_outputPath, xml);
        }
        catch (Exception exception)
        {
            Debug.LogError($"Failed to write failure XML: {exception}");
        }
    }

    private static void EnsureOutputDirectoryExists()
    {
        var directoryPath = Path.GetDirectoryName(_outputPath);
        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }
    }

    private static void ResolveSelection(out string[] selectedAssemblyNames, out string[] selectedCategories)
    {
        selectedAssemblyNames = Array.Empty<string>();
        selectedCategories = Array.Empty<string>();

        if (_selection == FullSelection)
        {
            selectedCategories = new[] { $"!{TerminalIrisCaptureCategory}" };
            return;
        }

        if (_selection == TerminalIrisCaptureSelection)
        {
            if (_testMode != TestMode.PlayMode)
            {
                throw new InvalidOperationException(
                    $"Selection '{_selection}' only supports PlayMode execution.");
            }

            selectedAssemblyNames = new[] { PlayModeAssemblyName };
            selectedCategories = new[] { TerminalIrisCaptureCategory };
            return;
        }

        if (_selection == CoreSelection)
        {
            if (_testMode == TestMode.EditMode)
            {
                selectedAssemblyNames = new[] { CoreEditModeAssemblyName };
                return;
            }

            selectedAssemblyNames = new[] { PlayModeAssemblyName };
            selectedCategories = new[] { CoreCategory };
            return;
        }

        if (_selection == CoreFeatureGateSelection)
        {
            if (_testMode != TestMode.EditMode)
            {
                throw new InvalidOperationException($"Selection '{_selection}' only supports EditMode execution.");
            }

            selectedAssemblyNames = new[] { CoreFeatureGateEditModeAssemblyName };
            selectedCategories = new[] { CoreCategory };
            return;
        }

        if (_testMode != TestMode.EditMode)
        {
            throw new InvalidOperationException($"Selection '{_selection}' only supports EditMode execution.");
        }

        switch (_selection)
        {
            case UiSelection:
                selectedAssemblyNames = new[] { UiEditModeAssemblyName };
                return;
            case IntegrationSimulationSelection:
                selectedAssemblyNames = new[] { IntegrationSimulationAssemblyName };
                return;
            case IntegrationReplaySelection:
                selectedAssemblyNames = new[] { IntegrationReplayAssemblyName };
                return;
            case IntegrationFuzzSelection:
                selectedAssemblyNames = new[] { IntegrationFuzzAssemblyName };
                return;
            default:
                throw new InvalidOperationException($"Unsupported selection '{_selection}'.");
        }
    }

    private static bool TryLoadCommandLineState(TestMode testMode, out string error)
    {
        _testMode = testMode;

        var rawOutputPath = GetSingleArgumentValue(ResultPathArg);
        _outputPath = string.IsNullOrWhiteSpace(rawOutputPath)
            ? GetFallbackOutputPath(testMode)
            : NormalizePath(rawOutputPath);

        var rawSelection = GetSingleArgumentValue(SelectionArg);
        if (string.IsNullOrWhiteSpace(rawSelection))
        {
            error = $"Missing required command line argument: {SelectionArg}";
            return false;
        }

        _selection = rawSelection.Trim().ToLowerInvariant();
        if (_selection != CoreSelection &&
            _selection != CoreFeatureGateSelection &&
            _selection != FullSelection &&
            _selection != TerminalIrisCaptureSelection &&
            _selection != UiSelection &&
            _selection != IntegrationSimulationSelection &&
            _selection != IntegrationReplaySelection &&
            _selection != IntegrationFuzzSelection)
        {
            error = $"Invalid selection '{rawSelection}'.";
            return false;
        }

        var rawTimeout = GetSingleArgumentValue(TestTimeoutArg);
        _watchdogTimeoutSeconds = DefaultWatchdogTimeoutSeconds;
        if (rawTimeout.Length > 0 && !TryParseWatchdogTimeout(rawTimeout, out _watchdogTimeoutSeconds))
        {
            error = $"Invalid {TestTimeoutArg}: expected an integer from 1 through {MaximumWatchdogTimeoutSeconds} without whitespace or leading zeros.";
            return false;
        }

        _testFilter = GetSingleArgumentValue(TestFilterArg).Trim();
        error = string.Empty;
        return true;
    }

    private static bool TryParseWatchdogTimeout(string value, out int seconds)
    {
        seconds = 0;
        return !string.IsNullOrEmpty(value) &&
            value[0] >= '1' && value[0] <= '9' &&
            int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out seconds) &&
            seconds <= MaximumWatchdogTimeoutSeconds;
    }

    private static bool TryRestoreSessionState(out string error)
    {
        var modeValue = SessionState.GetString(ModeKey, string.Empty);
        if (!TryParseMode(modeValue, out _testMode))
        {
            _outputPath = GetFallbackOutputPath(TestMode.EditMode);
            error = "Failed to restore test mode from SessionState.";
            return false;
        }

        _selection = SessionState.GetString(SelectionKey, string.Empty);
        _outputPath = SessionState.GetString(OutputPathKey, GetFallbackOutputPath(_testMode));
        _testFilter = SessionState.GetString(TestFilterKey, string.Empty);
        _watchdogTimeoutSeconds = SessionState.GetInt(WatchdogTimeoutKey, DefaultWatchdogTimeoutSeconds);
        if (_watchdogTimeoutSeconds <= 0 || _watchdogTimeoutSeconds > MaximumWatchdogTimeoutSeconds)
        {
            error = "Failed to restore test watchdog timeout from SessionState.";
            return false;
        }

        if (_selection != CoreSelection &&
            _selection != CoreFeatureGateSelection &&
            _selection != FullSelection &&
            _selection != TerminalIrisCaptureSelection &&
            _selection != UiSelection &&
            _selection != IntegrationSimulationSelection &&
            _selection != IntegrationReplaySelection &&
            _selection != IntegrationFuzzSelection)
        {
            error = "Failed to restore selection from SessionState.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static void PersistSessionState(bool started)
    {
        SessionState.SetBool(ActiveKey, true);
        SessionState.SetBool(StartedKey, started);
        SessionState.SetString(ModeKey, _testMode.ToString());
        SessionState.SetString(SelectionKey, _selection);
        SessionState.SetString(OutputPathKey, _outputPath);
        SessionState.SetString(TestFilterKey, _testFilter);
        SessionState.SetInt(WatchdogTimeoutKey, _watchdogTimeoutSeconds);
    }

    private static void ClearSessionState()
    {
        SessionState.EraseBool(ActiveKey);
        SessionState.EraseBool(StartedKey);
        SessionState.EraseString(ModeKey);
        SessionState.EraseString(SelectionKey);
        SessionState.EraseString(OutputPathKey);
        SessionState.EraseString(TestFilterKey);
        SessionState.EraseString(WatchdogDeadlineKey);
        SessionState.EraseInt(WatchdogTimeoutKey);
    }

    private static string[] ParseTestFilters(string rawFilter)
    {
        if (string.IsNullOrWhiteSpace(rawFilter))
        {
            return Array.Empty<string>();
        }

        return rawFilter
            .Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(filter => filter.Trim())
            .Where(filter => filter.Length > 0)
            .ToArray();
    }

    private static string[] BuildTestNameFilters(string[] testFilters)
    {
        var testNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var testFilter in testFilters)
        {
            testNames.Add(testFilter);
        }

        foreach (var type in EnumerateLoadedTypes())
        {
            foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                var fullMethodName = $"{type.FullName}.{method.Name}";
                if (testFilters.Any(testFilter => MatchesFilter(testFilter, method.Name) || MatchesFilter(testFilter, fullMethodName)))
                {
                    testNames.Add(fullMethodName);
                }
            }
        }

        return testNames.ToArray();
    }

    private static string[] BuildGroupNameFilters(string[] testFilters)
    {
        var groupNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var testFilter in testFilters)
        {
            groupNames.Add(testFilter);
        }

        foreach (var type in EnumerateLoadedTypes())
        {
            if (testFilters.Any(testFilter => MatchesFilter(testFilter, type.Name) || MatchesFilter(testFilter, type.FullName)))
            {
                groupNames.Add(type.FullName);
            }
        }

        return groupNames.ToArray();
    }

    private static Type[] EnumerateLoadedTypes()
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(GetTypesOrEmpty)
            .Where(type => type != null && !string.IsNullOrWhiteSpace(type.FullName))
            .ToArray();
    }

    private static Type[] GetTypesOrEmpty(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types.Where(type => type != null).ToArray();
        }
        catch
        {
            return Array.Empty<Type>();
        }
    }

    private static bool MatchesFilter(string testFilter, string candidate)
    {
        return !string.IsNullOrWhiteSpace(candidate) &&
            candidate.IndexOf(testFilter, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string FormatFilterValues(string[] values)
    {
        return values == null || values.Length == 0
            ? "<none>"
            : string.Join(";", values);
    }

    private static string GetSingleArgumentValue(string argumentName)
    {
        var args = Environment.GetCommandLineArgs();
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], argumentName, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        return string.Empty;
    }

    private static string NormalizePath(string path)
    {
        return Path.GetFullPath(path.Trim());
    }

    private static string GetFallbackOutputPath(TestMode testMode)
    {
        var suffix = testMode == TestMode.EditMode ? "editmode" : "playmode";
        return Path.Combine(GetProjectRootPath(), "TestResults", $"codex-bootstrap-{suffix}-failure.xml");
    }

    private static string GetProjectRootPath()
    {
        return Directory.GetParent(Application.dataPath)?.FullName ?? Directory.GetCurrentDirectory();
    }

    private static bool TryParseMode(string rawValue, out TestMode testMode)
    {
        if (string.Equals(rawValue, TestMode.EditMode.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            testMode = TestMode.EditMode;
            return true;
        }

        if (string.Equals(rawValue, TestMode.PlayMode.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            testMode = TestMode.PlayMode;
            return true;
        }

        testMode = default;
        return false;
    }

    private sealed class BatchCallbacks : ICallbacks
    {
        public void RunStarted(ITestAdaptor testsToRun)
        {
        }

        public void RunFinished(ITestResultAdaptor result)
        {
            HandleRunFinished(result);
        }

        public void TestStarted(ITestAdaptor test)
        {
        }

        public void TestFinished(ITestResultAdaptor result)
        {
        }
    }
}
