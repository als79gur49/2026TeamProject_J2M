param(
    [Parameter(Mandatory = $true)][ValidateRange(1, 4294967295)][long]$AppId,
    [Parameter(Mandatory = $true)][int]$ParentId,
    [Parameter(Mandatory = $true)][long]$ParentStartTicks,
    [Parameter(Mandatory = $true)][string]$Executable,
    [ValidateRange(1, 120)][int]$TimeoutSeconds = 30
)

$ErrorActionPreference = 'Stop'
try {
    if ($ParentId -le 0 -or $ParentStartTicks -le 0) {
        throw 'The previous game process identity is invalid.'
    }
    if (-not [System.IO.Path]::IsPathRooted($Executable) -or
        -not [System.IO.File]::Exists($Executable)) {
        throw 'The exhibition game executable is missing.'
    }
    $previous = Get-Process -Id $ParentId -ErrorAction SilentlyContinue
    if ($null -ne $previous) {
        if ($previous.StartTime.ToUniversalTime().Ticks -eq $ParentStartTicks) {
            if (-not $previous.WaitForExit($TimeoutSeconds * 1000)) {
                throw 'The previous game has not exited. Close it before starting the game manually.'
            }
        }
        # A reused PID belongs to a different process; the requested parent has exited.
    }
    # This helper's environment is inherited by the child, never written to steam_appid.txt.
    $env:SteamAppId = [string]$AppId
    $env:SteamGameId = [string]$AppId
    Start-Process -FilePath $Executable -WorkingDirectory ([System.IO.Path]::GetDirectoryName($Executable)) -ArgumentList @('-j2mPlatformProvider', 'steam') -ErrorAction Stop | Out-Null
    exit 0
}
catch {
    $message = "The exhibition game could not restart. Start the game manually after resolving this error.`n`n" + $_.Exception.Message
    try {
        Add-Type -AssemblyName System.Windows.Forms
        [System.Windows.Forms.MessageBox]::Show($message, 'Exhibition restart failed') | Out-Null
    }
    catch { [Console]::Error.WriteLine($message) }
    exit 1
}
