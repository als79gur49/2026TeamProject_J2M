using System;
using System.Globalization;
using System.IO;
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

    private const string CoreSelection = "core";
    private const string FullSelection = "full";
    private const string IntegrationSimulationSelection = "integration-simulation";
    private const string IntegrationReplaySelection = "integration-replay";
    private const string IntegrationFuzzSelection = "integration-fuzz";
    private const string CoreCategory = "Core";
    private const string CoreEditModeAssemblyName = "Game.Core.Tests";
    private const string PlayModeAssemblyName = "Game.Feature.Gameplay.PlayModeTests";
    private const string IntegrationSimulationAssemblyName = "Game.Integration.Simulation.Tests";
    private const string IntegrationReplayAssemblyName = "Game.Integration.Replay.Tests";
    private const string IntegrationFuzzAssemblyName = "Game.Integration.Fuzz.Tests";
    private const int WatchdogTimeoutSeconds = 285;

    private const string SessionPrefix = "Codex.TestRunnerCliBootstrap.";
    private const string ActiveKey = SessionPrefix + "Active";
    private const string StartedKey = SessionPrefix + "Started";
    private const string ModeKey = SessionPrefix + "Mode";
    private const string SelectionKey = SessionPrefix + "Selection";
    private const string OutputPathKey = SessionPrefix + "OutputPath";
    private const string WatchdogDeadlineKey = SessionPrefix + "WatchdogDeadlineUtcTicks";

    private static bool _hasRun;
    private static bool _scheduled;
    private static bool _watchdogAttached;
    private static bool _exitRequested;

    private static TestMode _testMode;
    private static string _selection = string.Empty;
    private static string _outputPath = string.Empty;
    private static DateTime _watchdogDeadlineUtc = DateTime.MinValue;

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

        if (_selection == CoreSelection)
        {
            if (selectedAssemblyNames.Length > 0)
            {
                filter.assemblyNames = selectedAssemblyNames;
            }

            if (selectedCategories.Length > 0)
            {
                filter.categoryNames = selectedCategories;
            }
        }
        else if (selectedAssemblyNames.Length > 0)
        {
            filter.assemblyNames = selectedAssemblyNames;
        }

        var executionSettings = new ExecutionSettings(filter)
        {
            runSynchronously = _testMode == TestMode.EditMode,
        };

        try
        {
            SessionState.SetBool(StartedKey, true);
            StartWatchdog();
            Debug.Log("Test run started");
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
        _watchdogDeadlineUtc = DateTime.UtcNow.AddSeconds(WatchdogTimeoutSeconds);
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
                _watchdogDeadlineUtc = DateTime.UtcNow.AddSeconds(WatchdogTimeoutSeconds);
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

        if (totalAttr.Value == "0")
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

        if (_testMode != TestMode.EditMode)
        {
            throw new InvalidOperationException($"Selection '{_selection}' only supports EditMode execution.");
        }

        switch (_selection)
        {
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
            _selection != FullSelection &&
            _selection != IntegrationSimulationSelection &&
            _selection != IntegrationReplaySelection &&
            _selection != IntegrationFuzzSelection)
        {
            error = $"Invalid selection '{rawSelection}'.";
            return false;
        }

        error = string.Empty;
        return true;
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

        if (_selection != CoreSelection &&
            _selection != FullSelection &&
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
    }

    private static void ClearSessionState()
    {
        SessionState.EraseBool(ActiveKey);
        SessionState.EraseBool(StartedKey);
        SessionState.EraseString(ModeKey);
        SessionState.EraseString(SelectionKey);
        SessionState.EraseString(OutputPathKey);
        SessionState.EraseString(WatchdogDeadlineKey);
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
