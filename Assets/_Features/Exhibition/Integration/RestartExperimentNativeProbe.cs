// Loaded only in the separate, owned x64 readiness probe. Never called by normal game startup.
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Game.Exhibition.RestartExperiment
{
    public static class NativeProbe
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadLibraryEx(string path, IntPtr reserved, uint flags);
        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr library, string name);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool FreeLibrary(IntPtr library);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int Init([Out] byte[] error);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void VoidCall();
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr InterfaceCall();
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate byte LoggedOnCall(IntPtr user);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate ulong SteamIdCall(IntPtr user);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate uint AppIdCall(IntPtr utils);

        private static T Export<T>(IntPtr library, string name) where T : class
        {
            IntPtr address = GetProcAddress(library, name);
            if (address == IntPtr.Zero) throw new MissingMethodException(name);
            return (T)(object)Marshal.GetDelegateForFunctionPointer(address, typeof(T));
        }

        private static void Fail(ProbeObservation result, string stage, Exception error)
        {
            string message = error.ToString();
            if (message.Length > 1024) message = message.Substring(0, 1024);
            if (result.Error == null) { result.Error = message; result.FailureStage = stage; }
            if (stage == "Query") result.QueryError = message;
            if (stage == "Shutdown") result.ShutdownError = message;
            if (stage == "Cleanup") result.CleanupError = message;
        }

        public static int CaptureInit(ProbeObservation result, Func<byte[], int> init)
        {
            var buffer = new byte[1024];
            int code = init(buffer);
            int length = Array.IndexOf(buffer, (byte)0);
            if (length < 0) length = 1023;
            result.InitDiagnostic = Encoding.UTF8.GetString(buffer, 0, Math.Min(1023, length));
            return code;
        }

        // False means leave the DLL loaded until this owned process exits.
        public static bool ObserveSession(ProbeObservation result, Func<int> init, Action query, Action shutdown)
        {
            bool initialized = false, releaseLibrary = true;
            string stage = "Init";
            try
            {
                releaseLibrary = false; result.InitCalled = true;
                result.InitResult = init(); result.InitReturned = true;
                result.InitDisposition = ProbeInitPolicy.Classify(result.InitResult, result.InitDiagnostic);
                initialized = result.InitResult == 0;
                if (!initialized)
                {
                    if (result.InitDisposition != ProbeInitDisposition.NoSteamClient &&
                        result.InitDisposition != ProbeInitDisposition.GlobalUserConnectionUnavailable) Fail(result, "Init", new InvalidOperationException("Steam Init failed: " +
                        (result.InitResult == 1 ? "FailedGeneric" : result.InitResult == 3 ? "VersionMismatch" : "UnknownResult") + " (" + result.InitResult + ")."));
                }
                else
                {
                    stage = "Query";
                    result.QueryCalled = true; query(); result.QueryReturned = true;
                }
            }
            catch (Exception e) { Fail(result, stage, e); }
            finally
            {
                if (initialized)
                {
                    try { result.ShutdownCalled = true; shutdown(); result.ShutdownReturned = true; releaseLibrary = true; }
                    catch (Exception e) { Fail(result, "Shutdown", e); }
                }
            }
            return releaseLibrary;
        }

        public static ProbeObservation Run(string requestPath)
        {
            var request = ExperimentFiles.Read<ExperimentRequest>(requestPath);
            if (request.Trial != Trial.FullCycle) throw new InvalidOperationException("Probe trial required.");
            new WindowsCycleEnvironment(request, requestPath).Validate();
            var steam = WindowsIdentityCapture.Steam(request.Parent);
            if (steam == null || WindowsIdentityCapture.SameProcess(steam, request.Steam) ||
                !string.Equals(steam.Path, request.Steam.Path, StringComparison.OrdinalIgnoreCase) || steam.Sha256 != request.Steam.Sha256)
                throw new InvalidOperationException("Probe requires a new client from the captured installation.");
            return RunValidated(request.Nonce, request.DllPath);

        }

        // Both entrypoints validate their own typed request before reaching this native-only core.
        public static ProbeObservation RunValidated(string nonce, string dllPath)
        {
            using (var process = Process.GetCurrentProcess())
            {
                var result = new ProbeObservation { Nonce = nonce, Pid = process.Id,
                    StartTicks = process.StartTime.ToUniversalTime().Ticks, InitResult = -1 };
                // LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR | LOAD_LIBRARY_SEARCH_DEFAULT_DIRS; no ambient DLL search.
                IntPtr library = LoadLibraryEx(WindowsIdentityCapture.CanonicalPath(dllPath), IntPtr.Zero, 0x1100);
                if (library == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
                bool releaseLibrary = true;
                try
                {
                    var init = Export<Init>(library, "SteamAPI_InitFlat");
                    var shutdown = Export<VoidCall>(library, "SteamAPI_Shutdown");
                    var callbacks = Export<VoidCall>(library, "SteamAPI_RunCallbacks");
                    var getUser = Export<InterfaceCall>(library, "SteamAPI_SteamUser_v023");
                    var getUtils = Export<InterfaceCall>(library, "SteamAPI_SteamUtils_v011");
                    var loggedOn = Export<LoggedOnCall>(library, "SteamAPI_ISteamUser_BLoggedOn");
                    var steamId = Export<SteamIdCall>(library, "SteamAPI_ISteamUser_GetSteamID");
                    var appId = Export<AppIdCall>(library, "SteamAPI_ISteamUtils_GetAppID");
                    // Never unload a possibly active native session; process exit owns failed cleanup.
                    releaseLibrary = false;
                    releaseLibrary = ObserveSession(result, () => CaptureInit(result, buffer => init(buffer)), () =>
                    {
                        callbacks(); // No registered handlers or achievement APIs.
                        IntPtr user = getUser(), utils = getUtils();
                        if (user == IntPtr.Zero || utils == IntPtr.Zero) throw new IOException("Steam interface unavailable.");
                        result.AppId = appId(utils); result.SteamId = steamId(user); result.LoggedOn = loggedOn(user) != 0;
                    }, () => shutdown());
                }
                finally
                {
                    if (releaseLibrary && !FreeLibrary(library))
                        Fail(result, "Cleanup", new Win32Exception(Marshal.GetLastWin32Error(), "Probe DLL release failed."));
                }
                return result;
            }
        }
    }
}
