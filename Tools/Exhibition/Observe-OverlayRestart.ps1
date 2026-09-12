# Test-only, read-only process/log observer. Never packaged or used for launch admission.
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$EvidenceRoot,
    [string]$RunsRoot = 'D:\J2M\evidence\overlay-v3-runs',
    [string]$SteamLogs = 'C:\Program Files (x86)\Steam\logs'
)
$ErrorActionPreference = 'Stop'
if (-not $EvidenceRoot.StartsWith('D:\J2M\evidence\', [StringComparison]::OrdinalIgnoreCase)) { throw 'D evidence required.' }
if (Test-Path -LiteralPath $EvidenceRoot) { throw 'Fresh observer evidence directory required.' }
New-Item -ItemType Directory -Path $EvidenceRoot | Out-Null
$started = [DateTime]::UtcNow
$clock = [Diagnostics.Stopwatch]::StartNew()
$handles = @{}
$attempted = @{}
$facts = [Collections.Generic.List[object]]::new()
$run = $null
$submissionAt = $null
$childSeen = $false
function Record([string]$Event, $Detail) {
    $facts.Add(@{ Utc = [DateTime]::UtcNow.ToString('o'); ElapsedMs = $clock.ElapsedMilliseconds; Event = $Event; Detail = $Detail })
}
function Capture([string]$Role, $Identity) {
    if ($null -eq $Identity -or $attempted.ContainsKey($Role)) { return }
    $attempted[$Role] = $true
    try {
        $p = [Diagnostics.Process]::GetProcessById([int]$Identity.Pid)
        $null = $p.Handle # retain the OS process handle before observing exit
        if ($p.StartTime.ToUniversalTime().Ticks -ne [long]$Identity.StartTicks) { $p.Dispose(); throw 'PID start time mismatch.' }
        $handles[$Role] = @{ Process = $p; Identity = $Identity; Exited = $false }
        Record 'HandleRetained' @{ Role = $Role; Identity = $Identity }
    } catch { Record 'HandleUnavailable' @{ Role = $Role; Identity = $Identity; Error = $_.Exception.Message } }
}
function SnapshotLogs([string]$Phase) {
    foreach ($name in @('gameprocess_log.txt','console_log.txt')) {
        try { Copy-Item -LiteralPath (Join-Path $SteamLogs $name) -Destination (Join-Path $EvidenceRoot ($Phase + '-' + $name)) }
        catch { Record 'LogUnavailable' @{ Phase = $Phase; Name = $name; Error = $_.Exception.Message } }
    }
}
try {
    SnapshotLogs 'before'
    Write-Host 'Observer ready. Run the existing manual Origin command in Win+R. This observer never launches or retries anything.'
    while ($true) {
        if ($null -eq $run) {
            $candidates = @(Get-ChildItem -LiteralPath $RunsRoot -Directory -ErrorAction SilentlyContinue | Where-Object { $_.CreationTimeUtc -ge $started -and (Test-Path -LiteralPath (Join-Path $_.FullName 'helper-attempt.json')) })
            if ($candidates.Count -gt 1) { throw 'Multiple new runs: ambiguous observation, no automatic selection.' }
            if ($candidates.Count -eq 1) { $run = $candidates[0].FullName; Record 'RunObserved' $run }
        }
        if ($null -ne $run) {
            # A handoff failure can precede submission-intent. Preserve it and stop
            # instead of waiting forever for an intent that will never be created.
            $failure = $null
            try {
                $failure = Get-Content -LiteralPath (Join-Path $run 'terminal-failure.json') -Raw | ConvertFrom-Json
                if ($failure.ProtocolRevision -cne 'observation-v3-runtime-3') { $failure = $null }
            } catch { }
            if ($null -ne $failure) {
                Record 'TerminalFailureObserved' $failure
                break
            }
            # Files may still be being written. A failed read is retried only by this
            # external observer; it never repairs or substitutes runtime documents.
            foreach ($item in @(@('helper-creation.json','Helper','Helper'), @('replacement-started.json','Child','Self'))) {
                if ($attempted.ContainsKey($item[1])) { continue }
                try {
                    $doc = Get-Content -LiteralPath (Join-Path $run $item[0]) -Raw | ConvertFrom-Json
                    if ($doc.ProtocolRevision -cne 'observation-v3-runtime-3') { throw 'Unexpected revision.' }
                    Capture $item[1] $doc.($item[2])
                    if ($item[1] -eq 'Child') { $childSeen = $true }
                } catch { }
            }
            if ($null -eq $submissionAt) {
                try {
                    $intent = Get-Content -LiteralPath (Join-Path $run 'submission-intent.json') -Raw | ConvertFrom-Json
                    if ($intent.ProtocolRevision -cne 'observation-v3-runtime-3') { throw 'Unexpected revision.' }
                    $submissionAt = $clock.ElapsedMilliseconds
                    Record 'SubmissionIntentObserved' $intent
                } catch { }
            }
        }
        foreach ($role in @($handles.Keys)) {
            $h = $handles[$role]
            if (-not $h.Exited -and $h.Process.HasExited) {
                $h.Exited = $true
                Record 'ActualProcessExit' @{ Role = $role; Identity = $h.Identity; ExitUtc = $h.Process.ExitTime.ToUniversalTime().ToString('o'); ExitCode = $h.Process.ExitCode }
            }
        }
        if ($handles.ContainsKey('Child') -and $handles['Child'].Exited) { break }
        if ((-not $childSeen -or -not $handles.ContainsKey('Child')) -and $null -ne $submissionAt -and $clock.ElapsedMilliseconds - $submissionAt -ge 60000) {
            Record 'LaunchObservationWindowEnded' 'No independent start observed within 60 seconds after intent was observed. No retry.'
            break
        }
        Start-Sleep -Milliseconds 100
    }
} finally {
    SnapshotLogs 'after'
    foreach ($role in @($handles.Keys)) { $handles[$role].Process.Dispose() }
    @{ StartedUtc = $started.ToString('o'); Run = $run; Classification = 'U'; Facts = @($facts.ToArray());
       Note = 'W/R/F requires manual Steam session+AppID+ActionID correlation. Self-reported startup is not OS exit or Steam acceptance.' } |
        ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $EvidenceRoot 'observer.json') -Encoding UTF8
}
