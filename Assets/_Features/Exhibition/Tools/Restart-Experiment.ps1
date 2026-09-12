[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$RequestPath,
    [switch]$Probe
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
# These PowerShell-only functions must remain available even when request parsing
# or helper C# compilation fails. All non-probe failures share the same guidance.
function Format-RestartExperimentFailure([Exception]$Failure, [string]$EvidenceDirectory, [string]$Purpose = 'Unknown') {
    $summary = $Failure.Message
    if ($summary.Length -gt 600) { $summary = $summary.Substring(0, 600) + '...' }
    if ([string]::IsNullOrWhiteSpace($EvidenceDirectory)) { $EvidenceDirectory = 'unknown (not obtained)' }
    if ($Purpose -eq 'Observation') {
        return "Overlay observation stopped. This request does not authorize reset or retry. An accepted helper may still complete the single GameOnly handoff.`n`n$summary`n`nEvidence: $EvidenceDirectory"
    }
    if ($Purpose -ne 'Reset') {
        return "Diagnostic purpose is unknown. Do not restart the game or repeat the cycle until the evidence has been reviewed.`n`n$summary`n`nEvidence: $EvidenceDirectory"
    }
    return "Restart experiment stopped. Reset may have been partially applied or Pending may remain. Do not restart the game or repeat the cycle until the evidence has been reviewed.`n`n$summary`n`nEvidence: $EvidenceDirectory"
}

function Show-RestartExperimentFailure(
    [Exception]$Failure,
    [string]$EvidenceDirectory,
    [string]$Purpose = 'Unknown',
    [scriptblock]$Display = {
        param($message)
        Add-Type -AssemblyName System.Windows.Forms
        [System.Windows.Forms.MessageBox]::Show($message, 'Exhibition restart experiment') | Out-Null
    }
) {
    $guidance = Format-RestartExperimentFailure -Failure $Failure -EvidenceDirectory $EvidenceDirectory -Purpose $Purpose
    # Keep stop guidance available if Forms cannot load or the popup fails.
    [Console]::Error.WriteLine($guidance)
    try { & $Display $guidance | Out-Null }
    catch { [Console]::Error.WriteLine($_.Exception.Message) }
}

$request = $null
$environment = $null
$bootstrapDirectory = $null
$purpose = 'Unknown'
try {
    if (-not [Environment]::Is64BitProcess) { throw 'The diagnostic host must be x64.' }
    $rawRequest = Get-Content -LiteralPath $RequestPath -Raw | ConvertFrom-Json
    $runtime1 = $null -ne $rawRequest.ProtocolRevision -or $rawRequest.Version -eq 3
    if ($runtime1 -and ($rawRequest.Version -ne 3 -or $rawRequest.ProtocolRevision -cne 'observation-v3-runtime-3')) {
        throw 'Unsupported observation protocol revision.'
    }
    if ($rawRequest.EvidenceDirectory) {
        $candidate = [IO.Path]::GetFullPath($rawRequest.EvidenceDirectory)
        if ($candidate.StartsWith('D:\J2M\evidence\', [StringComparison]::OrdinalIgnoreCase) -and [IO.Directory]::Exists($candidate)) {
            $bootstrapDirectory = $candidate
            $row = @{ Utc=[DateTime]::UtcNow.ToString('o'); Nonce=$rawRequest.Nonce; Pid=$PID; Stage='HostBootstrap'; Probe=[bool]$Probe }
            [IO.File]::AppendAllText((Join-Path $bootstrapDirectory "bootstrap-$PID.jsonl"), ($row | ConvertTo-Json -Compress) + [Environment]::NewLine)
        }
    }
    # Compiler references do not load this assembly for PowerShell's New-Object resolver.
    Add-Type -AssemblyName System.Runtime.Serialization
    # These sources are copied only to the diagnostic build, and shared with the NUnit fake tests.
    $sharedSources = @(
        (Join-Path $PSScriptRoot 'RestartExperiment.cs'),
        (Join-Path $PSScriptRoot 'RestartExperimentWindows.cs'),
        (Join-Path $PSScriptRoot 'RestartExperimentNativeProbe.cs')
    )
    if ($runtime1) {
        foreach ($name in @('ObservationV3Wire.cs','ObservationV3Handoff.cs','ObservationV3Pipe.cs','ObservationV3RuntimeWire.cs',
                'ObservationV3RuntimeProtocol.cs','ObservationV3Admission.cs','ObservationV3JournalReader.cs','ObservationV3Session.cs','ObservationV3WindowsEnvironment.cs','ObservationV3WindowsHost.cs')) {
            $sharedSources += Join-Path $PSScriptRoot $name
        }
    }
    Add-Type -Path $sharedSources -ReferencedAssemblies @('System.dll', 'System.Core.dll', 'System.Xml.dll', 'System.Runtime.Serialization.dll')
    if ($runtime1) {
        # C# strict parser checks explicit required/duplicate fields; ConvertFrom-Json is only a discriminator.
        if ($Probe) {
            $grant = [Game.Exhibition.RestartExperiment.ObservationV3WindowsHost]::BootstrapLine(30000)
            $result = [Game.Exhibition.RestartExperiment.ObservationV3WindowsHost]::RunProbe($RequestPath, $grant)
            [Console]::Out.WriteLine([Game.Exhibition.RestartExperiment.ObservationV3Wire]::Serialize($result))
            exit 0
        }
        exit [Game.Exhibition.RestartExperiment.ObservationV3WindowsHost]::Run($RequestPath)
    }
    if ($Probe) {
        # No native SDK session before the supervisor attaches its kill-on-close job and grants this nonce.
        $grant = [Console]::In.ReadLine()
        if ([string]::IsNullOrEmpty($grant) -or $grant -cne $rawRequest.Nonce) { throw 'Probe ownership grant missing.' }
        $observation = [Game.Exhibition.RestartExperiment.NativeProbe]::Run($RequestPath)
        # Exactly one JSON result; no success until the supervisor also observes process exit.
        $serializer = New-Object System.Runtime.Serialization.Json.DataContractJsonSerializer($observation.GetType())
        $stream = New-Object System.IO.MemoryStream
        try {
            $serializer.WriteObject($stream, $observation)
            [Console]::Out.WriteLine([Text.Encoding]::UTF8.GetString($stream.ToArray()))
        } finally { $stream.Dispose() }
        exit 0
    }
    $serializer = New-Object System.Runtime.Serialization.Json.DataContractJsonSerializer([Game.Exhibition.RestartExperiment.ExperimentRequest])
    $stream = [IO.File]::OpenRead($RequestPath)
    try { $request = $serializer.ReadObject($stream) } finally { $stream.Dispose() }
    [Game.Exhibition.RestartExperiment.LaunchEnvironment]::ValidateChildRole($request)
    $purpose = $(if ([Game.Exhibition.RestartExperiment.OverlayObservationWire]::HasObservation($request)) { 'Observation' } elseif ($null -ne $request.Parent -and $null -ne $request.Steam -and $request.AppId -ne 0 -and $request.SteamId -ne 0) { 'Reset' } else { 'Unknown' })
    $environment = New-Object Game.Exhibition.RestartExperiment.WindowsCycleEnvironment($request, $RequestPath)
    [Game.Exhibition.RestartExperiment.Cycle]::Run($environment, $request.Trial)
    exit 0
} catch {
    # Observation helper errors must never leave a modal window holding AppID tracking.
    if ($runtime1) { [Console]::Error.WriteLine($_.Exception.ToString()); exit 1 }
    $message = $_.Exception.ToString()
    $displayError = $_.Exception
    while ($null -ne $displayError.InnerException -and $displayError.GetType().FullName -ne 'Game.Exhibition.RestartExperiment.ProbeAttemptException') { $displayError = $displayError.InnerException }
    if ($bootstrapDirectory) {
        try {
            $row = @{ Utc=[DateTime]::UtcNow.ToString('o'); Pid=$PID; Stage=$(if ($null -ne $environment -and $environment.CycleEntered) { 'HostCycleFailureReported' } else { 'HostBootstrapFailed' }); Error=$message; Probe=[bool]$Probe }
            [IO.File]::AppendAllText((Join-Path $bootstrapDirectory "bootstrap-$PID.jsonl"), ($row | ConvertTo-Json -Compress) + [Environment]::NewLine)
        } catch { [Console]::Error.WriteLine($_.Exception.Message) }
    }
    [Console]::Error.WriteLine($message)
    if (-not $Probe) {
        Show-RestartExperimentFailure -Failure $displayError -EvidenceDirectory $bootstrapDirectory -Purpose $purpose
    }
    exit 1
}
