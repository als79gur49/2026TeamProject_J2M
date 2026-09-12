[CmdletBinding()]
param([string]$RepositoryRoot = '')
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
if (-not $RepositoryRoot) { $RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path }
$source = Join-Path $RepositoryRoot 'Assets/_Features/Exhibition/Integration'
$script:passed = 0
function Assert-True($Value, [string]$Message) { if (-not $Value) { throw $Message } }
function Invoke-Case([string]$Name, [scriptblock]$Body) {
    & $Body
    $script:passed++
    Write-Output "PASS $Name"
}
# Run the real host in cold PowerShell, including when every Add-Type is blocked.
# Exercise the actual catch/console fallback without popup or native calls.
$hostPath = Join-Path $RepositoryRoot 'Assets/_Features/Exhibition/Tools/Restart-Experiment.ps1'
foreach ($case in @(
    @{Name='missing request'; Json=$null; Error='ItemNotFoundException'; Known=$false},
    @{Name='malformed JSON'; Json='{'; Error='ArgumentException'; Known=$false},
    @{Name='partial request'; Json='{}'; Error='injected Add-Type unavailable'; Known=$false},
    @{Name='unvalidated role before compiler'; Json='{"ResetOverlayChildRole":999}'; Error='injected Add-Type unavailable'; Known=$false},
    @{Name='C# bootstrap unavailable'; Json='{"EvidenceDirectory":"EVIDENCE"}'; Error='injected Add-Type unavailable'; Known=$false},
    @{Name='invalid role after compilation'; Json='{"ResetOverlayChildRole":999}'; Error='Completed-reset product request required.'; Known=$false; Compile=$true},
    @{Name='environment validation after compilation'; Json='{}'; Error='Completed-reset product request required.'; Known=$false; Compile=$true}
)) {
    Invoke-Case ('Actual host stop guidance with ' + $case.Name) {
        $scratch = Join-Path 'D:\J2M\evidence\participant-restart-preflight' ('guidance-fake-' + [guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $scratch | Out-Null
        $requestPath = Join-Path $scratch 'request.json'
        if ($null -ne $case.Json) {
            [IO.File]::WriteAllText($requestPath, $case.Json.Replace('EVIDENCE', $scratch.Replace('\','\\')))
        }
        $testHost = $hostPath
        $addTypeStub = "function Add-Type { throw 'injected Add-Type unavailable' }"
        if ($case.Compile) {
            # Compile exact helper sources, but only run requests guaranteed to fail
            # Validate() before cycle lock acquisition or process/native operations.
            Copy-Item -LiteralPath $hostPath -Destination $scratch
            foreach ($name in @('RestartExperiment.cs', 'RestartExperimentWindows.cs', 'RestartExperimentNativeProbe.cs')) {
                Copy-Item -LiteralPath (Join-Path $source $name) -Destination $scratch
            }
            $testHost = Join-Path $scratch 'Restart-Experiment.ps1'
            $addTypeStub = @'
function Add-Type {
    param($AssemblyName, $Path, $ReferencedAssemblies)
    if ($AssemblyName -eq 'System.Windows.Forms') { throw 'injected Forms unavailable' }
    if ($Path) { Microsoft.PowerShell.Utility\Add-Type -Path $Path -ReferencedAssemblies $ReferencedAssemblies }
    else { Microsoft.PowerShell.Utility\Add-Type -AssemblyName $AssemblyName }
}
'@
        }
        $body = $addTypeStub + "`n& '" + $testHost.Replace("'", "''") + "' -RequestPath '" + $requestPath.Replace("'", "''") + "'`nexit `$LASTEXITCODE"
        $start = New-Object Diagnostics.ProcessStartInfo
        $start.FileName = Join-Path $PSHOME 'powershell.exe'
        $start.Arguments = '-NoProfile -EncodedCommand ' + [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($body))
        $start.UseShellExecute = $false; $start.CreateNoWindow = $true
        $start.RedirectStandardOutput = $true; $start.RedirectStandardError = $true
        $child = [Diagnostics.Process]::Start($start)
        try {
            $output = $child.StandardOutput.ReadToEndAsync(); $errors = $child.StandardError.ReadToEndAsync()
            Assert-True ($child.WaitForExit(10000)) 'Cold failure host did not exit'
            $text = $output.Result + $errors.Result
            Assert-True ($child.ExitCode -eq 1) 'Failure host did not preserve nonzero exit'
            Assert-True ($text.Contains($case.Error)) ('Original error lost: ' + $text)
            Assert-True ($text.Contains('Participant restart stopped.')) ('Bootstrap failure must not infer purpose: ' + $text)
            Assert-True ($text.Contains('Keep the game closed and check the failure before another restart.')) 'Missing stop-only guidance'
            Assert-True (-not $text.Contains('start the game manually')) 'Unsafe manual restart guidance remains'
            $expectedEvidence = $(if ($case.Known) { $scratch } else { 'unknown (not obtained)' })
            Assert-True ($text.Contains('Handoff: ' + $expectedEvidence)) 'Known/unknown evidence path missing'
            Assert-True (-not (Test-Path (Join-Path $scratch 'restart-failure.txt'))) 'Unvalidated bootstrap created an error file'
        } finally { if (-not $child.HasExited) { $child.Kill(); $child.WaitForExit() }; $child.Dispose() }
    }
}

# Extract actual production functions rather than maintaining a second formatter.
$tokens = $null; $parseErrors = $null
$hostAst = [Management.Automation.Language.Parser]::ParseFile($hostPath, [ref]$tokens, [ref]$parseErrors)
Assert-True ($parseErrors.Count -eq 0) 'Host script parse failed'
foreach ($name in @('Get-RestartFailureDetails', 'Save-RestartFailure', 'Format-RestartExperimentFailure', 'Show-RestartExperimentFailure')) {
    $functionAst = $hostAst.Find({ param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq $name }, $false)
    Assert-True ($null -ne $functionAst) ('Production failure seam missing: ' + $name)
    . ([scriptblock]::Create($functionAst.Extent.Text))
}
foreach ($failure in @('environment validation failed', 'worker creation uncertain', 'FullCycle failed')) {
    Invoke-Case ('Actual display seam preserves ' + $failure) {
        $script:displayedFailure = $null
        $error = New-Object InvalidOperationException($failure)
        Show-RestartExperimentFailure -Failure $error -EvidenceDirectory 'D:\J2M\evidence\known' -Purpose Reset -Display {
            param($message) $script:displayedFailure = $message
        }
        Assert-True ($script:displayedFailure.Contains($failure)) 'Display lost original error'
        Assert-True ($script:displayedFailure.Contains('Participant restart stopped.')) 'Display lost restart failure'
        Assert-True ($script:displayedFailure.Contains('Keep the game closed and check the failure before another restart.')) 'Display offered recovery'
        Assert-True ($script:displayedFailure.Contains('Handoff: D:\J2M\evidence\known')) 'Display lost evidence path'
    }
}
Invoke-Case 'Actual display failure does not throw or replace the original error' {
    $failure = New-Object InvalidOperationException('original cycle failure')
    Show-RestartExperimentFailure -Failure $failure -EvidenceDirectory $null -Display { throw 'display unavailable' }
    Assert-True ($failure.Message -ceq 'original cycle failure') 'Original error changed'
    $message = Format-RestartExperimentFailure -Failure $failure -EvidenceDirectory $null
    Assert-True ($message.Contains('Handoff: unknown (not obtained)')) 'Unknown path not explicit'
}

Invoke-Case 'Failure details preserve native code and both operation contexts' {
    $primary = New-Object ComponentModel.Win32Exception(5)
    $primary.Data['RestartOperation'] = 'StartSteam.CaptureNewSteamIdentity'
    $primary.Data['NativeOperation'] = 'OpenProcessToken'
    $secondary = New-Object IO.IOException('cleanup sentinel')
    $secondary.Data['RestartOperation'] = 'Cleanup'
    $aggregate = New-Object AggregateException('cycle', ([Exception[]]@($primary, $secondary)))
    $details = Get-RestartFailureDetails $aggregate
    Assert-True ($details.IndexOf('StartSteam.CaptureNewSteamIdentity') -lt $details.IndexOf('RestartOperation: Cleanup')) 'Cleanup preceded primary failure'
    foreach ($text in @('NativeErrorCode: 5', 'OpenProcessToken', 'StartSteam.CaptureNewSteamIdentity', 'Cleanup', 'cleanup sentinel')) {
        Assert-True ($details.Contains($text)) ('Missing failure detail: ' + $text)
    }
}
Invoke-Case 'Account redaction precedes truncation and leaves numeric metadata intact' {
    $account = '76561198123456789'
    $failure = New-Object IO.IOException(('x' * 990) + $account + ' end')
    $details = Get-RestartFailureDetails $failure @($account)
    Assert-True (-not $details.Contains($account.Substring(0, 10))) 'Partial account survived truncation'
    $native = New-Object ComponentModel.Win32Exception(100)
    $native.Data['TargetPid'] = 1000
    $zero = Get-RestartFailureDetails $native @('0', '100')
    Assert-True ($zero.Contains('NativeErrorCode: 100')) 'Native error metadata was redacted'
    Assert-True ($zero.Contains('TargetPid: 1000')) 'PID metadata was redacted'
    Assert-True ($zero.Contains('HResult: ' + $native.HResult)) 'HResult metadata was redacted'
    Assert-True ($zero -match 'UTC: \d{4}-\d{2}-\d{2}T') 'UTC metadata was redacted'
}
Invoke-Case 'Failure record is bounded create-only and independent of restart state' {
    $scratch = Join-Path 'D:\J2M\evidence\restart-failure-hardening' ([guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $scratch -Force | Out-Null
    $failure = New-Object IO.IOException(('private-account ' * 4000))
    $details = Get-RestartFailureDetails $failure @('private-account')
    Assert-True (-not $details.Contains('private-account')) 'Account leaked'
    Assert-True ([Text.Encoding]::UTF8.GetByteCount($details) -le 32768) 'Details unbounded'
    $result = Save-RestartFailure $scratch $details
    Assert-True ($result.StartsWith('Failure record:')) 'File not saved'
    $path = Join-Path $scratch 'restart-failure.txt'
    $before = [IO.File]::ReadAllText($path)
    Assert-True ((Save-RestartFailure $scratch 'replacement').Contains('unavailable')) 'Existing record overwritten'
    Assert-True ([IO.File]::ReadAllText($path) -ceq $before) 'Original record changed'
    Assert-True ((Save-RestartFailure $null $details).Contains('not validated')) 'Unvalidated path accepted'
    Assert-True ((Save-RestartFailure (Join-Path $scratch 'missing') $details).Contains('unavailable')) 'Write failure not isolated'
    Assert-True (-not (Test-Path (Join-Path $scratch 'missing'))) 'Fallback directory created'
}

Invoke-Case 'Cold copied helper accepts v2 and rejects incompatible completed mappings' {
    $scratch = Join-Path 'D:\J2M\evidence\participant-restart-preflight' ('mapping-contract-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $scratch | Out-Null
    foreach ($name in @('RestartExperiment.cs', 'RestartExperimentWindows.cs', 'RestartExperimentNativeProbe.cs')) {
        Copy-Item -LiteralPath (Join-Path $source $name) -Destination $scratch
    }
    # The child loads only the three shipped sources and BCL assemblies, never Unity/application DLLs.
    # Only the wire validator runs: no Steam client, game process, or native API calls.
    $body = @'
$ErrorActionPreference = 'Stop'
$root = '__ROOT__'
Add-Type -AssemblyName System.Runtime.Serialization
Add-Type -Path @(
    (Join-Path $root 'RestartExperiment.cs'),
    (Join-Path $root 'RestartExperimentWindows.cs'),
    (Join-Path $root 'RestartExperimentNativeProbe.cs')
) -ReferencedAssemblies @('System.dll', 'System.Core.dll', 'System.Xml.dll', 'System.Runtime.Serialization.dll')
foreach ($mapping in @('level-and-efficient-clear-v2', 'level-clear-v1', 'unknown-mapping')) {
    $journal = New-Object Game.Exhibition.RestartExperiment.ProductReadyJournal
    $journal.SchemaVersion = 1
    $journal.OperationId = [guid]::NewGuid().ToString('N')
    $journal.State = 'Ready'; $journal.MappingVersion = $mapping
    $journal.AppId = 123; $journal.SteamId = 456
    $journalPath = Join-Path $root 'exhibition-reset.json'
    [IO.File]::WriteAllText($journalPath, [Game.Exhibition.RestartExperiment.ExperimentFiles]::Json($journal))
    $request = New-Object Game.Exhibition.RestartExperiment.ExperimentRequest
    $request.CompletedResetProduct = $true
    $request.OperationId = $journal.OperationId
    $request.Trial = [Game.Exhibition.RestartExperiment.Trial]::FullCycle
    $request.AppId = 123; $request.SteamId = 456
    $request.ReadyJournalPath = [Game.Exhibition.RestartExperiment.WindowsIdentityCapture]::CanonicalPath($journalPath)
    $request.ReadyJournalSha256 = [Game.Exhibition.RestartExperiment.ExperimentFiles]::Hash($journalPath)
    $before = [IO.File]::ReadAllText($journalPath)
    $failure = $null
    try { [Game.Exhibition.RestartExperiment.CompletedResetProductWire]::Validate($request) }
    catch { $failure = $_.Exception.ToString() }
    if ($mapping -eq 'level-and-efficient-clear-v2') {
        if ($null -ne $failure) { throw ('Current mapping rejected: ' + $failure) }
    } else {
        if ($null -eq $failure -or -not $failure.Contains('achievement mapping is incompatible')) {
            throw ('Incompatible mapping was not rejected by the mapping guard: ' + $mapping + ' ' + $failure)
        }
    }
    if ([IO.File]::ReadAllText($journalPath) -cne $before) { throw 'Wire validation changed the journal' }
}
Write-Output 'MAPPING_CONTRACT_OK'
'@
    $body = $body.Replace('__ROOT__', $scratch.Replace("'", "''"))
    $start = New-Object Diagnostics.ProcessStartInfo
    $start.FileName = Join-Path $PSHOME 'powershell.exe'
    $start.Arguments = '-NoProfile -EncodedCommand ' + [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($body))
    $start.UseShellExecute = $false; $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true; $start.RedirectStandardError = $true
    $child = [Diagnostics.Process]::Start($start)
    try {
        $output = $child.StandardOutput.ReadToEndAsync(); $errors = $child.StandardError.ReadToEndAsync()
        Assert-True ($child.WaitForExit(30000)) 'Cold mapping contract timed out'
        $text = $output.Result + $errors.Result
        Assert-True ($child.ExitCode -eq 0 -and $text.Contains('MAPPING_CONTRACT_OK')) ('Cold mapping contract failed: ' + $text)
    } finally {
        if (-not $child.HasExited) { $child.Kill(); $child.WaitForExit() }
        $child.Dispose()
    }
}

Add-Type -Path @(
    (Join-Path $source 'RestartExperiment.cs'),
    (Join-Path $source 'RestartExperimentWindows.cs'),
    (Join-Path $source 'RestartExperimentNativeProbe.cs'),
    (Join-Path $PSScriptRoot 'Restart-Experiment.Fakes.cs')
) -ReferencedAssemblies @('System.dll', 'System.Core.dll', 'System.Xml.dll', 'System.Runtime.Serialization.dll')


function New-FakeProbe([string]$Body) {
    $Body = "`$ProgressPreference = 'SilentlyContinue'`n`$null = [Console]::In.ReadLine()`n" + $Body
    $start = New-Object System.Diagnostics.ProcessStartInfo
    $start.FileName = [Game.Exhibition.RestartExperiment.ExperimentFiles]::PowerShell
    $start.Arguments = '-NoProfile -EncodedCommand ' + [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($Body))
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.RedirectStandardInput = $true
    return $start
}
$expected = New-Object Game.Exhibition.RestartExperiment.ExperimentRequest
$expected.Nonce = 'fake-probe-nonce'
$expected.AppId = 123
$expected.SteamId = 456
$success = @'
$p = [Diagnostics.Process]::GetCurrentProcess()
@{ WireVersion=3; InitDisposition=0; InitDiagnostic=$null; InitCalled=$true; InitReturned=$true; QueryCalled=$true; QueryReturned=$true; ShutdownCalled=$true; FailureStage=$null; QueryError=$null; ShutdownError=$null; CleanupError=$null; Nonce='fake-probe-nonce'; Pid=$p.Id; StartTicks=$p.StartTime.ToUniversalTime().Ticks;
   AppId=123; SteamId=456; LoggedOn=$true; ShutdownReturned=$true; InitResult=0; Error=$null } | ConvertTo-Json -Compress
'@

Invoke-Case 'PowerShell compiles exact helper sources without native Init' {
    Assert-True ([Game.Exhibition.RestartExperiment.ExperimentFiles]::DllHash.Length -eq 64) 'Missing SDK hash'
}
Invoke-Case 'Windows token, logon, canonical path and PID/start identity' {
    $p = [Diagnostics.Process]::GetCurrentProcess()
    try {
        $identity = [Game.Exhibition.RestartExperiment.WindowsIdentityCapture]::Capture($p, $true)
        Assert-True ($identity.Pid -eq $PID -and $identity.StartTicks -gt 0 -and $identity.Logon.Length -eq 16) 'Identity mismatch'
        Assert-True ($identity.Session -eq $p.SessionId) 'Native session differs from Windows .NET session'
        Assert-True ($identity.Path -eq [Game.Exhibition.RestartExperiment.WindowsIdentityCapture]::CanonicalPath($identity.Path)) 'Path mismatch'
    } finally { $p.Dispose() }
}
Invoke-Case 'Real harmless probe success requires process exit and result identity' {
    $ready = [Game.Exhibition.RestartExperiment.WindowsCycleEnvironment]::RunOwnedProbe((New-FakeProbe $success), 10000, $expected)
    Assert-True $ready 'Probe not ready'
}
Invoke-Case 'NoSteamClient requires clean process exit before a fresh successful observation' {
    $cold = $success.Replace('InitDisposition=0', 'InitDisposition=1').Replace('InitResult=0', 'InitResult=2').Replace('QueryCalled=$true', 'QueryCalled=$false').Replace('QueryReturned=$true', 'QueryReturned=$false').Replace('ShutdownCalled=$true', 'ShutdownCalled=$false').Replace('ShutdownReturned=$true', 'ShutdownReturned=$false').Replace('LoggedOn=$true', 'LoggedOn=$false').Replace('AppId=123', 'AppId=0').Replace('SteamId=456', 'SteamId=0')
    $timer = [Diagnostics.Stopwatch]::StartNew()
    $ready = [Game.Exhibition.RestartExperiment.WindowsCycleEnvironment]::RunOwnedProbe((New-FakeProbe ($cold + "`nStart-Sleep -Milliseconds 300")), 10000, $expected)
    Assert-True (-not $ready -and $timer.ElapsedMilliseconds -ge 300) 'NoSteamClient returned before child exit'
    $ready = [Game.Exhibition.RestartExperiment.WindowsCycleEnvironment]::RunOwnedProbe((New-FakeProbe $success), 10000, $expected)
    Assert-True $ready 'Fresh successful observation did not become ready'
}
$globalUser = $success.Replace('InitDisposition=0', 'InitDisposition=2').Replace('InitResult=0', 'InitResult=1').Replace('InitDiagnostic=$null', "InitDiagnostic='ConnectToGlobalUser failed.'").Replace('QueryCalled=$true', 'QueryCalled=$false').Replace('QueryReturned=$true', 'QueryReturned=$false').Replace('ShutdownCalled=$true', 'ShutdownCalled=$false').Replace('ShutdownReturned=$true', 'ShutdownReturned=$false').Replace('LoggedOn=$true', 'LoggedOn=$false').Replace('AppId=123', 'AppId=0').Replace('SteamId=456', 'SteamId=0')
Invoke-Case 'Global user unavailable permits only a fresh observation after owned process exit' {
    $ready = [Game.Exhibition.RestartExperiment.WindowsCycleEnvironment]::RunOwnedProbe((New-FakeProbe $globalUser), 10000, $expected)
    Assert-True (-not $ready) 'Failed Init became ready'
    $ready = [Game.Exhibition.RestartExperiment.WindowsCycleEnvironment]::RunOwnedProbe((New-FakeProbe $success), 10000, $expected)
    Assert-True $ready 'Fresh ready result rejected'
}
foreach ($case in @(
    @{Name='wire2'; Body=$globalUser.Replace('WireVersion=3', 'WireVersion=2')},
    @{Name='missing disposition'; Body=$globalUser.Replace('InitDisposition=2;', '')},
    @{Name='mismatched disposition'; Body=$globalUser.Replace('InitDisposition=2', 'InitDisposition=1')},
    @{Name='different diagnostic'; Body=$globalUser.Replace('ConnectToGlobalUser failed.', 'ConnectToGlobalUser failed. ')},
    @{Name='nonzero exit'; Body=($globalUser + "`nexit 7")},
    @{Name='stderr'; Body=($globalUser + "`n[Console]::Error.Write('fatal')")}
)) {
    Invoke-Case ('Global user observation rejects ' + $case.Name) {
        $rejected = $false
        try { [Game.Exhibition.RestartExperiment.WindowsCycleEnvironment]::RunOwnedProbe((New-FakeProbe $case.Body), 10000, $expected) | Out-Null }
        catch { $rejected = $true }
        Assert-True $rejected 'Invalid unavailable result accepted'
    }
}
Invoke-Case 'Identity mismatch returns not-ready after clean shutdown' {
    $ready = [Game.Exhibition.RestartExperiment.WindowsCycleEnvironment]::RunOwnedProbe((New-FakeProbe ($success.Replace('SteamId=456', 'SteamId=789'))), 10000, $expected)
    Assert-True (-not $ready) 'Mismatched identity accepted'
}
foreach ($case in @(
    @{Name='wrong nonce'; Body=$success.Replace('fake-probe-nonce','stale')},
    @{Name='wrong PID'; Body=$success.Replace('Pid=$p.Id','Pid=1')},
    @{Name='shutdown missing'; Body=$success.Replace('ShutdownReturned=$true','ShutdownReturned=$false')},
    @{Name='required init result missing'; Body=$success.Replace('InitResult=0;', '')},
    @{Name='multiple JSON results'; Body=($success + "`n" + $success)},
    @{Name='nonzero exit'; Body=($success + "`nexit 7")},
    @{Name='stderr'; Body=($success + "`n[Console]::Error.WriteLine('fake failure')")}
)) {
    Invoke-Case $case.Name {
        $rejected = $false
        try { [Game.Exhibition.RestartExperiment.WindowsCycleEnvironment]::RunOwnedProbe((New-FakeProbe $case.Body), 10000, $expected) | Out-Null }
        catch { $rejected = $true }
        Assert-True $rejected 'Invalid probe accepted'
    }
}
Invoke-Case 'Timeout kills only owned harmless probe and returns without retry' {
    $timer = [Diagnostics.Stopwatch]::StartNew()
    $rejected = $false
    try { [Game.Exhibition.RestartExperiment.WindowsCycleEnvironment]::RunOwnedProbe((New-FakeProbe 'Start-Sleep -Seconds 30'), 200, $expected) | Out-Null }
    catch { $rejected = $_.Exception.ToString().Contains('Probe timed out') }
    Assert-True ($rejected -and $timer.ElapsedMilliseconds -lt 5000) 'Timeout supervision failed'
}

Invoke-Case 'Real mutex rejects another process sharing the same cycle scope' {
    $p = [Diagnostics.Process]::GetCurrentProcess()
    try { $identity = [Game.Exhibition.RestartExperiment.WindowsIdentityCapture]::Capture($p, $false) } finally { $p.Dispose() }
    $request = New-Object Game.Exhibition.RestartExperiment.ExperimentRequest
    $request.Steam = $identity
    $environment = New-Object Game.Exhibition.RestartExperiment.WindowsCycleEnvironment($request, '')
    $lease = $environment.AcquireCycleLock()
    try {
        $name = [Game.Exhibition.RestartExperiment.WindowsIdentityCapture]::LockName($identity)
        $body = "`$m = New-Object Threading.Mutex(`$false, '$name'); try { [Console]::Out.Write(`$m.WaitOne(0)) } finally { `$m.Dispose() }"
        $start = New-FakeProbe $body
        $child = [Diagnostics.Process]::Start($start)
        try {
            $child.StandardInput.WriteLine('test'); $child.StandardInput.Close()
            $result = $child.StandardOutput.ReadToEndAsync()
            Assert-True ($child.WaitForExit(5000)) 'Mutex child did not exit'
            Assert-True ($result.Result -ceq 'False') 'Concurrent mutex acquired'
        } finally { if (-not $child.HasExited) { $child.Kill(); $child.WaitForExit() }; $child.Dispose() }
    } finally { $lease.Dispose() }
}

Invoke-Case 'Reused parent PID with different start ticks is already exited' {
    $p = [Diagnostics.Process]::GetCurrentProcess()
    try { $identity = [Game.Exhibition.RestartExperiment.WindowsIdentityCapture]::Capture($p, $false) } finally { $p.Dispose() }
    $request = New-Object Game.Exhibition.RestartExperiment.ExperimentRequest
    $request.Parent = $identity
    $environment = New-Object Game.Exhibition.RestartExperiment.WindowsCycleEnvironment($request, '')
    Assert-True ($environment.ParentAlive()) 'Current parent missing'
    $request.Parent.StartTicks++
    Assert-True (-not $environment.ParentAlive()) 'Reused PID treated as parent'
}

Invoke-Case 'Diagnostic probe script rejects invalid request before SDK or Steam process access' {
    $scratch = Join-Path 'D:\J2M\evidence\participant-restart-preflight' ('script-fake-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $scratch | Out-Null
    foreach ($name in @('RestartExperiment.cs', 'RestartExperimentWindows.cs', 'RestartExperimentNativeProbe.cs')) {
        Copy-Item -LiteralPath (Join-Path $source $name) -Destination $scratch
    }
    Copy-Item -LiteralPath (Join-Path $RepositoryRoot 'Assets/_Features/Exhibition/Tools/Restart-Experiment.ps1') -Destination $scratch
    $fakeRequest = Join-Path $scratch 'invalid-request.json'
    [IO.File]::WriteAllText($fakeRequest, '{"Nonce":"fake-probe-nonce","Trial":3}')
    $start = New-FakeProbe ''
    $start.Arguments = '-NoProfile -ExecutionPolicy Bypass -File ' + [Game.Exhibition.RestartExperiment.ExperimentFiles]::Quote((Join-Path $scratch 'Restart-Experiment.ps1')) + ' -RequestPath ' + [Game.Exhibition.RestartExperiment.ExperimentFiles]::Quote($fakeRequest) + ' -Probe'
    $probeExpected = New-Object Game.Exhibition.RestartExperiment.ExperimentRequest
    $probeExpected.Nonce = $expected.Nonce
    $probeExpected.AppId = $expected.AppId; $probeExpected.SteamId = $expected.SteamId
    $probeExpected.EvidenceDirectory = $scratch
    $rejected = $false
    try { [Game.Exhibition.RestartExperiment.WindowsCycleEnvironment]::RunOwnedProbe($start, 10000, $probeExpected) | Out-Null }
    catch {
        $rejected = $_.Exception.ToString().Contains('ExitCode=1') -and
            [IO.File]::ReadAllText((Join-Path $scratch 'probe-attempt-001.stderr.txt')).Contains('Completed-reset product request required.') -and
            [IO.File]::ReadAllText((Join-Path $scratch 'probe-attempt-001.json')).Contains('SkippedBecauseProcessOrOutputFailed')
    }
    Assert-True $rejected 'Script did not reach guarded validation'
}

Invoke-Case 'Unexpected supervisor exit closes job and terminates only its harmless probe' {
    $scratch = Join-Path 'D:\J2M\evidence\participant-restart-preflight' ('job-fake-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $scratch | Out-Null
    $marker = (Join-Path $scratch 'owned-child.txt').Replace("'", "''")
    $probeBody = @"
`$null = [Console]::In.ReadLine()
`$p = [Diagnostics.Process]::GetCurrentProcess()
[IO.File]::WriteAllText('$marker', (`$p.Id.ToString() + '|' + `$p.StartTime.ToUniversalTime().Ticks))
Start-Sleep -Seconds 30
"@
    $probeEncoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($probeBody))
    $quotedSource = $source.Replace("'", "''")
    $supervisorBody = @"
Add-Type -Path @('$quotedSource\RestartExperiment.cs', '$quotedSource\RestartExperimentWindows.cs', '$quotedSource\RestartExperimentNativeProbe.cs') -ReferencedAssemblies @('System.dll','System.Core.dll','System.Xml.dll','System.Runtime.Serialization.dll')
`$expected = New-Object Game.Exhibition.RestartExperiment.ExperimentRequest
`$expected.Nonce = 'job-test'
`$start = New-Object Diagnostics.ProcessStartInfo
`$start.FileName = [Game.Exhibition.RestartExperiment.ExperimentFiles]::PowerShell
`$start.Arguments = '-NoProfile -EncodedCommand $probeEncoded'
`$start.UseShellExecute = `$false; `$start.CreateNoWindow = `$true
`$start.RedirectStandardInput = `$true; `$start.RedirectStandardOutput = `$true; `$start.RedirectStandardError = `$true
[Game.Exhibition.RestartExperiment.WindowsCycleEnvironment]::RunOwnedProbe(`$start, 10000, `$expected)
"@
    $supervisor = [Diagnostics.Process]::Start((New-FakeProbe $supervisorBody))
    $owned = $null
    try {
        $errors = $supervisor.StandardError.ReadToEndAsync()
        $supervisor.StandardInput.WriteLine('test'); $supervisor.StandardInput.Close()
        $timer = [Diagnostics.Stopwatch]::StartNew()
        while (-not [IO.File]::Exists($marker) -and -not $supervisor.HasExited -and $timer.ElapsedMilliseconds -lt 8000) { Start-Sleep -Milliseconds 50 }
        Assert-True ([IO.File]::Exists($marker)) 'Owned child did not reach post-grant marker'
        $parts = [IO.File]::ReadAllText($marker).Split('|')
        $owned = [Diagnostics.Process]::GetProcessById([int]$parts[0])
        $null = $owned.Handle
        Assert-True ($owned.StartTime.ToUniversalTime().Ticks -eq [long]$parts[1]) 'Owned child identity changed'
        $supervisor.Kill()
        Assert-True ($supervisor.WaitForExit(3000)) 'Supervisor did not exit'
        Assert-True ($owned.WaitForExit(3000)) 'Probe survived job owner exit'
    } finally {
        if (-not $supervisor.HasExited) { $supervisor.Kill(); $supervisor.WaitForExit() }
        if ($null -ne $owned) { if (-not $owned.HasExited) { $owned.Kill(); $owned.WaitForExit() }; $owned.Dispose() }
        $supervisor.Dispose()
    }
}
Invoke-Case 'Cold PowerShell bootstrap resolves both host serializers before any cycle or native call' {
    $scratch = Join-Path 'D:\J2M\evidence\participant-restart-preflight' ('serializer-fake-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $scratch | Out-Null
    foreach ($name in @('RestartExperiment.cs', 'RestartExperimentWindows.cs', 'RestartExperimentNativeProbe.cs')) {
        Copy-Item -LiteralPath (Join-Path $source $name) -Destination $scratch
    }
    $hostText = [IO.File]::ReadAllText((Join-Path $RepositoryRoot 'Assets/_Features/Exhibition/Tools/Restart-Experiment.ps1'))
    # The legacy branch follows the runtime-1 branch, which also has a nested Probe check.
    $boundary = $hostText.IndexOf('    if ($Probe) {', [StringComparison]::Ordinal)
    Assert-True ($boundary -gt 0) 'Host bootstrap boundary missing'
    # Execute the real bootstrap in a new -NoProfile process, then the exact serializer statements.
    $constructors = [regex]::Matches($hostText, '(?m)^\s*\$serializer = New-Object System\.Runtime\.Serialization\.Json\.DataContractJsonSerializer.*$')
    Assert-True ($constructors.Count -eq 2) 'Expected request and observation serializers'
    $body = $hostText.Substring(0, $boundary) + "`n`$observation = New-Object Game.Exhibition.RestartExperiment.ProbeObservation`n"
    foreach ($line in $constructors) { $body += $line.Value + "`nif (`$null -eq `$serializer) { throw 'Serializer missing' }`n" }
    $body += "[Console]::Out.Write('SERIALIZERS_OK')`n} catch { [Console]::Error.Write(`$_.Exception.ToString()); exit 1 }"
    $scriptPath = Join-Path $scratch 'bootstrap-test.ps1'
    $requestPath = Join-Path $scratch 'request.json'
    [IO.File]::WriteAllText($scriptPath, $body)
    [IO.File]::WriteAllText($requestPath, '{}')
    $start = New-FakeProbe ''
    $start.Arguments = '-NoProfile -ExecutionPolicy Bypass -File ' + [Game.Exhibition.RestartExperiment.ExperimentFiles]::Quote($scriptPath) + ' -RequestPath ' + [Game.Exhibition.RestartExperiment.ExperimentFiles]::Quote($requestPath)
    $child = [Diagnostics.Process]::Start($start)
    try {
        $child.StandardInput.Close()
        $output = $child.StandardOutput.ReadToEndAsync()
        $errors = $child.StandardError.ReadToEndAsync()
        Assert-True ($child.WaitForExit(15000)) 'Cold bootstrap timed out'
        Assert-True ($child.ExitCode -eq 0 -and $output.Result -ceq 'SERIALIZERS_OK' -and $errors.Result -eq '') ('Cold bootstrap failed: ' + $errors.Result)
    } finally { if (-not $child.HasExited) { $child.Kill(); $child.WaitForExit() }; $child.Dispose() }
}
function Invoke-ObservedProbe([string]$Body, [int]$Budget = 10000, $Operations = $null) {
    $timer = [Diagnostics.Stopwatch]::StartNew()
    $start = New-FakeProbe $Body
    $script:lastAttempt = New-Object Game.Exhibition.RestartExperiment.ProbeAttempt
    $script:lastAttempt.Nonce = $expected.Nonce
    $deadline = New-Object Game.Exhibition.RestartExperiment.Deadline([Func[long]]{ $timer.ElapsedMilliseconds }, [long]$Budget, 'Probe timed out')
    if ($null -eq $Operations) { $Operations = New-Object Game.Exhibition.RestartExperiment.ProbeOperations }
    [Game.Exhibition.RestartExperiment.WindowsCycleEnvironment]::RunOwnedProbe(
        [Func[Diagnostics.ProcessStartInfo]]{ $start }, $deadline, [Func[long]]{ $timer.ElapsedMilliseconds }, $expected,
        $script:lastAttempt, $Operations)
}

$sdkDiagnostics = @'
[Console]::Error.Write("Setting breakpad minidump AppID = 123`r`nSteamInternal_SetMinidumpSteamID:  Caching Steam ID:  456 [API loaded no]`r`n")
'@
Invoke-Case 'Observed SDK stderr succeeds only after full ready result and owned exit' {
    $ready = Invoke-ObservedProbe ($success + "`n" + $sdkDiagnostics) 10000
    Assert-True $ready 'Observed successful SDK diagnostics rejected'
    Assert-True ($script:lastAttempt.StderrPolicyVersion -eq 1 -and $script:lastAttempt.StderrDisposition -ceq 'Sdk165InitDiagnostics') 'Policy classification missing'
    Assert-True ($script:lastAttempt.Stderr -ceq "Setting breakpad minidump AppID = 123`r`nSteamInternal_SetMinidumpSteamID:  Caching Steam ID:  456 [API loaded no]`r`n") 'Captured stderr changed'
    Assert-True ($script:lastAttempt.ExitCode -eq 0 -and $script:lastAttempt.OwnedExitConfirmed -and $script:lastAttempt.OutputComplete -and $script:lastAttempt.ErrorOutputComplete) 'Exit/output contract bypassed'
}
foreach ($case in @(
    @{Name='extra fatal line'; Body=$success + "`n" + $sdkDiagnostics + "`n[Console]::Error.Write('fatal')"},
    @{Name='wrong diagnostic AppID'; Body=$success + "`n" + $sdkDiagnostics.Replace('AppID = 123','AppID = 124')},
    @{Name='wrong diagnostic SteamID'; Body=$success + "`n" + $sdkDiagnostics.Replace('456 [API','457 [API')},
    @{Name='duplicate diagnostics'; Body=$success + "`n" + $sdkDiagnostics + "`n" + $sdkDiagnostics},
    @{Name='nonzero exit'; Body=$success + "`n" + $sdkDiagnostics + "`nexit 7"},
    @{Name='query error'; Body=$success.Replace('QueryError=$null', "QueryError='failure'") + "`n" + $sdkDiagnostics},
    @{Name='shutdown error'; Body=$success.Replace('ShutdownError=$null', "ShutdownError='failure'") + "`n" + $sdkDiagnostics},
    @{Name='not logged on'; Body=$success.Replace('LoggedOn=$true', 'LoggedOn=$false') + "`n" + $sdkDiagnostics},
    @{Name='failed Init'; Body=$globalUser + "`n" + $sdkDiagnostics},
    @{Name='stderr overflow'; Body=$success + "`n" + $sdkDiagnostics + "`n[Console]::Error.Write(('x' * 20000))"},
    @{Name='stdout collision'; Body=$success + "`n" + $sdkDiagnostics.Replace('::Error.Write', '::Out.Write')}
)) {
    Invoke-Case ('Known SDK stderr cannot excuse ' + $case.Name) {
        $rejected = $false
        try { Invoke-ObservedProbe $case.Body 10000 | Out-Null } catch { $rejected = $true }
        Assert-True ($rejected -and -not $script:lastAttempt.ReadyObserved) 'Damaged observation accepted'
        Assert-True ($script:lastAttempt.Stderr.Length -le 16384) 'Stderr retention limit bypassed'
    }
}
foreach ($mode in @('job', 'output-throw', 'output-incomplete', 'held-pipes', 'reader-failure')) {
    Invoke-Case ('Known SDK stderr cannot excuse supervisor ' + $mode) {
        $ops = New-Object RestartExperimentFakes.FailureOperations
        $ops.Mode = $mode
        $rejected = $false
        try { Invoke-ObservedProbe ($success + "`n" + $sdkDiagnostics) 10000 $ops | Out-Null } catch { $rejected = $true }
        finally { $ops.DisposeRetainedProcess() }
        Assert-True ($rejected -and -not $script:lastAttempt.ReadyObserved) 'Supervisor failure excused'
    }
}
foreach ($case in @(
    @{Name='old wire'; Body=$success.Replace('WireVersion=3;', '')},
    @{Name='unsupported wire'; Body=$success.Replace('WireVersion=3', 'WireVersion=1')},
    @{Name='required query flag missing'; Body=$success.Replace('QueryCalled=$true;', '')},
    @{Name='query error with ready identity'; Body=$success.Replace('QueryError=$null', "QueryError='query failed'")},
    @{Name='shutdown error with returned flag'; Body=$success.Replace('ShutdownError=$null', "ShutdownError='shutdown failed'")},
    @{Name='empty stderr abnormal exit'; Body='exit 9'},
    @{Name='partial output timeout'; Body="[Console]::Out.Write('{'); Start-Sleep -Seconds 30"},
    @{Name='stdout overflow after valid JSON'; Body=($success + "`n[Console]::Out.Write(('x' * 20000))")},
    @{Name='stderr overflow'; Body=($success + "`n[Console]::Error.Write(('x' * 20000))")}
)) {
    Invoke-Case $case.Name {
        $rejected = $false
        try { Invoke-ObservedProbe $case.Body 1500 | Out-Null } catch { $rejected = $true }
        Assert-True $rejected 'Invalid observation accepted'
        Assert-True ($null -ne $script:lastAttempt.Error) 'Missing primary error'
        Assert-True ($script:lastAttempt.Stdout.Length -le 16384 -and $script:lastAttempt.Stderr.Length -le 16384) 'Unbounded retention'
        Assert-True $script:lastAttempt.OwnedExitConfirmed 'Owned process exit not recovered'
        if ($case.Name -eq 'empty stderr abnormal exit') { Assert-True ($script:lastAttempt.ExitCode -eq 9) 'Exit code lost' }
        if ($case.Name -eq 'partial output timeout') { Assert-True ($script:lastAttempt.Stdout -ceq '{') 'Partial output lost' }
    }
}

foreach ($mode in @('kill', 'job', 'output-throw', 'output-incomplete', 'held-pipes', 'reader-failure', 'create', 'create-null')) {
    Invoke-Case "Supervisor injected $mode failure" {
        $ops = New-Object RestartExperimentFakes.FailureOperations
        $ops.Mode = $mode
        $body = $success; $budget = 10000
        if ($mode -eq 'kill') { $body = 'Start-Sleep -Seconds 30'; $budget = 1000 }
        $rejected = $false
        try { Invoke-ObservedProbe $body $budget $ops | Out-Null } catch { $rejected = $true }
        Assert-True $rejected 'Injected failure accepted'
        Assert-True ($null -ne $script:lastAttempt.Error) 'Failure disappeared'
        if ($mode -eq 'kill' -or $mode -eq 'job') { Assert-True ($null -ne $script:lastAttempt.CleanupError) 'Cleanup error missing' }
        if ($mode.StartsWith('output-') -or $mode -eq 'held-pipes' -or $mode -eq 'reader-failure') { Assert-True ($null -ne $script:lastAttempt.CollectionError) 'Collection error missing' }
        if ($mode.StartsWith('create')) { Assert-True ($script:lastAttempt.Creation -ceq 'Unknown' -and $null -eq $script:lastAttempt.Pid) 'Unknown creation inferred success' }
        Assert-True ($ops.OutputWaits -le 1 -and $ops.TerminationWaits -le 1) 'Cleanup allowance restarted'
        $ops.DisposeRetainedProcess()
    }
}

foreach ($role in @('Steam', 'Probe', 'FullCycleGame')) {
    Invoke-Case "Actual child environment policy $role" {
        $start = New-FakeProbe @'
@{ Old=[Environment]::GetEnvironmentVariable('sTeAmObsolete'); App=[Environment]::GetEnvironmentVariable('SteamAppId'); Game=[Environment]::GetEnvironmentVariable('SteamGameId'); Keep=[Environment]::GetEnvironmentVariable('J2M_KEEP'); PathPresent=([bool]$env:PATH); TempPresent=([bool]$env:TEMP) } | ConvertTo-Json -Compress
'@
        $start.EnvironmentVariables['sTeAmObsolete'] = 'synthetic-secret'
        $start.EnvironmentVariables['SteamAppId'] = '999'
        $start.EnvironmentVariables['SteamGameId'] = '999'
        $start.EnvironmentVariables['J2M_KEEP'] = 'preserved'
        [Game.Exhibition.RestartExperiment.LaunchEnvironment]::Apply($start, [Game.Exhibition.RestartExperiment.LaunchRole]::$role, 123)
        $p = [Game.Exhibition.RestartExperiment.LaunchEnvironment]::Start([Func[Diagnostics.ProcessStartInfo]]{ $start }, $null, [Func[Diagnostics.ProcessStartInfo,Diagnostics.Process]]{ param($psi) [Diagnostics.Process]::Start($psi) })
        try {
            $out = New-Object Game.Exhibition.RestartExperiment.BoundedOutput($p.StandardOutput)
            $err = New-Object Game.Exhibition.RestartExperiment.BoundedOutput($p.StandardError)
            $p.StandardInput.WriteLine('test'); $p.StandardInput.Close()
            Assert-True ($p.WaitForExit(10000)) 'Environment child timeout'
            Assert-True ([Threading.Tasks.Task]::WaitAll([Threading.Tasks.Task[]]@($out.Completion,$err.Completion), 1000)) 'Environment pipes incomplete'
            $row = $out.Snapshot().Text | ConvertFrom-Json
            Assert-True ($row.Keep -ceq 'preserved' -and $row.PathPresent -and $row.TempPresent) 'OS environment changed'
            Assert-True ($null -eq $row.Old) 'Obsolete Steam value retained'
            if ($role -eq 'Steam') { Assert-True ($null -eq $row.App -and $null -eq $row.Game) 'Steam received app autorun environment' }
            else { Assert-True ($row.App -ceq '123' -and $row.Game -ceq '123') 'Request AppID not set' }
        } finally {
            if (-not $p.HasExited) { $p.Kill(); $null = $p.WaitForExit(2000) }
            $p.Dispose()
        }
    }
}

Invoke-Case 'Bounded collector drains overflow and retains reader exceptions' {
    $large = New-Object IO.StringReader(('x' * 40000))
    $out = New-Object Game.Exhibition.RestartExperiment.BoundedOutput($large)
    Assert-True ($out.Completion.Wait(1000)) 'Finite reader did not finish'
    Assert-True ($out.Snapshot().Overflow -and $out.Snapshot().Complete -and $out.Snapshot().Text.Length -eq 16384) 'Overflow reader did not drain'
    $broken = New-Object RestartExperimentFakes.BrokenReader
    $out = New-Object Game.Exhibition.RestartExperiment.BoundedOutput($broken)
    Assert-True ($out.Completion.Wait(1000) -and $out.Snapshot().Error.Contains('reader failure') -and -not $out.Snapshot().Complete) 'Reader exception lost'
}

foreach ($exitCode in @(0, 7)) {
    Invoke-Case "Owned command retains handle and observes exit $exitCode" {
        $start = New-FakeProbe ('exit ' + $exitCode)
        $owned = New-Object Game.Exhibition.RestartExperiment.OwnedShutdownCommand
        $script:commandChild = $null
        $script:observedCode = $null
        try {
            $owned.Start([Func[Diagnostics.ProcessStartInfo]]{ $start }, $null,
                [Func[Diagnostics.ProcessStartInfo,Diagnostics.Process]]{ param($psi) $script:commandChild = [Diagnostics.Process]::Start($psi); $script:commandChild },
                [Func[Diagnostics.Process,Game.Exhibition.RestartExperiment.ProcessIdentity]]{ param($p)
                    $identity = New-Object Game.Exhibition.RestartExperiment.ProcessIdentity
                    $identity.Pid = $p.Id; $identity.StartTicks = $p.StartTime.ToUniversalTime().Ticks; $identity
                })
            Assert-True ($owned.Identity.Pid -eq $script:commandChild.Id) 'Owned identity lost'
            Assert-True ($owned.Alive([Action[int]]{ param($code) $script:observedCode = $code })) 'Command should be waiting for grant'
            $script:commandChild.StandardInput.WriteLine('fake'); $script:commandChild.StandardInput.Close()
            Assert-True ($script:commandChild.WaitForExit(10000)) 'Harmless command did not exit'
            $rejected = $false
            try { $alive = $owned.Alive([Action[int]]{ param($code) $script:observedCode = $code }) } catch { $rejected = $true }
            Assert-True ($script:observedCode -eq $exitCode -and $owned.ExitCode -eq $exitCode) 'Command exit code lost'
            Assert-True ($rejected -eq ($exitCode -ne 0)) 'Command exit policy mismatch'
        } finally {
            if ($script:commandChild -and -not $script:commandChild.HasExited) { $script:commandChild.Kill(); $null = $script:commandChild.WaitForExit(2000) }
            $owned.Dispose()
        }
    }
}

Invoke-Case 'Owned command identity failure and handle disposal never kill command' {
    $start = New-FakeProbe 'Start-Sleep -Seconds 30'
    $owned = New-Object Game.Exhibition.RestartExperiment.OwnedShutdownCommand
    $script:commandChild = $null
    $script:retainedCommand = $null
    try {
        $rejected = $false
        try {
            $owned.Start([Func[Diagnostics.ProcessStartInfo]]{ $start }, $null,
                [Func[Diagnostics.ProcessStartInfo,Diagnostics.Process]]{ param($psi)
                    $script:commandChild = [Diagnostics.Process]::Start($psi)
                    $script:retainedCommand = [Diagnostics.Process]::GetProcessById($script:commandChild.Id)
                    $null = $script:retainedCommand.Handle
                    $script:commandChild
                },
                [Func[Diagnostics.Process,Game.Exhibition.RestartExperiment.ProcessIdentity]]{ param($p) throw 'identity unavailable' })
        } catch { $rejected = $true }
        Assert-True ($rejected -and $owned.Creation -ceq 'Created' -and $null -eq $owned.Identity) 'Identity failure inferred ownership'
        $owned.Dispose()
        Assert-True (-not $script:retainedCommand.HasExited) 'Disposing command killed it'
    } finally {
        if ($script:retainedCommand) {
            if (-not $script:retainedCommand.HasExited) { $script:retainedCommand.Kill(); $null = $script:retainedCommand.WaitForExit(2000) }
            $script:retainedCommand.Dispose()
        }
        $owned.Dispose()
    }
}

Write-Output "Restart experiment Windows fake tests: $script:passed passed; Steam/game/native API executions: 0."
