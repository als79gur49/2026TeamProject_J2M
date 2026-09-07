using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace Game.Exhibition.Integration
{
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
}
