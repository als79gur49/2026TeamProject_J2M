$ErrorActionPreference = "Stop"
$env:VECTORQUAKE_STEAMWORKS_EXPECTATION_TEST_MODE = "1"
. (Join-Path $PSScriptRoot "..\Export-SteamworksConfigurationExpectation.ps1")

$script:Passed = 0
$script:Failed = 0
$script:Results = @()

function Invoke-Case {
    param([string]$Name, [scriptblock]$Body)
    try {
        & $Body
        $script:Passed++
        $script:Results += "PASS $Name"
    } catch {
        $script:Failed++
        $script:Results += "FAIL $Name :: $($_.Exception.Message)"
    }
}

function Assert-True {
    param($Value, [string]$Message = "Expected true.")
    if (-not $Value) { throw $Message }
}

Invoke-Case "path containment recognizes repository children" {
    $root = [IO.Path]::GetFullPath((Join-Path $env:TEMP "j2m-repository"))
    Assert-True (Test-PathIsSameOrUnder `
        -Candidate (Join-Path $root "Artifacts\expectation") `
        -Parent $root)
}

Invoke-Case "path containment permits private sibling output" {
    $root = [IO.Path]::GetFullPath((Join-Path $env:TEMP "j2m-repository"))
    $private = [IO.Path]::GetFullPath((Join-Path $env:TEMP "j2m-evidence"))
    Assert-True (-not (Test-PathIsSameOrUnder -Candidate $private -Parent $root))
}

Invoke-Case "production wrapper contains no identity or backend mutation surface" {
    $source = Get-Content -LiteralPath `
        (Join-Path $PSScriptRoot "..\Export-SteamworksConfigurationExpectation.ps1") -Raw
    foreach ($token in @(
        "steamcmd.exe", "+login", "+run_app_build", "SetLive",
        "SteamGuard", "password", "ACH_WIN_ONE_GAME")) {
        Assert-True (-not $source.Contains($token)) "Forbidden token: $token"
    }
}

$script:Results | ForEach-Object { Write-Host $_ }
Write-Host "TOTAL=$($script:Passed + $script:Failed) PASS=$script:Passed FAIL=$script:Failed"
if ($script:Failed -ne 0) { exit 1 }
exit 0
