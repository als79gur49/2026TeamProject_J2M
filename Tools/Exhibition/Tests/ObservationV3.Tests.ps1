[CmdletBinding()]
param([Parameter(Mandatory)][string]$EvidenceRoot)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$source = Join-Path $root 'Assets/_Features/Exhibition/Integration'
if (Test-Path -LiteralPath $EvidenceRoot) { throw 'Use a fresh evidence directory.' }
if (-not $EvidenceRoot.StartsWith('D:\J2M\evidence\', [StringComparison]::OrdinalIgnoreCase)) { throw 'D evidence required.' }
New-Item -ItemType Directory -Path $EvidenceRoot | Out-Null
$buildRoot = Join-Path 'D:\J2M\builds\overlay-observation-v3-fakes' ([guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $buildRoot | Out-Null
$exe = Join-Path $buildRoot 'ObservationV3.PipeFakes.exe'
Add-Type -Path @("$source\RestartExperiment.cs", "$source\RestartExperimentWindows.cs", "$source\ObservationV3Wire.cs",
    "$source\ObservationV3Handoff.cs", "$source\ObservationV3Pipe.cs", (Join-Path $PSScriptRoot 'ObservationV3.PipeFakes.cs')) `
    -ReferencedAssemblies @('System.dll','System.Core.dll','System.Xml.dll','System.Runtime.Serialization.dll') -OutputAssembly $exe -OutputType ConsoleApplication
@{ Executable = $exe; Sha256 = (Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash.ToLowerInvariant() } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $EvidenceRoot 'fake-build.json') -Encoding UTF8
& $exe $EvidenceRoot
if ($LASTEXITCODE -ne 0) { throw "Windows pipe fake suite failed: $LASTEXITCODE" }

# Compile the exact immutable policy getter in both modes; this is not a Unity candidate build.
$policySource = [IO.File]::ReadAllText((Join-Path $root 'Assets/_Features/Stages/Runtime/Campaign/Save/CampaignSaveCompositionProvider.cs'))
$policy = [regex]::Match($policySource, 'public static bool ObservationBuild.*?(?=        private static bool observationOnly)', 'Singleline').Value
if (-not $policy) { throw 'Observation build policy not found.' }
foreach ($forced in @($false, $true)) {
    $name = 'Policy' + [guid]::NewGuid().ToString('N')
    $parameters = New-Object CodeDom.Compiler.CompilerParameters
    $parameters.GenerateInMemory = $true
    if ($forced) { $parameters.CompilerOptions = '/define:J2M_OVERLAY_OBSERVATION_ONLY' }
    $type = Add-Type -TypeDefinition ("public static class $name {`n" + $policy + "`n}") -CompilerParameters $parameters -PassThru
    if ($type.GetProperty('ObservationBuild').GetValue($null, $null) -ne $forced) { throw 'Immutable build policy mismatch.' }
    Write-Output "PASS exact build policy forced=$forced"
}
