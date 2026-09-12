using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using Game.Exhibition.RestartExperiment;

namespace Game.Exhibition.Integration
{
    public interface ICompletedParticipantResetReturn
    {
        void Validate(ExhibitionResetCoordinator coordinator);
    }

    public sealed class ExhibitionRelaunchAdapter : IParticipantRestart
    {
        private readonly int _timeoutSeconds;

        public ExhibitionRelaunchAdapter(int timeoutSeconds)
        {
            if (timeoutSeconds < 1 || timeoutSeconds > 120)
                throw new ArgumentOutOfRangeException(nameof(timeoutSeconds), "The relaunch timeout must be between 1 and 120 seconds.");
            _timeoutSeconds = timeoutSeconds;
        }

        public void ValidateAvailable()
        {
            var request = Game.Platform.Runtime.PlatformProviderSelection.CurrentRequest;
            if (request.Kind != Game.Platform.Runtime.PlatformProviderSelectionKind.Explicit ||
                request.RequestedProviderId != Game.Platform.Steam.SteamPlatformRuntime.ProviderId)
                throw new InvalidOperationException("Restart from the Steam launch entry. Participant reset cannot switch a Local session to Steam.");
            using (var current = Process.GetCurrentProcess())
            {
                var executable = current.MainModule?.FileName;
                if (string.IsNullOrEmpty(executable) || !File.Exists(executable) ||
                    !File.Exists(Path.Combine(Path.GetDirectoryName(executable), "Exhibition-Relaunch.ps1")))
                    throw new FileNotFoundException("게임 재실행 파일이 없습니다.");
                if (!File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),
                    "WindowsPowerShell", "v1.0", "powershell.exe")))
                    throw new FileNotFoundException("Windows PowerShell을 찾을 수 없습니다.");
            }
        }

        public void Restart(ResetIdentity identity)
        {
            Launch(identity);
            UnityEngine.Application.Quit();
        }

        public void Launch(ResetIdentity identity)
        {
            using (var current = Process.GetCurrentProcess())
            {
                var executable = current.MainModule?.FileName;
                if (string.IsNullOrWhiteSpace(executable) || !File.Exists(executable))
                    throw new IOException("The running exhibition executable could not be located.");
                var directory = Path.GetDirectoryName(executable);
                var helper = Path.Combine(directory, "Exhibition-Relaunch.ps1");
                if (!File.Exists(helper))
                    throw new FileNotFoundException("The exhibition relaunch helper is missing.", helper);
                var powershell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),
                    "WindowsPowerShell", "v1.0", "powershell.exe");
                if (!File.Exists(powershell))
                    throw new FileNotFoundException("Windows PowerShell could not be located.", powershell);

                var arguments = new[]
                {
                    "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", helper,
                    "-AppId", identity.AppId.ToString(CultureInfo.InvariantCulture),
                    "-ParentId", current.Id.ToString(CultureInfo.InvariantCulture),
                    "-ParentStartTicks", current.StartTime.ToUniversalTime().Ticks.ToString(CultureInfo.InvariantCulture),
                    "-Executable", executable,
                    "-TimeoutSeconds", _timeoutSeconds.ToString(CultureInfo.InvariantCulture),
                };
                var encoded = new StringBuilder();
                foreach (var argument in arguments)
                {
                    if (encoded.Length > 0) encoded.Append(' ');
                    encoded.Append(QuoteArgument(argument));
                }
                var start = new ProcessStartInfo
                {
                    FileName = powershell,
                    Arguments = encoded.ToString(),
                    WorkingDirectory = directory,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                using (var helperProcess = Process.Start(start))
                {
                    if (helperProcess == null)
                        throw new IOException("The exhibition relaunch helper did not start.");
                }
            }
        }

        // Windows command-line argument encoding, not PowerShell expression interpolation.
        // -File receives these as literal script parameters (including $, spaces and apostrophes).
        private static string QuoteArgument(string value)
        {
            var result = new StringBuilder();
            result.Append('"');
            var slashes = 0;
            foreach (var character in value)
            {
                if (character == '\\') { slashes++; continue; }
                if (character == '"')
                {
                    result.Append('\\', slashes * 2 + 1);
                    result.Append('"');
                }
                else
                {
                    result.Append('\\', slashes);
                    result.Append(character);
                }
                slashes = 0;
            }
            result.Append('\\', slashes * 2);
            result.Append('"');
            return result.ToString();
        }
    }

    public sealed class CompletedParticipantResetOptions : ICompletedParticipantResetReturn
    {
        public string RequestPath { get; private set; }
        public string RequestHash { get; private set; }

        public static bool Present(string[] args)
        {
            if (args == null) return false;
            for (int i = 0; i < args.Length; i++)
                if (args[i] == "-j2mCompletedParticipantReset" || args[i] == "-j2mCompletedParticipantResetHash") return true;
            return false;
        }

        public static CompletedParticipantResetOptions Parse(string[] args)
        {
            var path = Find(args, "-j2mCompletedParticipantReset");
            var hash = Find(args, "-j2mCompletedParticipantResetHash");
            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(hash) || hash.Length != 64)
                throw new InvalidOperationException("Completed participant reset arguments are incomplete.");
            var request = ExperimentFiles.Read<ExperimentRequest>(path);
            if (ExperimentFiles.Hash(path) != hash) throw new IOException("Completed participant reset request changed.");
            CompletedResetProductWire.ValidateRequestPath(request, path);
            return new CompletedParticipantResetOptions { RequestPath = path, RequestHash = hash };
        }

        public void Validate(ExhibitionResetCoordinator coordinator)
        {
            var request = ExperimentFiles.Read<ExperimentRequest>(RequestPath);
            if (ExperimentFiles.Hash(RequestPath) != RequestHash) throw new IOException("Completed participant reset request changed.");
            CompletedResetProductWire.ValidateRequestPath(request, RequestPath);
            var record = coordinator.ReadRecord();
            var identity = coordinator.GetCurrentIdentity();
            if (record == null || record.State != ResetRecord.Ready || record.OperationId != request.OperationId ||
                record.AppId != identity.AppId || record.SteamId != identity.SteamId ||
                record.AppId != request.AppId || record.SteamId != request.SteamId)
                throw new InvalidOperationException("Completed participant reset account or Ready journal mismatch.");
            string consumed = RequestPath + ".consumed";
            using (var file = new FileStream(consumed, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(file, new UTF8Encoding(false)))
            {
                writer.Write(DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
                writer.Flush(); file.Flush(true);
            }
        }

        private static string Find(string[] args, string name)
        {
            if (args == null) return null;
            string value = null;
            for (int i = 0; i < args.Length; i++)
                if (args[i] == name)
                {
                    if (value != null || i + 1 >= args.Length || args[i + 1].StartsWith("-", StringComparison.Ordinal))
                        throw new InvalidOperationException("Duplicate or missing completed participant reset argument.");
                    value = args[++i];
                }
            return value;
        }
    }

    /// <summary>Runs the validated Steam client cycle only after Pending became Ready.</summary>
    public sealed class CompletedParticipantResetRestartAdapter : IParticipantRestart
    {
        private readonly string journalPath;

        public CompletedParticipantResetRestartAdapter(string journalPath) { this.journalPath = journalPath; }

        public void ValidateAvailable()
        {
            using (var current = Process.GetCurrentProcess())
            {
                string executable = current.MainModule?.FileName;
                string directory = Path.GetDirectoryName(executable);
                string tools = Path.Combine(directory, "RestartExperiment");
                foreach (string name in new[] { "Restart-Experiment.ps1", "RestartExperiment.cs", "RestartExperimentWindows.cs", "RestartExperimentNativeProbe.cs" })
                    if (!File.Exists(Path.Combine(tools, name))) throw new FileNotFoundException("Steam 재시작 구성 파일이 없습니다.", name);
                var dll = Path.Combine(directory, Path.GetFileNameWithoutExtension(executable) + "_Data", "Plugins", "x86_64", "steam_api64.dll");
                if (!File.Exists(dll))
                    throw new FileNotFoundException("Steam SDK 파일이 없습니다.");
                if (ExperimentFiles.Hash(dll) != ExperimentFiles.DllHash)
                    throw new IOException("Steam SDK 파일 버전이 재시작 helper와 일치하지 않습니다.");
                if (!File.Exists(ExperimentFiles.PowerShell)) throw new FileNotFoundException("Windows PowerShell을 찾을 수 없습니다.");
            }
        }

        public void Restart(ResetIdentity identity)
        {
            ValidateAvailable();
            using (var current = Process.GetCurrentProcess())
            {
                string executable = current.MainModule.FileName;
                string directory = Path.GetDirectoryName(executable);
                var parent = WindowsIdentityCapture.Capture(current, true);
                var steam = WindowsIdentityCapture.Steam(parent);
                if (steam == null) throw new InvalidOperationException("Steam client를 찾을 수 없습니다.");
                string operation = ExperimentFiles.Read<ProductReadyJournal>(journalPath).OperationId;
                string handoffDirectory = Path.Combine(Path.GetDirectoryName(journalPath), "participant-reset-handoff", operation);
                Directory.CreateDirectory(handoffDirectory);
                var requestPath = Path.Combine(handoffDirectory, "request.json");
                var request = new ExperimentRequest {
                    CompletedResetProduct = true, Nonce = Guid.NewGuid().ToString("N"), OperationId = operation,
                    AppId = identity.AppId, SteamId = identity.SteamId, Trial = Trial.FullCycle,
                    Parent = parent, Steam = steam, EvidenceDirectory = handoffDirectory,
                    ToolsDirectory = Path.Combine(directory, "RestartExperiment"),
                    DllPath = Path.Combine(directory, Path.GetFileNameWithoutExtension(executable) + "_Data", "Plugins", "x86_64", "steam_api64.dll"),
                    ReadyJournalPath = WindowsIdentityCapture.CanonicalPath(journalPath), ReadyJournalSha256 = ExperimentFiles.Hash(journalPath),
                    Build = UnityEngine.Application.buildGUID + "/" + UnityEngine.Application.version
                };
                CompletedResetProductWire.Validate(request);
                using (var file = new FileStream(requestPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var writer = new StreamWriter(file, new UTF8Encoding(false))) { writer.Write(ExperimentFiles.Json(request)); writer.Flush(); file.Flush(true); }
                var start = ExperimentFiles.HostStart(request.ToolsDirectory, requestPath, false);
                using (var helper = Process.Start(start))
                    if (helper == null) throw new IOException("Steam 재시작 helper를 시작하지 못했습니다.");
            }
            UnityEngine.Application.Quit();
        }
    }
}
