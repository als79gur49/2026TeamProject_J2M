[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$RequestPath,
    [switch]$Probe
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
# These PowerShell-only functions must remain available even when request parsing
# or helper C# compilation fails. All non-probe failures share the same guidance.
# Bounded failure-only details. Do not serialize the request or process environment.
function Get-RestartFailureDetails([Exception]$Failure, [string[]]$Redact = @()) {
    $lines = New-Object 'Collections.Generic.List[string]'
    $lines.Add("UTC: $([DateTime]::UtcNow.ToString('o')) HelperPid: $PID")
    $stacks = New-Object 'Collections.Generic.List[string]'
    $queue = New-Object 'Collections.Generic.Stack[Exception]'
    $queue.Push($Failure)
    $count = 0
    while ($queue.Count -gt 0 -and $count -lt 16) {
        $error = $queue.Pop(); $count++
        $lines.Add("Exception $count`: $($error.GetType().FullName) HResult: $($error.HResult)")
        foreach ($key in @('RestartOperation', 'NativeOperation', 'TargetPid', 'OriginalSteamPid', 'ShutdownCommandPid', 'ShutdownCommandExitConfirmed')) {
            if ($error.Data.Contains($key)) { $lines.Add("$key`: $($error.Data[$key])") }
        }
        if ($error -is [ComponentModel.Win32Exception]) { $lines.Add("NativeErrorCode: $($error.NativeErrorCode)") }
        $message = $error.Message
        foreach ($value in $Redact) {
            if (-not [string]::IsNullOrEmpty($value) -and $value -ne '0') { $message = $message.Replace($value, '[redacted]') }
        }
        if ($message.Length -gt 1000) { $message = $message.Substring(0, 1000) + ' [truncated]' }
        $stacks.Add("Exception $count`: $message")
        if ($error.StackTrace) {
            $stack = $error.StackTrace
            foreach ($value in $Redact) {
                if (-not [string]::IsNullOrEmpty($value) -and $value -ne '0') { $stack = $stack.Replace($value, '[redacted]') }
            }
            if ($stack.Length -gt 3000) { $stack = $stack.Substring(0, 3000) + ' [truncated]' }
            $stacks.Add($stack)
        }
        if ($error -is [AggregateException]) {
            for ($i = $error.InnerExceptions.Count - 1; $i -ge 0; $i--) { $queue.Push($error.InnerExceptions[$i]) }
        } elseif ($null -ne $error.InnerException) { $queue.Push($error.InnerException) }
    }
    $text = ($lines -join "`n") + "`n" + ($stacks -join "`n")
    # At most 32 KiB in UTF-8, including the truncation notice.
    if ($text.Length -gt 8000) { $text = $text.Substring(0, 8000) + "`n[truncated]" }
    return $text
}

function Save-RestartFailure([string]$ValidatedDirectory, [string]$Details) {
    if ([string]::IsNullOrWhiteSpace($ValidatedDirectory)) { return 'Failure record unavailable: handoff path was not validated.' }
    try {
        $path = Join-Path $ValidatedDirectory 'restart-failure.txt'
        $file = [IO.File]::Open($path, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::Read)
        try {
            $bytes = [Text.Encoding]::UTF8.GetBytes($Details)
            if ($bytes.Length -gt 32768) { throw 'Failure details exceed the size limit.' }
            $file.Write($bytes, 0, $bytes.Length)
            $file.Flush()
        } finally { $file.Dispose() }
        return "Failure record: $path"
    } catch {
        return 'Failure record unavailable: writing failed; original restart error is retained.'
    }
}

function Format-RestartExperimentFailure([Exception]$Failure, [string]$EvidenceDirectory, [string]$Purpose = 'Unknown', [string]$Details = '', [string]$RecordStatus = '') {
    $summary = $Failure.Message
    if ($Details) { $summary = $Details }
    if ($summary.Length -gt 600) { $summary = $summary.Substring(0, 600) + '...' }
    if ([string]::IsNullOrWhiteSpace($EvidenceDirectory)) { $EvidenceDirectory = 'unknown (not obtained)' }
    return "Participant restart stopped. Keep the game closed and check the failure before another restart.`n`n$summary`n`nHandoff: $EvidenceDirectory`n$RecordStatus"

}

function Show-RestartExperimentFailure(
    [Exception]$Failure,
    [string]$EvidenceDirectory,
    [string]$Purpose = 'Unknown',
    [string]$Details = '',
    [string]$RecordStatus = '',
    [scriptblock]$Display = {
        param($message)
        Add-Type -AssemblyName System.Windows.Forms
        [System.Windows.Forms.MessageBox]::Show($message, 'Exhibition participant restart') | Out-Null
    }
) {
    $guidance = Format-RestartExperimentFailure -Failure $Failure -EvidenceDirectory $EvidenceDirectory -Purpose $Purpose -Details $Details -RecordStatus $RecordStatus
    # Keep stop guidance available if Forms cannot load or the popup fails.
    [Console]::Error.WriteLine($guidance)
    try { & $Display $guidance | Out-Null }
    catch { [Console]::Error.WriteLine($_.Exception.Message) }
}

$request = $null
$environment = $null
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
    $environment = New-Object Game.Exhibition.RestartExperiment.WindowsCycleEnvironment($request, $RequestPath)
    [Game.Exhibition.RestartExperiment.Cycle]::Run($environment, $request.Trial)
    exit 0
} catch {
    $failure = $_.Exception
    if ($Probe) {
        # Preserve the supervisor IPC contract; no file, popup, or extra stdout.
        [Console]::Error.WriteLine($failure.ToString())
    } else {
        $directory = $null
        if ($null -ne $environment) { $directory = $environment.ValidatedHandoffDirectory }
        $redact = @()
        if ($null -ne $request) {
            if ($request.SteamId -ne 0) { $redact += [string]$request.SteamId }
            foreach ($identity in @($request.Parent, $request.Steam)) {
                if ($null -ne $identity) { $redact += @($identity.UserSid, $identity.Logon) }
            }
        }
        try { $details = Get-RestartFailureDetails -Failure $failure -Redact $redact }
        catch { $details = 'Failure formatting unavailable. ' + $failure.GetType().FullName }
        $recordStatus = Save-RestartFailure -ValidatedDirectory $directory -Details $details
        [Console]::Error.WriteLine($details)
        Show-RestartExperimentFailure -Failure $failure -EvidenceDirectory $directory -Purpose $purpose -Details $details -RecordStatus $recordStatus
    }
    exit 1
}
