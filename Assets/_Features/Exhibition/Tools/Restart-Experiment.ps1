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
    return "Participant restart stopped. Keep the game closed and check the failure before another restart.`n`n$summary`n`nHandoff: $EvidenceDirectory"

}

function Show-RestartExperimentFailure(
    [Exception]$Failure,
    [string]$EvidenceDirectory,
    [string]$Purpose = 'Unknown',
    [scriptblock]$Display = {
        param($message)
        Add-Type -AssemblyName System.Windows.Forms
        [System.Windows.Forms.MessageBox]::Show($message, 'Exhibition participant restart') | Out-Null
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
    if (-not [Environment]::Is64BitProcess) { throw 'The restart host must be x64.' }
    $rawRequest = Get-Content -LiteralPath $RequestPath -Raw | ConvertFrom-Json
    # Compiler references do not load this assembly for PowerShell's New-Object resolver.
    Add-Type -AssemblyName System.Runtime.Serialization
    # These operational sources are shared with the NUnit tests and the Windows player.
    $sharedSources = @(
        (Join-Path $PSScriptRoot 'RestartExperiment.cs'),
        (Join-Path $PSScriptRoot 'RestartExperimentWindows.cs'),
        (Join-Path $PSScriptRoot 'RestartExperimentNativeProbe.cs')
    )
    Add-Type -Path $sharedSources -ReferencedAssemblies @('System.dll', 'System.Core.dll', 'System.Xml.dll', 'System.Runtime.Serialization.dll')
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
    $purpose = 'Reset'
    $bootstrapDirectory = $request.EvidenceDirectory
    $environment = New-Object Game.Exhibition.RestartExperiment.WindowsCycleEnvironment($request, $RequestPath)
    [Game.Exhibition.RestartExperiment.Cycle]::Run($environment, $request.Trial)
    exit 0
} catch {
    $message = $_.Exception.ToString()
    $displayError = $_.Exception
    while ($null -ne $displayError.InnerException -and $displayError.GetType().FullName -ne 'Game.Exhibition.RestartExperiment.ProbeAttemptException') { $displayError = $displayError.InnerException }
    [Console]::Error.WriteLine($message)
    if (-not $Probe) {
        Show-RestartExperimentFailure -Failure $displayError -EvidenceDirectory $bootstrapDirectory -Purpose $purpose
    }
    exit 1
}
