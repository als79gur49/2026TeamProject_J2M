// Harmless Windows processes and real runtime-2 IPC. Never loads Steam or game/native code.
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Game.Exhibition.RestartExperiment;
public static class Runtime2Fakes
{
    static void Check(bool ok, string why) { if (!ok) throw new Exception(why); }
    public static int Main(string[] args)
    {
        try { Run(args).GetAwaiter().GetResult(); return 0; }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
    static async Task Run(string[] args)
    {
        if (args[0] == "handoff-child") { await Runtime2HandoffFakes.Child(args[1]); return; }
        if (args[0] == "handoff-helper") { Runtime2HandoffFakes.Helper(args[1]); return; }
        if (args[0] == "role")
        {
            Console.WriteLine(ObservationV3Wire.Serialize(Environment.GetCommandLineArgs()));
            Console.Out.Flush(); Console.ReadLine(); return;
        }
        if (args[0] == "pipe")
        {
            using (var token = new CancellationTokenSource(10000))
            using (var p = await ObservationV3Pipe.ConnectAsync(args[1], 10000, token.Token))
            {
                p.Runtime2 = args[2] != "legacy";
                await p.SendAsync("visibility-report", "opened", token.Token);
                if (args[2] == "legacy") return;
                Check(await p.ReceiveAsync<string>("report-stored", token.Token) == "stored", "store acknowledgement");
                await p.SendAsync("exit-request", "exit", token.Token);
                Check(await p.ReceiveAsync<string>("exit-authorized", token.Token) == "authorized", "exit authorization");
            }
            return;
        }
        string root = ObservationV3RuntimeWire.EvidenceRoot(args[0]);
        string exe = Process.GetCurrentProcess().MainModule.FileName;
        using (var handles = new ObservationV3ProcessHandles())
        {
            var self = handles.Capture(Process.GetCurrentProcess().Id);
            var roles = new[] { "origin", "helper", "old-client", "new-client", "probe", "launcher", "actual-child" };
            var children = new System.Collections.Generic.List<Process>();
            try
            {
                foreach (string role in roles)
                {
                    string[] tokens = { "role", role, "space value", "quote\"value", "trailing slash\\", "한글", "" };
                    var p = Process.Start(new ProcessStartInfo { FileName = exe, Arguments = string.Join(" ", tokens.Select(ObservationV3Arguments.Quote)),
                        UseShellExecute = false, RedirectStandardInput = true, RedirectStandardOutput = true, CreateNoWindow = true });
                    children.Add(p); var id = handles.Capture(p.Id);
                    handles.Match(id);
                    var actual = ObservationV3Wire.Parse<string[]>(p.StandardOutput.ReadLine());
                    Check(actual.Skip(1).SequenceEqual(tokens), "Windows quoting: " + role);
                    File.WriteAllText(Path.Combine(root, role + "-identity.json"), ObservationV3Wire.Serialize(id));
                }
                Check(children.Select(p => p.Id).Distinct().Count() == roles.Length, "independent role identities");
                foreach (var p in children)
                {
                    var id = handles.Capture(p.Id); p.StandardInput.WriteLine("exit");
                    Check(p.WaitForExit(10000) && p.ExitCode == 0 && handles.Exited(id), "retained handle exit");
                }
            }
            finally { foreach (var p in children) { try { p.StandardInput.WriteLine("exit"); } catch { } p.Dispose(); } }
            foreach (string mode in new[] { "normal", "legacy" })
            using (var token = new CancellationTokenSource(10000))
            {
                string endpoint = "j2m-observation-v3-" + Guid.NewGuid().ToString("N");
                using (var pipe = ObservationV3Pipe.Listen(endpoint, self.UserSid))
                using (var child = Process.Start(new ProcessStartInfo { FileName = exe, Arguments = "pipe " + endpoint + " " + mode, UseShellExecute = false, CreateNoWindow = true }))
                {
                    pipe.Runtime2 = true; var id = handles.Capture(child.Id); await pipe.WaitAsync(token.Token);
                    Check(ObservationV3Wire.Same(id, pipe.CapturePeer()), "external pipe peer");
                    if (mode == "legacy")
                    {
                        bool rejected = false; try { await pipe.ReceiveFrameAsync(token.Token); } catch (IOException) { rejected = true; }
                        Check(rejected, "legacy peer accepted");
                    }
                    else
                    {
                        Check(await pipe.ReceiveAsync<string>("visibility-report", token.Token) == "opened", "report pump");
                        await pipe.SendAsync("report-stored", "stored", token.Token);
                        Check(await pipe.ReceiveAsync<string>("exit-request", token.Token) == "exit", "explicit exit");
                        await pipe.SendAsync("exit-authorized", "authorized", token.Token);
                        bool eof = false; try { await pipe.ReceiveFrameAsync(token.Token); } catch (EndOfStreamException) { eof = true; }
                        Check(eof, "bounded EOF observation");
                    }
                    Check(child.WaitForExit(10000) && child.ExitCode == 0 && handles.Exited(id), "pipe child exit");
                }
            }
        }
        string e2e = Path.Combine(root, Guid.NewGuid().ToString("N")); Directory.CreateDirectory(e2e);
        File.WriteAllText(Path.Combine(e2e, "binding.txt"), "Harmless fake binding; no live eligibility");
        using (var owned = new ObservationV3ProcessHandles())
        using (var helperProcess = Process.Start(new ProcessStartInfo { FileName = exe, Arguments = "handoff-helper " + ObservationV3Arguments.Quote(e2e), UseShellExecute = false, CreateNoWindow = true }))
        {
            var id = owned.Capture(helperProcess.Id);
            Check(helperProcess.WaitForExit(20000) && helperProcess.ExitCode == 0 && owned.Exited(id), "external helper E2E exit");
            var terminal = ObservationV3RuntimeWire.ReadTerminal(e2e, new DirectoryInfo(e2e).Name);
            Check(terminal.Outcome == "Completed" && terminal.PurposeAchieved && terminal.ChildExit == "Confirmed", "fake E2E terminal");
        }
        File.WriteAllText(Path.Combine(root, "result.json"), "{\"Passed\":true,\"RoleProcesses\":7,\"PipeCases\":2,\"SteamNativeCalls\":0,\"Scope\":\"Harmless OS boundaries only; no live launch proof\"}");
        Console.WriteLine("PASS runtime-2 Windows roles, argv, retained handles, framed reports/exit, legacy rejection");
    }
}
