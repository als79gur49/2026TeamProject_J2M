[CmdletBinding()]
param([Parameter(Mandatory)][string]$EvidenceRoot)
$ErrorActionPreference = 'Stop'
if (Test-Path -LiteralPath $EvidenceRoot) { throw 'Fresh evidence directory required.' }
if (-not $EvidenceRoot.StartsWith('D:\J2M\evidence\', [StringComparison]::OrdinalIgnoreCase)) { throw 'D evidence required.' }
New-Item -ItemType Directory -Path $EvidenceRoot | Out-Null
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$source = Join-Path $repo 'Assets/_Features/Exhibition/Integration'
$build = Join-Path 'D:\J2M\builds\overlay-observation-runtime2-fakes' ([guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $build | Out-Null
$names = @('RestartExperiment.cs','RestartExperimentWindows.cs','RestartExperimentNativeProbe.cs','ObservationV3Wire.cs','ObservationV3Handoff.cs','ObservationV3Pipe.cs','ObservationV3RuntimeWire.cs','ObservationV3RuntimeProtocol.cs','ObservationV3Admission.cs','ObservationV3JournalReader.cs','ObservationV3Session.cs','ObservationV3WindowsEnvironment.cs','ObservationV3WindowsHost.cs')
$files = @($names | ForEach-Object { Join-Path $source $_ })
$exe = Join-Path $build 'Runtime2Fakes.exe'
Add-Type -Path ($files + @((Join-Path $PSScriptRoot 'ObservationV3.Runtime2Fakes.cs'), (Join-Path $PSScriptRoot 'ObservationV3.Runtime2HandoffFakes.cs'))) -ReferencedAssemblies @('System.dll','System.Core.dll','System.Xml.dll','System.Runtime.Serialization.dll') -OutputAssembly $exe -OutputType ConsoleApplication
& $exe $EvidenceRoot
if ($LASTEXITCODE -ne 0) { throw 'Runtime2 Windows fake failed.' }
# Exercise the actual packaged PS discriminator and strict probe entry with an invalid request.
# Its hash grant is valid; missing mandatory schema must reject before any native access.
$files | ForEach-Object { Copy-Item -LiteralPath $_ -Destination $build }
$hostPath = Join-Path $build 'Restart-Experiment.ps1'
Copy-Item -LiteralPath (Join-Path $repo 'Assets/_Features/Exhibition/Tools/Restart-Experiment.ps1') -Destination $hostPath
$invalid = Join-Path $EvidenceRoot 'invalid-probe.json'
[IO.File]::WriteAllText($invalid, '{"Version":3,"ProtocolRevision":"observation-v3-runtime-3"}')
$hash = (Get-FileHash -LiteralPath $invalid -Algorithm SHA256).Hash.ToLowerInvariant()
$start = New-Object Diagnostics.ProcessStartInfo
$start.FileName = Join-Path $PSHOME 'powershell.exe'
$start.Arguments = '-NoProfile -ExecutionPolicy Bypass -File "' + $hostPath + '" -Probe -RequestPath "' + $invalid + '"'
$start.UseShellExecute = $false
$start.RedirectStandardInput = $true
$start.RedirectStandardOutput = $true
$start.RedirectStandardError = $true
$p = [Diagnostics.Process]::Start($start)
$p.StandardInput.WriteLine($hash)
$p.StandardInput.Close()
$stdout = $p.StandardOutput.ReadToEndAsync()
$stderr = $p.StandardError.ReadToEndAsync()
if (-not $p.WaitForExit(30000)) { throw 'Strict PS entry did not exit within deadline.' }
$err = $stderr.Result
[IO.File]::WriteAllText((Join-Path $EvidenceRoot 'ps-entry-stderr.txt'), $err)
if ($p.ExitCode -ne 1 -or $err -notmatch 'MissingRequiredField') { throw ('Unexpected strict PS rejection: ' + $err) }
Write-Output 'PASS actual PS runtime-3 source compilation and strict rejection before native'
# Old/missing revision must fail in the real PS discriminator, without invoking native.
foreach ($case in @(@{ Name='runtime-2'; Json='{"Version":3,"ProtocolRevision":"observation-v3-runtime-2"}' }, @{ Name='runtime-1'; Json='{"Version":3,"ProtocolRevision":"observation-v3-runtime-1"}' },
                    @{ Name='missing-revision'; Json='{"Version":3}' })) {
    $request = Join-Path $EvidenceRoot ($case.Name + '.json')
    [IO.File]::WriteAllText($request, $case.Json)
    $start = New-Object Diagnostics.ProcessStartInfo
    $start.FileName = Join-Path $PSHOME 'powershell.exe'
    $start.Arguments = '-NoProfile -ExecutionPolicy Bypass -File "' + $hostPath + '" -Probe -RequestPath "' + $request + '"'
    $start.UseShellExecute = $false
    $start.RedirectStandardInput = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $p = [Diagnostics.Process]::Start($start)
    $p.StandardInput.Close()
    $stdout = $p.StandardOutput.ReadToEndAsync(); $stderr = $p.StandardError.ReadToEndAsync()
    if (-not $p.WaitForExit(30000)) { throw 'Revision rejection did not exit.' }
    $err = $stderr.Result
    [IO.File]::WriteAllText((Join-Path $EvidenceRoot ($case.Name + '-stderr.txt')), $err)
    if ($p.ExitCode -ne 1 -or $err -notmatch 'Unsupported observation protocol revision') { throw ('Unexpected revision rejection: ' + $err) }
    $p.Dispose()
}
Write-Output 'PASS actual PS runtime-2/runtime-1 and missing-revision rejection before native'

$runtime3 = Join-Path $build 'Runtime3Fakes.exe'
Add-Type -Path ($files + (Join-Path $PSScriptRoot 'ObservationV3.Runtime3Fakes.cs')) -ReferencedAssemblies @('System.dll','System.Core.dll','System.Xml.dll','System.Runtime.Serialization.dll') -OutputAssembly $runtime3 -OutputType ConsoleApplication
& $runtime3 (Join-Path $EvidenceRoot 'runtime3')
if ($LASTEXITCODE -ne 0) { throw 'Runtime3 final invocation/process fixture failed.' }
