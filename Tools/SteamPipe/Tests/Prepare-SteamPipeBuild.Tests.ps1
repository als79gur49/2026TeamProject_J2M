param([string]$PreserveSyntheticEvidenceRoot = "")

$ErrorActionPreference = "Stop"
$env:VECTORQUAKE_STEAMPIPE_DRYRUN_TEST_MODE = "1"
. (Join-Path $PSScriptRoot "..\Prepare-SteamPipeBuild.ps1")

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
        $script:Results += "FAIL $Name :: $($_.Exception.Message) :: $($_.ScriptStackTrace)"
    }
}

function Assert-True {
    param($Value, [string]$Message = "Expected true.")
    if (-not $Value) { throw $Message }
}

function Assert-Equal {
    param($Expected, $Actual, [string]$Message = "")
    if ($Expected -cne $Actual) {
        throw "Expected '$Expected', got '$Actual'. $Message"
    }
}

function Assert-ThrowsContaining {
    param([scriptblock]$Body, [string]$Expected)
    try {
        & $Body
    } catch {
        if ($_.Exception.Message.IndexOf(
                $Expected,
                [StringComparison]::OrdinalIgnoreCase) -lt 0) {
            throw "Expected '$Expected', got '$($_.Exception.Message)'."
        }
        return
    }
    throw "Expected '$Expected'."
}

function Write-Utf8File {
    param([string]$Path, [string]$Content)
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($Path)) | Out-Null
    [IO.File]::WriteAllText($Path, $Content, [Text.UTF8Encoding]::new($false))
}

function Write-TestJson {
    param($Value, [string]$Path)
    $json = ($Value | ConvertTo-Json -Depth 12).Replace("`r`n", "`n") + "`n"
    Write-Utf8File $Path $json
}

function Add-DuplicateJsonPropertyLine {
    param(
        [string]$Path,
        [string]$PropertyName,
        [string]$DuplicatePropertyName = $PropertyName,
        [string]$DuplicateJsonValue
    )

    $lines = [Collections.Generic.List[string]]::new()
    foreach ($line in ([IO.File]::ReadAllLines($Path))) {
        $trimmed = $line.TrimStart()
        if ($trimmed.StartsWith(
                '"' + $PropertyName + '":',
                [StringComparison]::Ordinal)) {
            $indent = $line.Substring(0, $line.Length - $trimmed.Length)
            $lines.Add(
                $indent + '"' + $DuplicatePropertyName + '":  ' +
                $DuplicateJsonValue + ',')
        }
        $lines.Add($line)
    }
    [IO.File]::WriteAllLines(
        $Path,
        $lines,
        [Text.UTF8Encoding]::new($false))
}

function New-PromotedFixture {
    param(
        [string]$Root,
        [string]$Target = "steam-windows",
        [string]$ScriptingBackend = "Mono2x",
        [bool]$IncludeUnityPlayer = $true,
        [bool]$IncludeNative = $true,
        [bool]$IncludeManaged = $true,
        [bool]$IncludeSteamAppId = $false,
        [bool]$IncludeDenied = $false
    )

    $payload = Join-Path $Root "payload"
    $evidence = Join-Path $Root "evidence"
    [IO.Directory]::CreateDirectory($payload) | Out-Null
    [IO.Directory]::CreateDirectory($evidence) | Out-Null
    Write-Utf8File (Join-Path $payload "VectorQuake.exe") "exe"
    if ($IncludeUnityPlayer) {
        Write-Utf8File (Join-Path $payload "UnityPlayer.dll") "unity"
    }
    Write-Utf8File (Join-Path $payload "VectorQuake_Data\globalgamemanagers") "managers"
    Write-Utf8File (Join-Path $payload "MonoBleedingEdge\etc\mono\config") "mono"
    if ($IncludeManaged) {
        Write-Utf8File `
            (Join-Path $payload "VectorQuake_Data\Managed\com.rlabrecque.steamworks.net.dll") `
            "managed"
    }
    if ($IncludeNative) {
        Write-Utf8File `
            (Join-Path $payload "VectorQuake_Data\Plugins\x86_64\steam_api64.dll") `
            "native"
    }
    if ($IncludeSteamAppId) {
        Write-Utf8File (Join-Path $payload "steam_appid.txt") "synthetic"
    }
    if ($IncludeDenied) {
        Write-Utf8File (Join-Path $payload "VectorQuake_Data\TestLogs\run.log") "denied"
    }

    $files = @(Get-ChildItem -LiteralPath $payload -File -Recurse |
        ForEach-Object {
            [ordered]@{
                relativePath = $_.FullName.Substring($payload.Length + 1).Replace('\', '/')
                size = $_.Length
                sha256 = Get-FileSha256 $_.FullName
            }
        } | Sort-Object { $_.relativePath })
    $totalBytes = [long]0
    foreach ($file in $files) {
        $totalBytes += [long]$file["size"]
    }
    $deniedCount = if ($IncludeDenied) { 1 } else { 0 }
    $provider = if ($Target -ceq "steam-windows") { "steam" } else { "local" }
    $arguments = if ($Target -ceq "steam-windows") {
        @("-j2mPlatformProvider", "steam")
    } else {
        @()
    }
    $manifest = [ordered]@{
        schemaVersion = "1.0"
        distributionTargetId = $Target
        sourceSha = "synthetic-source"
        sourceTree = "synthetic-tree"
        artifactId = "synthetic-artifact"
        runId = "synthetic-run"
        scriptingBackend = $ScriptingBackend
        expectedProviderId = $provider
        expectedLaunchArguments = $arguments
        deniedArtifactCount = $deniedCount
        fileCount = $files.Count
        totalBytes = $totalBytes
        files = $files
    }
    $manifestPath = Join-Path $evidence "distribution-manifest.json"
    Write-TestJson $manifest $manifestPath
    $success = [ordered]@{
        status = "SUCCESS"
        distributionTarget = $Target
        sourceSha = "synthetic-source"
        sourceTree = "synthetic-tree"
        scriptingBackend = $ScriptingBackend
        fileCount = $files.Count
        totalBytes = $totalBytes
        manifestSha256 = Get-FileSha256 $manifestPath
        sourceBuildImmutable = $true
        copyByteIdentity = $true
        promotedArtifactContractPassed = $true
        deniedArtifactCount = $deniedCount
    }
    Write-TestJson $success (Join-Path $evidence "SUCCESS.json")
    return $Root
}

function New-ValidArguments {
    param([string]$PromotedRoot, [string]$OutputRoot)
    return @{
        AppId = "900000001"
        DepotId = "900000002"
        PromotedSteamWindowsRoot = $PromotedRoot
        OutputRoot = $OutputRoot
        IdentityMode = "Synthetic"
        RepositoryRoot = $script:RepositoryRoot
        DryRun = $true
    }
}

function Assert-FinalOutputAbsent {
    param([string]$OutputRoot)
    Assert-True (-not (Test-Path -LiteralPath $OutputRoot))
    Assert-True (-not (Test-Path -LiteralPath `
        (Join-Path $OutputRoot "PRE_APPID_DRY_RUN_SUCCESS.json")))
}

$script:FixtureRoot = Join-Path ([IO.Path]::GetTempPath()) `
    ("j2m-steampipe-tests-" + [Guid]::NewGuid().ToString("N"))
$script:RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..\..")).Path
[IO.Directory]::CreateDirectory($script:FixtureRoot) | Out-Null

try {
    Invoke-Case "VDF writer and parser preserve quotes backslashes and canonical syntax" {
        $value = 'D:\Synthetic Folder\quoted"name'
        $node = New-VdfSection "Root" @((New-VdfPair "Path" $value))
        $text = ConvertTo-VdfText $node
        $parsed = ConvertFrom-VdfText $text
        Assert-Equal $value (Get-RequiredVdfPairValue $parsed "Path")
        Assert-True ($text.EndsWith("`n"))
        Assert-True (-not $text.Contains("`r"))
        Assert-ThrowsContaining { ConvertFrom-VdfText '"Root" { "A" "B"' } `
            "unbalanced braces"
    }

    Invoke-Case "VDF schema validator rejects missing duplicate and mismatched fields" {
        $contentRoot = 'D:\Synthetic promoted\payload'
        $buildOutput = 'D:\Synthetic evidence\build-output'
        $depotFile = 'depot_build_900000002.vdf'
        $valid = New-VdfSection "AppBuild" @(
            (New-VdfPair "AppID" "900000001"),
            (New-VdfPair "Desc" "SYNTHETIC_VALIDATION_ONLY"),
            (New-VdfPair "Preview" "1"),
            (New-VdfPair "ContentRoot" $contentRoot),
            (New-VdfPair "BuildOutput" $buildOutput),
            (New-VdfSection "Depots" @(
                (New-VdfPair "900000002" $depotFile)
            ))
        )
        $schemaArguments = @{
            ExpectedAppId = "900000001"
            ExpectedDepotId = "900000002"
            ExpectedDepotFile = $depotFile
            ExpectedContentRoot = $contentRoot
            ExpectedBuildOutput = $buildOutput
        }

        $missing = New-VdfSection "AppBuild" @(
            $valid.Entries | Where-Object { $_.Key -cne "AppID" })
        Assert-ThrowsContaining {
            Assert-AppVdfSchema -Root $missing @schemaArguments
        } "required pair AppID"

        $duplicate = New-VdfSection "AppBuild" @(
            @($valid.Entries) + (New-VdfPair "Preview" "1"))
        Assert-ThrowsContaining {
            Assert-AppVdfSchema -Root $duplicate @schemaArguments
        } "required pair Preview"

        $mismatch = New-VdfSection "AppBuild" @(
            $valid.Entries | ForEach-Object {
                if ($_.Key -ceq "Depots") {
                    New-VdfSection "Depots" @(
                        (New-VdfPair "900000002" "wrong-depot.vdf"))
                } else {
                    $_
                }
            })
        Assert-ThrowsContaining {
            Assert-AppVdfSchema -Root $mismatch @schemaArguments
        } "DEPOT_REFERENCE_MISMATCH"

        $depotMissingId = New-VdfSection "DepotBuild" @(
            (New-VdfSection "FileMapping" @(
                (New-VdfPair "LocalPath" "*"),
                (New-VdfPair "DepotPath" "."),
                (New-VdfPair "Recursive" "1")
            )),
            (New-VdfPair "FileExclusion" "steam_appid.txt")
        )
        Assert-ThrowsContaining {
            Assert-DepotVdfSchema -Root $depotMissingId `
                -ExpectedDepotId "900000002"
        } "required pair DepotID"
    }

    Invoke-Case "validator snapshot mutation fails before compile and cache load" {
        $stageWrapper = Join-Path $script:RepositoryRoot `
            "Tools\Build\Stage-WindowsDistribution.ps1"
        $previousMode = $env:VECTORQUAKE_DISTRIBUTION_STAGER_TEST_MODE
        $cacheRoot = Join-Path $script:FixtureRoot "snapshot-mutation-cache"
        try {
            $env:VECTORQUAKE_DISTRIBUTION_STAGER_TEST_MODE = "1"
            . $stageWrapper
        } finally {
            $env:VECTORQUAKE_DISTRIBUTION_STAGER_TEST_MODE = $previousMode
        }

        $script:OriginalValidatorCopy =
            (Get-Command Copy-WindowsDistributionValidatorSource).ScriptBlock
        $script:MutatedValidatorCopy = $false
        try {
            Set-Item Function:\Copy-WindowsDistributionValidatorSource {
                param([string]$SourcePath, [string]$DestinationPath)
                & $script:OriginalValidatorCopy `
                    -SourcePath $SourcePath `
                    -DestinationPath $DestinationPath
                if (-not $script:MutatedValidatorCopy) {
                    [IO.File]::AppendAllText($DestinationPath, "`n")
                    $script:MutatedValidatorCopy = $true
                }
            }
            Assert-ThrowsContaining {
                Import-WindowsDistributionStagerTypes `
                    -Root $script:RepositoryRoot `
                    -OfflineOnly `
                    -CacheRoot $cacheRoot
            } "STAGING_POLICY_SOURCE_SNAPSHOT_MISMATCH"
            $assemblies = @(Get-ChildItem -LiteralPath $cacheRoot `
                -Filter "VectorQuake.DistributionStager.dll" -File -Recurse `
                -ErrorAction SilentlyContinue | Where-Object {
                    $_.FullName -match `
                        '[\\/]bin[\\/]VectorQuake\.DistributionStager\.dll$'
                })
            Assert-Equal 0 $assemblies.Count
        } finally {
            Set-Item Function:\Copy-WindowsDistributionValidatorSource `
                $script:OriginalValidatorCopy
            Remove-Variable OriginalValidatorCopy `
                -Scope Script -ErrorAction SilentlyContinue
            Remove-Variable MutatedValidatorCopy `
                -Scope Script -ErrorAction SilentlyContinue
        }
    }

    Invoke-Case "post-compile snapshot mismatch removes cached assembly" {
        $stageWrapper = Join-Path $script:RepositoryRoot `
            "Tools\Build\Stage-WindowsDistribution.ps1"
        $previousMode = $env:VECTORQUAKE_DISTRIBUTION_STAGER_TEST_MODE
        try {
            $env:VECTORQUAKE_DISTRIBUTION_STAGER_TEST_MODE = "1"
            . $stageWrapper
        } finally {
            $env:VECTORQUAKE_DISTRIBUTION_STAGER_TEST_MODE = $previousMode
        }
        $cacheRoot = Join-Path ([IO.Path]::GetTempPath()) `
            "VectorQuakeDistributionStagerPostMismatch"
        if (Test-Path -LiteralPath $cacheRoot -PathType Container) {
            [IO.Directory]::Delete("\\?\$cacheRoot", $true)
        }
        $script:OriginalSourceHashAssertion =
            (Get-Command Assert-WindowsDistributionSourceHashes).ScriptBlock
        $script:SnapshotAssertionCount = 0
        $script:FinalAssemblyVisibleDuringVerification = $false
        try {
            Set-Item Function:\Assert-WindowsDistributionSourceHashes {
                param(
                    [string[]]$Paths,
                    [string[]]$ExpectedHashes,
                    [string]$FailureCode
                )
                if ($FailureCode -ceq `
                    "STAGING_POLICY_SOURCE_SNAPSHOT_MISMATCH") {
                    $script:SnapshotAssertionCount++
                    if ($script:SnapshotAssertionCount -eq 2) {
                        $compileRoot = [IO.Path]::GetDirectoryName($Paths[0])
                        $finalAssembly = Join-Path $compileRoot `
                            "bin\VectorQuake.DistributionStager.dll"
                        $script:FinalAssemblyVisibleDuringVerification =
                            Test-Path -LiteralPath $finalAssembly -PathType Leaf
                        throw $FailureCode
                    }
                }
                & $script:OriginalSourceHashAssertion `
                    -Paths $Paths `
                    -ExpectedHashes $ExpectedHashes `
                    -FailureCode $FailureCode
            }
            Assert-ThrowsContaining {
                Import-WindowsDistributionStagerTypes `
                    -Root $script:RepositoryRoot `
                    -OfflineOnly `
                    -CacheRoot $cacheRoot
            } "STAGING_POLICY_SOURCE_SNAPSHOT_MISMATCH"
            Assert-True (-not $script:FinalAssemblyVisibleDuringVerification)
            $assemblies = @(Get-ChildItem -LiteralPath $cacheRoot `
                -Filter "VectorQuake.DistributionStager.dll" -File -Recurse `
                -ErrorAction SilentlyContinue | Where-Object {
                    $_.FullName -match `
                        '[\\/]bin[\\/]VectorQuake\.DistributionStager\.dll$'
                })
            Assert-Equal 0 $assemblies.Count
        } finally {
            Set-Item Function:\Assert-WindowsDistributionSourceHashes `
                $script:OriginalSourceHashAssertion
            Remove-Variable OriginalSourceHashAssertion `
                -Scope Script -ErrorAction SilentlyContinue
            Remove-Variable SnapshotAssertionCount `
                -Scope Script -ErrorAction SilentlyContinue
            Remove-Variable FinalAssemblyVisibleDuringVerification `
                -Scope Script -ErrorAction SilentlyContinue
        }
    }

    Invoke-Case "concurrent cold imports serialize and share verified cache" {
        $stageWrapper = Join-Path $script:RepositoryRoot `
            "Tools\Build\Stage-WindowsDistribution.ps1"
        $cacheRoot = Join-Path ([IO.Path]::GetTempPath()) `
            "VectorQuakeDistributionStagerConcurrent"
        if (Test-Path -LiteralPath $cacheRoot -PathType Container) {
            [IO.Directory]::Delete("\\?\$cacheRoot", $true)
        }
        $jobArguments = [object[]]@(
            $stageWrapper,
            $script:RepositoryRoot,
            $cacheRoot)
        $jobs = @()
        for ($index = 0; $index -lt 2; $index++) {
            $jobs += Start-Job -ScriptBlock {
                param($Wrapper, $Repository, $Cache)
                $ErrorActionPreference = "Stop"
                Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass `
                    -Force
                $env:VECTORQUAKE_DISTRIBUTION_STAGER_TEST_MODE = "1"
                . $Wrapper -RepositoryRoot $Repository
                Import-WindowsDistributionStagerTypes `
                    -Root $Repository `
                    -OfflineOnly `
                    -CacheRoot $Cache
                "CONCURRENT_IMPORT_PASS"
            } -ArgumentList $jobArguments
        }
        try {
            $null = Wait-Job -Job $jobs -Timeout 120
            foreach ($job in $jobs) {
                $output = @($job | Receive-Job)
                Assert-Equal "Completed" $job.State
                Assert-True ($output -contains "CONCURRENT_IMPORT_PASS")
            }
            $sourceKeyCaches = @(Get-ChildItem -LiteralPath $cacheRoot -Directory)
            Assert-Equal 1 $sourceKeyCaches.Count
            $finalAssemblyPath = Join-Path $sourceKeyCaches[0].FullName `
                "bin\VectorQuake.DistributionStager.dll"
            Assert-True (Test-Path -LiteralPath $finalAssemblyPath -PathType Leaf)
            $preparing = @(Get-ChildItem `
                -LiteralPath $sourceKeyCaches[0].FullName `
                -Directory | Where-Object {
                    $_.Name.StartsWith(".preparing-bin-")
                })
            Assert-Equal 0 $preparing.Count
        } finally {
            $jobs | Remove-Job -Force -ErrorAction SilentlyContinue
        }
    }

    Invoke-Case "typed promoted validator cold cache compiles offline only" {
        $stageWrapper = Join-Path $script:RepositoryRoot `
            "Tools\Build\Stage-WindowsDistribution.ps1"
        $previousMode = $env:VECTORQUAKE_DISTRIBUTION_STAGER_TEST_MODE
        $previousPackages = $env:NUGET_PACKAGES
        $previousCertificateRevocation = $env:NUGET_CERT_REVOCATION_MODE
        $previousDotnetCliHome = $env:DOTNET_CLI_HOME
        $previousMsBuildSdksPath = $env:MSBuildSDKsPath
        $previousCustomBeforeCommonProps = `
            $env:CustomBeforeMicrosoftCommonProps
        $previousCustomAfterCommonProps = `
            $env:CustomAfterMicrosoftCommonProps
        $previousCustomBeforeCommonTargets = `
            $env:CustomBeforeMicrosoftCommonTargets
        $previousCustomAfterCommonTargets = `
            $env:CustomAfterMicrosoftCommonTargets
        $previousRestoreSources = $env:RestoreSources
        $previousRestoreAdditionalSources = `
            $env:RestoreAdditionalProjectSources
        $previousRestoreFallbackFolders = $env:RestoreFallbackFolders
        $previousRestoreAdditionalFallbackFolders = `
            $env:RestoreAdditionalProjectFallbackFolders
        $previousMsBuildExtensionsPath = $env:MSBuildExtensionsPath
        $previousMsBuildUserExtensionsPath = $env:MSBuildUserExtensionsPath
        $revocationAfterImport = ""
        $dotnetCliHomeAfterImport = ""
        $msBuildSdksPathAfterImport = ""
        $customBeforeCommonPropsAfterImport = ""
        $customAfterCommonPropsAfterImport = ""
        $customBeforeCommonTargetsAfterImport = ""
        $customAfterCommonTargetsAfterImport = ""
        $restoreSourcesAfterImport = ""
        $restoreAdditionalSourcesAfterImport = ""
        $restoreFallbackFoldersAfterImport = ""
        $restoreAdditionalFallbackFoldersAfterImport = ""
        $msBuildExtensionsPathAfterImport = ""
        $msBuildUserExtensionsPathAfterImport = ""
        $offlineCacheRoot = Join-Path ([IO.Path]::GetTempPath()) `
            "VectorQuakeDistributionStagerOfflineOnly"
        try {
            if (Test-Path -LiteralPath $offlineCacheRoot -PathType Container) {
                [IO.Directory]::Delete("\\?\$offlineCacheRoot", $true)
            }
            [IO.Directory]::CreateDirectory($offlineCacheRoot) | Out-Null
            Write-Utf8File `
                (Join-Path $offlineCacheRoot "Directory.Build.props") `
                @'
<Project>
  <Target Name="RejectUnexpectedDirectoryBuildProps" BeforeTargets="Restore;Build">
    <Error Text="SYNTHETIC_DIRECTORY_BUILD_PROPS_IMPORTED" />
  </Target>
</Project>
'@
            Write-Utf8File `
                (Join-Path $offlineCacheRoot "Directory.Build.targets") `
                @'
<Project>
  <Target Name="RejectUnexpectedDirectoryBuildTargets" BeforeTargets="Restore;Build">
    <Error Text="SYNTHETIC_DIRECTORY_BUILD_TARGETS_IMPORTED" />
  </Target>
</Project>
'@
            $env:VECTORQUAKE_DISTRIBUTION_STAGER_TEST_MODE = "1"
            $env:NUGET_PACKAGES = '\\synthetic.invalid\packages'
            $env:NUGET_CERT_REVOCATION_MODE = "synthetic-previous"
            $env:DOTNET_CLI_HOME = '\\synthetic.invalid\dotnet-cli-home'
            $env:MSBuildSDKsPath = '\\synthetic.invalid\msbuild-sdks'
            $env:CustomBeforeMicrosoftCommonProps = `
                '\\synthetic.invalid\before-common.props'
            $env:CustomAfterMicrosoftCommonProps = `
                '\\synthetic.invalid\after-common.props'
            $env:CustomBeforeMicrosoftCommonTargets = `
                '\\synthetic.invalid\before-common.targets'
            $env:CustomAfterMicrosoftCommonTargets = `
                '\\synthetic.invalid\after-common.targets'
            $env:RestoreSources = 'https://synthetic.invalid/v3/index.json'
            $env:RestoreAdditionalProjectSources = `
                '\\synthetic.invalid\additional-source'
            $env:RestoreFallbackFolders = `
                '\\synthetic.invalid\fallback-source'
            $env:RestoreAdditionalProjectFallbackFolders = `
                '\\synthetic.invalid\additional-fallback-source'
            $env:MSBuildExtensionsPath = `
                '\\synthetic.invalid\msbuild-extensions'
            $env:MSBuildUserExtensionsPath = `
                '\\synthetic.invalid\msbuild-user-extensions'
            . $stageWrapper
            Import-WindowsDistributionStagerTypes `
                -Root $script:RepositoryRoot `
                -OfflineOnly `
                -CacheRoot $offlineCacheRoot
        } finally {
            $revocationAfterImport = $env:NUGET_CERT_REVOCATION_MODE
            $dotnetCliHomeAfterImport = $env:DOTNET_CLI_HOME
            $msBuildSdksPathAfterImport = $env:MSBuildSDKsPath
            $customBeforeCommonPropsAfterImport = `
                $env:CustomBeforeMicrosoftCommonProps
            $customAfterCommonPropsAfterImport = `
                $env:CustomAfterMicrosoftCommonProps
            $customBeforeCommonTargetsAfterImport = `
                $env:CustomBeforeMicrosoftCommonTargets
            $customAfterCommonTargetsAfterImport = `
                $env:CustomAfterMicrosoftCommonTargets
            $restoreSourcesAfterImport = $env:RestoreSources
            $restoreAdditionalSourcesAfterImport = `
                $env:RestoreAdditionalProjectSources
            $restoreFallbackFoldersAfterImport = $env:RestoreFallbackFolders
            $restoreAdditionalFallbackFoldersAfterImport = `
                $env:RestoreAdditionalProjectFallbackFolders
            $msBuildExtensionsPathAfterImport = $env:MSBuildExtensionsPath
            $msBuildUserExtensionsPathAfterImport = `
                $env:MSBuildUserExtensionsPath
            $env:VECTORQUAKE_DISTRIBUTION_STAGER_TEST_MODE = $previousMode
            $env:NUGET_PACKAGES = $previousPackages
            $env:NUGET_CERT_REVOCATION_MODE = $previousCertificateRevocation
            $env:DOTNET_CLI_HOME = $previousDotnetCliHome
            $env:MSBuildSDKsPath = $previousMsBuildSdksPath
            $env:CustomBeforeMicrosoftCommonProps = `
                $previousCustomBeforeCommonProps
            $env:CustomAfterMicrosoftCommonProps = `
                $previousCustomAfterCommonProps
            $env:CustomBeforeMicrosoftCommonTargets = `
                $previousCustomBeforeCommonTargets
            $env:CustomAfterMicrosoftCommonTargets = `
                $previousCustomAfterCommonTargets
            $env:RestoreSources = $previousRestoreSources
            $env:RestoreAdditionalProjectSources = `
                $previousRestoreAdditionalSources
            $env:RestoreFallbackFolders = $previousRestoreFallbackFolders
            $env:RestoreAdditionalProjectFallbackFolders = `
                $previousRestoreAdditionalFallbackFolders
            $env:MSBuildExtensionsPath = $previousMsBuildExtensionsPath
            $env:MSBuildUserExtensionsPath = $previousMsBuildUserExtensionsPath
        }
        Assert-Equal "synthetic-previous" $revocationAfterImport
        Assert-Equal '\\synthetic.invalid\dotnet-cli-home' `
            $dotnetCliHomeAfterImport
        Assert-Equal '\\synthetic.invalid\msbuild-sdks' `
            $msBuildSdksPathAfterImport
        Assert-Equal '\\synthetic.invalid\before-common.props' `
            $customBeforeCommonPropsAfterImport
        Assert-Equal '\\synthetic.invalid\after-common.props' `
            $customAfterCommonPropsAfterImport
        Assert-Equal '\\synthetic.invalid\before-common.targets' `
            $customBeforeCommonTargetsAfterImport
        Assert-Equal '\\synthetic.invalid\after-common.targets' `
            $customAfterCommonTargetsAfterImport
        Assert-Equal 'https://synthetic.invalid/v3/index.json' `
            $restoreSourcesAfterImport
        Assert-Equal '\\synthetic.invalid\additional-source' `
            $restoreAdditionalSourcesAfterImport
        Assert-Equal '\\synthetic.invalid\fallback-source' `
            $restoreFallbackFoldersAfterImport
        Assert-Equal '\\synthetic.invalid\additional-fallback-source' `
            $restoreAdditionalFallbackFoldersAfterImport
        Assert-Equal '\\synthetic.invalid\msbuild-extensions' `
            $msBuildExtensionsPathAfterImport
        Assert-Equal '\\synthetic.invalid\msbuild-user-extensions' `
            $msBuildUserExtensionsPathAfterImport
        Assert-True ($null -ne ("WindowsDistributionStager" -as [type]))
        Assert-ThrowsContaining {
            Assert-WindowsDistributionStagerIdentity `
                -StagerType ([string]) `
                -ExpectedIdentity "synthetic-mismatch"
        } "STAGING_POLICY_LOADED_IDENTITY_MISMATCH"

        $stageSource = Get-Content -LiteralPath $stageWrapper -Raw
        Assert-True ($stageSource.Contains("--configfile"))
        Assert-True ($stageSource.Contains("--packages"))
        Assert-True ($stageSource.Contains("--no-restore"))
        Assert-True ($stageSource.Contains("NuGetAudit=false"))
        Assert-True ($stageSource.Contains(
            "DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE"))
        Assert-True ($stageSource.Contains(
            'NUGET_CERT_REVOCATION_MODE = "offline"'))
        Assert-True ($stageSource.Contains('DOTNET_CLI_HOME = $dotnetCliHome'))
        Assert-True ($stageSource.Contains('MSBuildSDKsPath = $null'))
        Assert-True ($stageSource.Contains(
            'CustomBeforeMicrosoftCommonProps = $null'))
        Assert-True ($stageSource.Contains(
            'CustomAfterMicrosoftCommonProps = $null'))
        Assert-True ($stageSource.Contains(
            'CustomBeforeMicrosoftCommonTargets = $null'))
        Assert-True ($stageSource.Contains(
            'CustomAfterMicrosoftCommonTargets = $null'))
        Assert-True ($stageSource.Contains(
            '"-p:RestoreSources=$offlineSource"'))
        Assert-True ($stageSource.Contains(
            '-p:RestoreAdditionalProjectSources='))
        Assert-True ($stageSource.Contains('-p:RestoreFallbackFolders='))
        Assert-True ($stageSource.Contains(
            '-p:RestoreAdditionalProjectFallbackFolders='))
        Assert-True ($stageSource.Contains(
            '-p:ImportByWildcardBeforeMicrosoftCommonProps=false'))
        Assert-True ($stageSource.Contains(
            '-p:ImportByWildcardAfterMicrosoftCommonProps=false'))
        Assert-True ($stageSource.Contains(
            '-p:ImportByWildcardBeforeMicrosoftCommonTargets=false'))
        Assert-True ($stageSource.Contains(
            '-p:ImportByWildcardAfterMicrosoftCommonTargets=false'))
        Assert-True ($stageSource.Contains(
            '-p:ImportDirectoryBuildProps=false'))
        Assert-True ($stageSource.Contains(
            '-p:ImportDirectoryBuildTargets=false'))
        $assetsPath = Get-ChildItem -LiteralPath $offlineCacheRoot `
            -Filter "project.assets.json" -File -Recurse | Select-Object -First 1
        Assert-True ($null -ne $assetsPath)
        $assets = Get-Content -LiteralPath $assetsPath.FullName -Raw |
            ConvertFrom-Json
        foreach ($packageRoot in @($assets.packageFolders.psobject.Properties.Name)) {
            Assert-True (Test-PathIsSameOrUnder `
                -Candidate $packageRoot `
                -Parent $offlineCacheRoot) `
                "Restore used package root outside validated cache: $packageRoot"
        }
        $productionSource = Get-Content -LiteralPath `
            (Join-Path $PSScriptRoot "..\Prepare-SteamPipeBuild.ps1") -Raw
        Assert-True ($productionSource.Contains(
            "Import-WindowsDistributionStagerTypes -Root `$Root -OfflineOnly"))
    }

    Invoke-Case "loaded validator reuse rechecks live canonical sources" {
        $stageWrapper = Join-Path $script:RepositoryRoot `
            "Tools\Build\Stage-WindowsDistribution.ps1"
        $previousMode = $env:VECTORQUAKE_DISTRIBUTION_STAGER_TEST_MODE
        try {
            $env:VECTORQUAKE_DISTRIBUTION_STAGER_TEST_MODE = "1"
            . $stageWrapper
        } finally {
            $env:VECTORQUAKE_DISTRIBUTION_STAGER_TEST_MODE = $previousMode
        }

        $copyRoot = Join-Path $script:FixtureRoot "loaded-reuse-source-root"
        foreach ($relativePath in @(
            "Tools\Build\Stage-WindowsDistribution.ps1",
            "Assets\_Features\Stages\Editor\Build\WindowsDistributionTargetPolicy.cs",
            "Assets\_Features\Stages\Editor\Build\SteamPipeStagingSanitizerPolicy.cs",
            "Assets\_Features\Stages\Editor\Build\WindowsDistributionStager.cs")) {
            $source = Join-Path $script:RepositoryRoot $relativePath
            $destination = Join-Path $copyRoot $relativePath
            [IO.Directory]::CreateDirectory(
                [IO.Path]::GetDirectoryName($destination)) | Out-Null
            [IO.File]::Copy($source, $destination, $true)
        }

        $script:OriginalIdentityAssertion =
            (Get-Command Assert-WindowsDistributionStagerIdentity).ScriptBlock
        $script:LoadedReuseMutationPath = Join-Path $copyRoot `
            "Assets\_Features\Stages\Editor\Build\WindowsDistributionTargetPolicy.cs"
        $script:LoadedReuseSourceMutated = $false
        try {
            Set-Item Function:\Assert-WindowsDistributionStagerIdentity {
                param([type]$StagerType, [string]$ExpectedIdentity)
                & $script:OriginalIdentityAssertion `
                    -StagerType $StagerType `
                    -ExpectedIdentity $ExpectedIdentity
                if (-not $script:LoadedReuseSourceMutated) {
                    [IO.File]::AppendAllText(
                        $script:LoadedReuseMutationPath,
                        "`n",
                        [Text.UTF8Encoding]::new($false))
                    $script:LoadedReuseSourceMutated = $true
                }
            }
            Assert-ThrowsContaining {
                Import-WindowsDistributionStagerTypes `
                    -Root $copyRoot `
                    -OfflineOnly `
                    -CacheRoot (Join-Path $script:FixtureRoot `
                        "loaded-reuse-source-cache")
            } "STAGING_POLICY_LIVE_SOURCE_MISMATCH"
        } finally {
            Set-Item Function:\Assert-WindowsDistributionStagerIdentity `
                $script:OriginalIdentityAssertion
            Remove-Variable OriginalIdentityAssertion `
                -Scope Script -ErrorAction SilentlyContinue
            Remove-Variable LoadedReuseMutationPath `
                -Scope Script -ErrorAction SilentlyContinue
            Remove-Variable LoadedReuseSourceMutated `
                -Scope Script -ErrorAction SilentlyContinue
        }
    }

    Invoke-Case "cached validator post-load rechecks live canonical sources" {
        $copyRoot = Join-Path $script:FixtureRoot "post-load-source-root"
        foreach ($relativePath in @(
            "Tools\Build\Stage-WindowsDistribution.ps1",
            "Assets\_Features\Stages\Editor\Build\WindowsDistributionTargetPolicy.cs",
            "Assets\_Features\Stages\Editor\Build\SteamPipeStagingSanitizerPolicy.cs",
            "Assets\_Features\Stages\Editor\Build\WindowsDistributionStager.cs")) {
            $source = Join-Path $script:RepositoryRoot $relativePath
            $destination = Join-Path $copyRoot $relativePath
            [IO.Directory]::CreateDirectory(
                [IO.Path]::GetDirectoryName($destination)) | Out-Null
            [IO.File]::Copy($source, $destination, $true)
        }
        $cacheRoot = Join-Path ([IO.Path]::GetTempPath()) `
            "VectorQuakeDistributionStagerOfflineOnly"
        $mutationPath = Join-Path $copyRoot `
            "Assets\_Features\Stages\Editor\Build\WindowsDistributionTargetPolicy.cs"
        $job = Start-Job -ScriptBlock {
            param($Repository, $Cache, $MutationPath)
            $ErrorActionPreference = "Stop"
            Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass -Force
            $env:VECTORQUAKE_DISTRIBUTION_STAGER_TEST_MODE = "1"
            . (Join-Path $Repository `
                "Tools\Build\Stage-WindowsDistribution.ps1") `
                -RepositoryRoot $Repository
            $script:OriginalPostLoadIdentityAssertion =
                (Get-Command Assert-WindowsDistributionStagerIdentity).ScriptBlock
            Set-Item Function:\Assert-WindowsDistributionStagerIdentity {
                param([type]$StagerType, [string]$ExpectedIdentity)
                & $script:OriginalPostLoadIdentityAssertion `
                    -StagerType $StagerType `
                    -ExpectedIdentity $ExpectedIdentity
                [IO.File]::AppendAllText(
                    $MutationPath,
                    "`n",
                    [Text.UTF8Encoding]::new($false))
            }
            try {
                Import-WindowsDistributionStagerTypes `
                    -Root $Repository `
                    -OfflineOnly `
                    -CacheRoot $Cache
            } catch {
                if ($_.Exception.Message.Contains(
                    "STAGING_POLICY_LIVE_SOURCE_MISMATCH")) {
                    "POST_LOAD_SOURCE_RECHECK_PASS"
                    return
                }
                throw
            }
            throw "Expected STAGING_POLICY_LIVE_SOURCE_MISMATCH."
        } -ArgumentList $copyRoot, $cacheRoot, $mutationPath
        try {
            $null = Wait-Job -Job $job -Timeout 120
            $output = @($job | Receive-Job)
            Assert-Equal "Completed" $job.State
            Assert-True ($output -contains "POST_LOAD_SOURCE_RECHECK_PASS")
        } finally {
            $job | Remove-Job -Force -ErrorAction SilentlyContinue
        }
    }

    Invoke-Case "validator cache rejects mapped drives and reparse ancestors" {
        $stageWrapper = Join-Path $script:RepositoryRoot `
            "Tools\Build\Stage-WindowsDistribution.ps1"
        $previousMode = $env:VECTORQUAKE_DISTRIBUTION_STAGER_TEST_MODE
        try {
            $env:VECTORQUAKE_DISTRIBUTION_STAGER_TEST_MODE = "1"
            . $stageWrapper
        } finally {
            $env:VECTORQUAKE_DISTRIBUTION_STAGER_TEST_MODE = $previousMode
        }

        $script:OriginalDistributionDriveType =
            (Get-Command Get-WindowsDistributionDriveType).ScriptBlock
        try {
            Set-Item Function:\Get-WindowsDistributionDriveType {
                param([string]$Path)
                return [IO.DriveType]::Network
            }
            Assert-ThrowsContaining {
                Import-WindowsDistributionStagerTypes `
                    -Root $script:RepositoryRoot `
                    -OfflineOnly
            } "STAGING_POLICY_VALIDATOR_NETWORK_PATH_REJECTED"
        } finally {
            Set-Item Function:\Get-WindowsDistributionDriveType `
                $script:OriginalDistributionDriveType
            Remove-Variable OriginalDistributionDriveType `
                -Scope Script -ErrorAction SilentlyContinue
        }

        $cacheTarget = Join-Path $script:FixtureRoot "validator-cache-target"
        $cacheJunction = Join-Path $script:FixtureRoot "validator-cache-junction"
        [IO.Directory]::CreateDirectory($cacheTarget) | Out-Null
        New-Item -ItemType Junction -Path $cacheJunction -Target $cacheTarget |
            Out-Null
        try {
            Assert-ThrowsContaining {
                Assert-WindowsDistributionLocalValidatorPath `
                    -Path (Join-Path $cacheJunction "compiled-validator") `
                    -Name "synthetic validator cache"
            } "STAGING_POLICY_VALIDATOR_REPARSE_PATH_REJECTED"
        } finally {
            Remove-Item -LiteralPath $cacheJunction -Force
        }

        $nestedCache = Join-Path $script:FixtureRoot "validator-nested-cache"
        $nestedTarget = Join-Path $script:FixtureRoot "validator-nested-target"
        $nestedBin = Join-Path $nestedCache "bin"
        [IO.Directory]::CreateDirectory($nestedCache) | Out-Null
        [IO.Directory]::CreateDirectory($nestedTarget) | Out-Null
        New-Item -ItemType Junction -Path $nestedBin -Target $nestedTarget |
            Out-Null
        try {
            Assert-ThrowsContaining {
                Assert-WindowsDistributionLocalValidatorTree `
                    -Path $nestedCache `
                    -Name "synthetic nested validator cache"
            } "STAGING_POLICY_VALIDATOR_REPARSE_PATH_REJECTED"
            Assert-ThrowsContaining {
                Assert-WindowsDistributionLocalValidatorPath `
                    -Path (Join-Path $nestedBin "validator.dll") `
                    -Name "synthetic compiled validator"
            } "STAGING_POLICY_VALIDATOR_REPARSE_PATH_REJECTED"
        } finally {
            Remove-Item -LiteralPath $nestedBin -Force
        }

        $script:OriginalDotnetResolver =
            (Get-Command Resolve-WindowsDistributionDotnetPath).ScriptBlock
        $script:OriginalDistributionDriveType =
            (Get-Command Get-WindowsDistributionDriveType).ScriptBlock
        try {
            Set-Item Function:\Resolve-WindowsDistributionDotnetPath {
                return 'Z:\tools\dotnet.exe'
            }
            Set-Item Function:\Get-WindowsDistributionDriveType {
                param([string]$Path)
                if ($Path.StartsWith("Z:", [StringComparison]::OrdinalIgnoreCase)) {
                    return [IO.DriveType]::Network
                }
                return & $script:OriginalDistributionDriveType -Path $Path
            }
            Assert-ThrowsContaining {
                Get-ValidatedWindowsDistributionDotnetPath
            } "STAGING_POLICY_VALIDATOR_NETWORK_PATH_REJECTED"
        } finally {
            Set-Item Function:\Resolve-WindowsDistributionDotnetPath `
                $script:OriginalDotnetResolver
            Set-Item Function:\Get-WindowsDistributionDriveType `
                $script:OriginalDistributionDriveType
            Remove-Variable OriginalDotnetResolver `
                -Scope Script -ErrorAction SilentlyContinue
            Remove-Variable OriginalDistributionDriveType `
                -Scope Script -ErrorAction SilentlyContinue
        }
    }

    Invoke-Case "foreign loaded validator identity is rejected by outer importer" {
        $foreignRoot = Join-Path $script:FixtureRoot "foreign-validator-root"
        foreach ($relativePath in @(
            "Tools\Build\Stage-WindowsDistribution.ps1",
            "Assets\_Features\Stages\Editor\Build\WindowsDistributionTargetPolicy.cs",
            "Assets\_Features\Stages\Editor\Build\SteamPipeStagingSanitizerPolicy.cs",
            "Assets\_Features\Stages\Editor\Build\WindowsDistributionStager.cs")) {
            $source = Join-Path $script:RepositoryRoot $relativePath
            $destination = Join-Path $foreignRoot $relativePath
            [IO.Directory]::CreateDirectory(
                [IO.Path]::GetDirectoryName($destination)) | Out-Null
            [IO.File]::Copy($source, $destination, $true)
        }
        $foreignPolicy = Join-Path $foreignRoot `
            "Assets\_Features\Stages\Editor\Build\WindowsDistributionTargetPolicy.cs"
        Write-Utf8File $foreignPolicy `
            (([IO.File]::ReadAllText($foreignPolicy)) + "`n")

        Assert-ThrowsContaining {
            Import-WindowsDistributionValidationTypes -Root $foreignRoot
        } "STAGING_POLICY_LOADED_IDENTITY_MISMATCH"
    }

    Invoke-Case "repository wrapper and validator source reparse descendants are rejected" {
        $wrapperLinkRoot = Join-Path $script:FixtureRoot "wrapper-link-repository"
        [IO.Directory]::CreateDirectory($wrapperLinkRoot) | Out-Null
        $toolsLink = Join-Path $wrapperLinkRoot "Tools"
        New-Item -ItemType Junction -Path $toolsLink `
            -Target (Join-Path $script:RepositoryRoot "Tools") | Out-Null
        try {
            Assert-ThrowsContaining {
                Import-WindowsDistributionValidationTypes -Root $wrapperLinkRoot
            } "STEAMPIPE_REPARSE_POINT_REJECTED"
        } finally {
            if ([IO.Directory]::Exists($toolsLink)) {
                [IO.Directory]::Delete($toolsLink)
            }
        }

        $sourceLinkRoot = Join-Path $script:FixtureRoot "source-link-repository"
        $wrapperSource = Join-Path $script:RepositoryRoot `
            "Tools\Build\Stage-WindowsDistribution.ps1"
        $wrapperDestination = Join-Path $sourceLinkRoot `
            "Tools\Build\Stage-WindowsDistribution.ps1"
        [IO.Directory]::CreateDirectory(
            [IO.Path]::GetDirectoryName($wrapperDestination)) | Out-Null
        [IO.File]::Copy($wrapperSource, $wrapperDestination, $true)
        $assetsLink = Join-Path $sourceLinkRoot "Assets"
        New-Item -ItemType Junction -Path $assetsLink `
            -Target (Join-Path $script:RepositoryRoot "Assets") | Out-Null
        try {
            Assert-ThrowsContaining {
                Import-WindowsDistributionValidationTypes -Root $sourceLinkRoot
            } "STAGING_POLICY_VALIDATOR_REPARSE_PATH_REJECTED"
        } finally {
            if ([IO.Directory]::Exists($assetsLink)) {
                [IO.Directory]::Delete($assetsLink)
            }
        }
    }

    Invoke-Case "promoted payload and evidence reparse descendants are rejected before reads" {
        $target = New-PromotedFixture `
            (Join-Path $script:FixtureRoot "promoted-reparse-target")

        $evidenceLinkRoot = Join-Path $script:FixtureRoot "evidence-link-promoted"
        [IO.Directory]::CreateDirectory(
            (Join-Path $evidenceLinkRoot "payload")) | Out-Null
        $evidenceLink = Join-Path $evidenceLinkRoot "evidence"
        New-Item -ItemType Junction -Path $evidenceLink `
            -Target (Join-Path $target "evidence") | Out-Null
        try {
            $output = Join-Path $script:FixtureRoot "evidence-link-output"
            $arguments = New-ValidArguments $evidenceLinkRoot $output
            Assert-ThrowsContaining {
                Invoke-PrepareSteamPipeBuild @arguments
            } "STEAMPIPE_REPARSE_POINT_REJECTED"
            Assert-FinalOutputAbsent $output
        } finally {
            if ([IO.Directory]::Exists($evidenceLink)) {
                [IO.Directory]::Delete($evidenceLink)
            }
        }

        $payloadLinkRoot = Join-Path $script:FixtureRoot "payload-link-promoted"
        [IO.Directory]::CreateDirectory($payloadLinkRoot) | Out-Null
        [IO.Directory]::CreateDirectory(
            (Join-Path $payloadLinkRoot "evidence")) | Out-Null
        $payloadLink = Join-Path $payloadLinkRoot "payload"
        New-Item -ItemType Junction -Path $payloadLink `
            -Target (Join-Path $target "payload") | Out-Null
        try {
            $output = Join-Path $script:FixtureRoot "payload-link-output"
            $arguments = New-ValidArguments $payloadLinkRoot $output
            Assert-ThrowsContaining {
                Invoke-PrepareSteamPipeBuild @arguments
            } "STEAMPIPE_REPARSE_POINT_REJECTED"
            Assert-FinalOutputAbsent $output
        } finally {
            if ([IO.Directory]::Exists($payloadLink)) {
                [IO.Directory]::Delete($payloadLink)
            }
        }
    }

    Invoke-Case "synthetic valid promoted artifact produces preview-only dry-run" {
        $promoted = New-PromotedFixture (Join-Path $script:FixtureRoot "valid-promoted")
        $output = Join-Path $script:FixtureRoot "valid-output"
        $arguments = New-ValidArguments $promoted $output
        $arguments.IdentityMode = "synthetic"
        $result = Invoke-PrepareSteamPipeBuild @arguments
        Assert-Equal "SYNTHETIC_VALIDATION_ONLY" $result.Classification
        Assert-True (Test-Path -LiteralPath $result.AppVdf -PathType Leaf)
        Assert-True (Test-Path -LiteralPath $result.DepotVdf -PathType Leaf)
        $app = Get-Content -LiteralPath $result.AppVdf -Raw
        Assert-True ($app.Contains('"Preview"    "1"'))
        Assert-True (-not $app.Contains("SetLive"))
        Assert-True ($app.Contains((ConvertTo-VdfQuoted (Join-Path $promoted "payload"))))
        Assert-True (-not $app.Contains((ConvertTo-VdfQuoted (Join-Path $promoted "evidence"))))
        $success = Get-Content `
            (Join-Path $output "PRE_APPID_DRY_RUN_SUCCESS.json") -Raw | ConvertFrom-Json
        Assert-Equal "NOT_UPLOADABLE" ([string]$success.uploadAuthority)
        Assert-Equal "NOT_ACTUAL_STEAM_IDENTITY" ([string]$success.identityAuthority)
        Assert-Equal "NO_STEAM_BACKEND_CONTACT" ([string]$success.backendContact)
        Assert-True (-not $success.networkOperation)
        Assert-True (-not $success.steamCmdExecuted)
    }

    Invoke-Case "same input and output policy produces deterministic VDF bytes" {
        $promoted = New-PromotedFixture (Join-Path $script:FixtureRoot "det-promoted")
        $output = Join-Path $script:FixtureRoot "det-output"
        $arguments = New-ValidArguments $promoted $output
        $first = Invoke-PrepareSteamPipeBuild @arguments
        $appFirst = [IO.File]::ReadAllBytes($first.AppVdf)
        $depotFirst = [IO.File]::ReadAllBytes($first.DepotVdf)
        $hashFirst = $first.VdfAggregateSha256
        Remove-Item -LiteralPath $output -Recurse -Force
        $second = Invoke-PrepareSteamPipeBuild @arguments
        Assert-True ([Linq.Enumerable]::SequenceEqual(
            [byte[]]$appFirst, [byte[]][IO.File]::ReadAllBytes($second.AppVdf)))
        Assert-True ([Linq.Enumerable]::SequenceEqual(
            [byte[]]$depotFirst, [byte[]][IO.File]::ReadAllBytes($second.DepotVdf)))
        Assert-Equal $hashFirst $second.VdfAggregateSha256
    }

    Invoke-Case "AppID invalid matrix fails closed" {
        $promoted = New-PromotedFixture (Join-Path $script:FixtureRoot "appid-promoted")
        foreach ($value in @("", "0", "-1", "not-a-number", "4294967296")) {
            $output = Join-Path $script:FixtureRoot ("appid-output-" + [Guid]::NewGuid().ToString("N"))
            $arguments = New-ValidArguments $promoted $output
            $arguments.AppId = $value
            Assert-ThrowsContaining { Invoke-PrepareSteamPipeBuild @arguments } "STEAMPIPE_ID_INVALID"
            Assert-FinalOutputAbsent $output
        }
    }

    Invoke-Case "DepotID invalid matrix fails closed" {
        $promoted = New-PromotedFixture (Join-Path $script:FixtureRoot "depotid-promoted")
        foreach ($value in @("", "0", "-1", "not-a-number", "4294967296")) {
            $output = Join-Path $script:FixtureRoot ("depotid-output-" + [Guid]::NewGuid().ToString("N"))
            $arguments = New-ValidArguments $promoted $output
            $arguments.DepotId = $value
            Assert-ThrowsContaining { Invoke-PrepareSteamPipeBuild @arguments } "STEAMPIPE_ID_INVALID"
            Assert-FinalOutputAbsent $output
        }
    }

    Invoke-Case "Actual mode rejects Spacewar AppID" {
        $promoted = New-PromotedFixture (Join-Path $script:FixtureRoot "actual-promoted")
        $output = Join-Path $script:FixtureRoot "actual-output"
        $arguments = New-ValidArguments $promoted $output
        $arguments.AppId = "480"
        foreach ($mode in @("Actual", "actual", "ACTUAL")) {
            $arguments.IdentityMode = $mode
            Assert-ThrowsContaining { Invoke-PrepareSteamPipeBuild @arguments } `
                "STEAMPIPE_SPACEWAR_APPID_REJECTED"
            Assert-FinalOutputAbsent $output
        }
    }

    Invoke-Case "raw Player input is rejected" {
        $raw = Join-Path $script:FixtureRoot "raw-player"
        Write-Utf8File (Join-Path $raw "VectorQuake.exe") "raw"
        $output = Join-Path $script:FixtureRoot "raw-output"
        $arguments = New-ValidArguments $raw $output
        Assert-ThrowsContaining {
            Invoke-PrepareSteamPipeBuild @arguments
        } "STEAMPIPE_PROMOTED_EVIDENCE_MISSING"
        Assert-FinalOutputAbsent $output
    }

    Invoke-Case "DirectWindows promoted target is rejected" {
        $promoted = New-PromotedFixture `
            (Join-Path $script:FixtureRoot "direct-promoted") `
            -Target "direct-windows" -IncludeNative $false -IncludeManaged $false
        $output = Join-Path $script:FixtureRoot "direct-output"
        $arguments = New-ValidArguments $promoted $output
        Assert-ThrowsContaining {
            Invoke-PrepareSteamPipeBuild @arguments
        } "STEAMPIPE_PROMOTED_TARGET_REJECTED"
        Assert-FinalOutputAbsent $output
    }

    Invoke-Case "manifest mismatch is rejected" {
        $promoted = New-PromotedFixture (Join-Path $script:FixtureRoot "mismatch-promoted")
        Write-Utf8File (Join-Path $promoted "payload\VectorQuake.exe") "tampered"
        $output = Join-Path $script:FixtureRoot "mismatch-output"
        $arguments = New-ValidArguments $promoted $output
        Assert-ThrowsContaining {
            Invoke-PrepareSteamPipeBuild @arguments
        } "STAGING_PROMOTED_MANIFEST_MISMATCH"
        Assert-FinalOutputAbsent $output
    }

    Invoke-Case "promoted JSON counters require numeric integer tokens" {
        $mutations = @(
            [pscustomobject]@{ Document = "manifest"; Field = "fileCount" },
            [pscustomobject]@{ Document = "manifest"; Field = "totalBytes" },
            [pscustomobject]@{ Document = "manifest"; Field = "deniedArtifactCount" },
            [pscustomobject]@{ Document = "manifestFile"; Field = "size" },
            [pscustomobject]@{ Document = "success"; Field = "fileCount" },
            [pscustomobject]@{ Document = "success"; Field = "totalBytes" },
            [pscustomobject]@{ Document = "success"; Field = "deniedArtifactCount" }
        )
        foreach ($mutation in $mutations) {
            $caseName = "$($mutation.Document)-$($mutation.Field)"
            $promoted = New-PromotedFixture `
                (Join-Path $script:FixtureRoot "numeric-token-$caseName")
            $manifestPath = Join-Path $promoted `
                "evidence\distribution-manifest.json"
            $successPath = Join-Path $promoted "evidence\SUCCESS.json"
            $manifest = Get-Content -LiteralPath $manifestPath -Raw |
                ConvertFrom-Json
            $success = Get-Content -LiteralPath $successPath -Raw |
                ConvertFrom-Json
            if ($mutation.Document -ceq "manifest") {
                $manifest.($mutation.Field) =
                    [string]$manifest.($mutation.Field)
            } elseif ($mutation.Document -ceq "manifestFile") {
                $manifest.files[0].($mutation.Field) =
                    [string]$manifest.files[0].($mutation.Field)
            } else {
                $success.($mutation.Field) =
                    [string]$success.($mutation.Field)
            }
            Write-TestJson $manifest $manifestPath
            $success.manifestSha256 = Get-FileSha256 $manifestPath
            Write-TestJson $success $successPath

            $output = Join-Path $script:FixtureRoot `
                "numeric-token-output-$caseName"
            $arguments = New-ValidArguments $promoted $output
            Assert-ThrowsContaining {
                Invoke-PrepareSteamPipeBuild @arguments
            } "STEAMPIPE_PROMOTED_EVIDENCE_INVALID"
            Assert-FinalOutputAbsent $output
        }
    }

    Invoke-Case "promoted source provenance requires JSON string tokens" {
        $malformedValues = @(
            123,
            $true,
            [pscustomobject]@{ value = "synthetic" }
        )
        foreach ($field in @("sourceSha", "sourceTree")) {
            for ($index = 0; $index -lt $malformedValues.Count; $index++) {
                $promoted = New-PromotedFixture `
                    (Join-Path $script:FixtureRoot `
                        "provenance-token-$field-$index")
                $manifestPath = Join-Path $promoted `
                    "evidence\distribution-manifest.json"
                $successPath = Join-Path $promoted "evidence\SUCCESS.json"
                $manifest = Get-Content -LiteralPath $manifestPath -Raw |
                    ConvertFrom-Json
                $success = Get-Content -LiteralPath $successPath -Raw |
                    ConvertFrom-Json
                $manifest.$field = $malformedValues[$index]
                $success.$field = $malformedValues[$index]
                Write-TestJson $manifest $manifestPath
                $success.manifestSha256 = Get-FileSha256 $manifestPath
                Write-TestJson $success $successPath

                $output = Join-Path $script:FixtureRoot `
                    "provenance-token-output-$field-$index"
                $arguments = New-ValidArguments $promoted $output
                Assert-ThrowsContaining {
                    Invoke-PrepareSteamPipeBuild @arguments
                } "STEAMPIPE_PROMOTED_EVIDENCE_INVALID"
                Assert-FinalOutputAbsent $output
            }
        }
    }

    Invoke-Case "promoted JSON rejects duplicate properties before materialization" {
        $cases = @(
            [pscustomobject]@{
                Document = "manifest"
                Property = "sourceSha"
                DuplicateValue = "123"
            },
            [pscustomobject]@{
                Document = "success"
                Property = "manifestSha256"
                DuplicateValue = "false"
            },
            [pscustomobject]@{
                Document = "manifest"
                Property = "size"
                DuplicateValue = '"0"'
            },
            [pscustomobject]@{
                Document = "manifest"
                Property = "SourceSha"
                SourceProperty = "sourceSha"
                DuplicateValue = '"case-variant"'
            }
        )
        foreach ($case in $cases) {
            $caseName = "$($case.Document)-$($case.Property)"
            $promoted = New-PromotedFixture `
                (Join-Path $script:FixtureRoot "duplicate-json-$caseName")
            $manifestPath = Join-Path $promoted `
                "evidence\distribution-manifest.json"
            $successPath = Join-Path $promoted "evidence\SUCCESS.json"
            $targetPath = if ($case.Document -ceq "manifest") {
                $manifestPath
            } else {
                $successPath
            }
            Add-DuplicateJsonPropertyLine `
                -Path $targetPath `
                -PropertyName $(if ($null -ne $case.SourceProperty) {
                    $case.SourceProperty
                } else {
                    $case.Property
                }) `
                -DuplicatePropertyName $case.Property `
                -DuplicateJsonValue $case.DuplicateValue
            if ($case.Document -ceq "manifest") {
                $success = Get-Content -LiteralPath $successPath -Raw |
                    ConvertFrom-Json
                $success.manifestSha256 = Get-FileSha256 $manifestPath
                Write-TestJson $success $successPath
            }

            $output = Join-Path $script:FixtureRoot `
                "duplicate-json-output-$caseName"
            $arguments = New-ValidArguments $promoted $output
            Assert-ThrowsContaining {
                Invoke-PrepareSteamPipeBuild @arguments
            } "STEAMPIPE_PROMOTED_EVIDENCE_DUPLICATE_PROPERTY"
            Assert-FinalOutputAbsent $output
        }
    }

    Invoke-Case "SUCCESS parse and hash use one byte snapshot" {
        $promoted = New-PromotedFixture `
            (Join-Path $script:FixtureRoot "success-snapshot-promoted")
        $successPath = Join-Path $promoted "evidence\SUCCESS.json"
        $originalSuccessBytes = [IO.File]::ReadAllBytes($successPath)
        $script:OriginalJsonSnapshotReader =
            (Get-Command Read-JsonFileSnapshot).ScriptBlock
        $script:CapturedSuccessSnapshotHash = ""
        $script:MutatedSuccessAfterSnapshot = $false
        try {
            Set-Item Function:\Read-JsonFileSnapshot {
                param([string]$Path)
                $snapshot = & $script:OriginalJsonSnapshotReader -Path $Path
                if ([IO.Path]::GetFileName($Path) -ceq "SUCCESS.json" -and
                    -not $script:MutatedSuccessAfterSnapshot) {
                    $script:CapturedSuccessSnapshotHash = $snapshot.Sha256
                    [IO.File]::WriteAllText(
                        $Path,
                        '{"distributionTarget":"steam-windows","status":"MUTATED_AFTER_SNAPSHOT"}',
                        [Text.UTF8Encoding]::new($false))
                    $script:MutatedSuccessAfterSnapshot = $true
                }
                return $snapshot
            }
            $preflight = Invoke-PromotedSteamWindowsPreflight `
                -PromotedRoot $promoted `
                -RepositoryRoot $script:RepositoryRoot
            Assert-Equal `
                $script:CapturedSuccessSnapshotHash `
                $preflight.SuccessSha256
            Assert-True ($preflight.SuccessSha256 -cne `
                (Get-FileSha256 -Path $successPath))
            [IO.File]::WriteAllBytes($successPath, $originalSuccessBytes)
            $script:MutatedSuccessAfterSnapshot = $false
            $output = Join-Path $script:FixtureRoot `
                "success-snapshot-output"
            $arguments = New-ValidArguments $promoted $output
            Assert-ThrowsContaining {
                Invoke-PrepareSteamPipeBuild @arguments
            } "STEAMPIPE_PROMOTED_EVIDENCE_INVALID"
            Assert-FinalOutputAbsent $output
        } finally {
            Set-Item Function:\Read-JsonFileSnapshot `
                $script:OriginalJsonSnapshotReader
            Remove-Variable OriginalJsonSnapshotReader `
                -Scope Script -ErrorAction SilentlyContinue
            Remove-Variable CapturedSuccessSnapshotHash `
                -Scope Script -ErrorAction SilentlyContinue
            Remove-Variable MutatedSuccessAfterSnapshot `
                -Scope Script -ErrorAction SilentlyContinue
        }
    }

    Invoke-Case "promoted source identity mismatch is rejected" {
        foreach ($field in @("sourceSha", "sourceTree")) {
            $promoted = New-PromotedFixture `
                (Join-Path $script:FixtureRoot ("source-mismatch-" + $field))
            $successPath = Join-Path $promoted "evidence\SUCCESS.json"
            $success = Get-Content -LiteralPath $successPath -Raw |
                ConvertFrom-Json
            $success.$field = "synthetic-mismatch"
            Write-TestJson $success $successPath

            $output = Join-Path $script:FixtureRoot `
                ("source-mismatch-output-" + $field)
            $arguments = New-ValidArguments $promoted $output
            Assert-ThrowsContaining {
                Invoke-PrepareSteamPipeBuild @arguments
            } "STEAMPIPE_PROMOTED_EVIDENCE_INVALID"
            Assert-FinalOutputAbsent $output
        }
    }

    Invoke-Case "unsupported promoted manifest schema is rejected" {
        $promoted = New-PromotedFixture `
            (Join-Path $script:FixtureRoot "schema-mismatch-promoted")
        $manifestPath = Join-Path $promoted `
            "evidence\distribution-manifest.json"
        $successPath = Join-Path $promoted "evidence\SUCCESS.json"
        $manifest = Get-Content -LiteralPath $manifestPath -Raw |
            ConvertFrom-Json
        $manifest.schemaVersion = "2.0"
        Write-TestJson $manifest $manifestPath
        $success = Get-Content -LiteralPath $successPath -Raw |
            ConvertFrom-Json
        $success.manifestSha256 = Get-FileSha256 $manifestPath
        Write-TestJson $success $successPath

        $output = Join-Path $script:FixtureRoot "schema-mismatch-output"
        $arguments = New-ValidArguments $promoted $output
        Assert-ThrowsContaining {
            Invoke-PrepareSteamPipeBuild @arguments
        } "STEAMPIPE_PROMOTED_EVIDENCE_INVALID"
        Assert-FinalOutputAbsent $output
    }

    Invoke-Case "promoted launch arguments are validated by the typed target policy" {
        $promoted = New-PromotedFixture `
            (Join-Path $script:FixtureRoot "launch-mismatch-promoted")
        $manifestPath = Join-Path $promoted `
            "evidence\distribution-manifest.json"
        $successPath = Join-Path $promoted "evidence\SUCCESS.json"
        $manifest = Get-Content -LiteralPath $manifestPath -Raw |
            ConvertFrom-Json
        $manifest.expectedLaunchArguments = @("-j2mPlatformProvider", "local")
        Write-TestJson $manifest $manifestPath
        $success = Get-Content -LiteralPath $successPath -Raw | ConvertFrom-Json
        $success.manifestSha256 = Get-FileSha256 $manifestPath
        Write-TestJson $success $successPath

        $output = Join-Path $script:FixtureRoot "launch-mismatch-output"
        $arguments = New-ValidArguments $promoted $output
        Assert-ThrowsContaining {
            Invoke-PrepareSteamPipeBuild @arguments
        } "STEAMPIPE_PROMOTED_LAUNCH_ARGUMENT_MISMATCH"
        Assert-FinalOutputAbsent $output
    }

    Invoke-Case "steam_appid and denied content are rejected" {
        foreach ($kind in @("appid", "denied")) {
            $promoted = New-PromotedFixture `
                (Join-Path $script:FixtureRoot ("forbidden-" + $kind)) `
                -IncludeSteamAppId ($kind -ceq "appid") `
                -IncludeDenied ($kind -ceq "denied")
            $output = Join-Path $script:FixtureRoot ("forbidden-output-" + $kind)
            $arguments = New-ValidArguments $promoted $output
            $expected = if ($kind -ceq "appid") {
                "STAGING_FORBIDDEN_ARTIFACT_PRESENT"
            } else {
                "STEAMPIPE_PROMOTED_EVIDENCE_INVALID"
            }
            Assert-ThrowsContaining {
                Invoke-PrepareSteamPipeBuild @arguments
            } $expected
            Assert-FinalOutputAbsent $output
        }
    }

    Invoke-Case "missing Steam native and managed bindings are rejected" {
        foreach ($kind in @("native", "managed")) {
            $promoted = New-PromotedFixture `
                (Join-Path $script:FixtureRoot ("missing-" + $kind)) `
                -IncludeNative ($kind -cne "native") `
                -IncludeManaged ($kind -cne "managed")
            $output = Join-Path $script:FixtureRoot ("missing-output-" + $kind)
            $arguments = New-ValidArguments $promoted $output
            Assert-ThrowsContaining {
                Invoke-PrepareSteamPipeBuild @arguments
            } "STAGING_PROMOTED_ARTIFACT_CONTRACT_FAILED"
            Assert-FinalOutputAbsent $output
        }
    }

    Invoke-Case "matching manifest with incomplete runtime is rejected" {
        $promoted = New-PromotedFixture `
            (Join-Path $script:FixtureRoot "missing-runtime") `
            -IncludeUnityPlayer $false
        $output = Join-Path $script:FixtureRoot "missing-runtime-output"
        $arguments = New-ValidArguments $promoted $output
        Assert-ThrowsContaining {
            Invoke-PrepareSteamPipeBuild @arguments
        } "STAGING_REQUIRED_RUNTIME_MISSING"
        Assert-FinalOutputAbsent $output
    }

    Invoke-Case "matching evidence with unsupported backend is rejected" {
        $promoted = New-PromotedFixture `
            (Join-Path $script:FixtureRoot "unsupported-backend") `
            -ScriptingBackend "Unknown"
        $output = Join-Path $script:FixtureRoot "unsupported-backend-output"
        $arguments = New-ValidArguments $promoted $output
        Assert-ThrowsContaining {
            Invoke-PrepareSteamPipeBuild @arguments
        } "STAGING_BACKEND_UNSUPPORTED"
        Assert-FinalOutputAbsent $output
    }

    Invoke-Case "repository promoted and content output overlap are rejected" {
        $promoted = New-PromotedFixture (Join-Path $script:FixtureRoot "path-promoted")
        $repositoryOutput = Join-Path $script:RepositoryRoot "TestResults\forbidden-steampipe"
        $arguments = New-ValidArguments $promoted $repositoryOutput
        Assert-ThrowsContaining {
            Invoke-PrepareSteamPipeBuild @arguments
        } "STEAMPIPE_REPOSITORY_OUTPUT_REJECTED"
        Assert-FinalOutputAbsent $repositoryOutput

        $repositoryAncestor = [IO.Path]::GetDirectoryName($script:RepositoryRoot)
        $arguments = New-ValidArguments $promoted $repositoryAncestor
        Assert-ThrowsContaining {
            Invoke-PrepareSteamPipeBuild @arguments
        } "STEAMPIPE_REPOSITORY_OUTPUT_REJECTED"

        $overlap = Join-Path $promoted "payload\dry-run-output"
        $arguments = New-ValidArguments $promoted $overlap
        Assert-ThrowsContaining {
            Invoke-PrepareSteamPipeBuild @arguments
        } "STEAMPIPE_CONTENT_OUTPUT_OVERLAP_REJECTED"
        Assert-FinalOutputAbsent $overlap

        $promotedEvidenceOutput = Join-Path $promoted "evidence\dry-run-output"
        $arguments = New-ValidArguments $promoted $promotedEvidenceOutput
        Assert-ThrowsContaining {
            Invoke-PrepareSteamPipeBuild @arguments
        } "STEAMPIPE_PROMOTED_OUTPUT_OVERLAP_REJECTED"
        Assert-FinalOutputAbsent $promotedEvidenceOutput

        $arguments = New-ValidArguments $promoted $script:FixtureRoot
        Assert-ThrowsContaining {
            Invoke-PrepareSteamPipeBuild @arguments
        } "STEAMPIPE_PROMOTED_OUTPUT_OVERLAP_REJECTED"
    }

    Invoke-Case "promoted and output traversal paths are rejected before resolution" {
        $promoted = New-PromotedFixture (Join-Path $script:FixtureRoot "escape-promoted")
        $output = Join-Path $script:FixtureRoot "escape-output"
        $arguments = New-ValidArguments `
            (Join-Path $promoted "payload\..") $output
        Assert-ThrowsContaining {
            Invoke-PrepareSteamPipeBuild @arguments
        } "STEAMPIPE_PATH_ESCAPE_REJECTED"
        Assert-FinalOutputAbsent $output

        $arguments = New-ValidArguments $promoted `
            (Join-Path $script:FixtureRoot "escape\..\escaped-output")
        Assert-ThrowsContaining {
            Invoke-PrepareSteamPipeBuild @arguments
        } "STEAMPIPE_PATH_ESCAPE_REJECTED"
        Assert-FinalOutputAbsent `
            (Join-Path $script:FixtureRoot "escaped-output")
    }

    Invoke-Case "UNC and device roots are rejected before filesystem access" {
        $promoted = New-PromotedFixture (Join-Path $script:FixtureRoot "local-path-promoted")
        $output = Join-Path $script:FixtureRoot "local-path-output"

        $arguments = New-ValidArguments '\\synthetic-host\share\promoted' $output
        Assert-ThrowsContaining {
            Invoke-PrepareSteamPipeBuild @arguments
        } "STEAMPIPE_NETWORK_PATH_REJECTED"
        Assert-FinalOutputAbsent $output

        $arguments = New-ValidArguments $promoted '\\synthetic-host\share\output'
        Assert-ThrowsContaining {
            Invoke-PrepareSteamPipeBuild @arguments
        } "STEAMPIPE_NETWORK_PATH_REJECTED"

        $arguments = New-ValidArguments $promoted $output
        $arguments.RepositoryRoot = '\\?\D:\synthetic-repository'
        Assert-ThrowsContaining {
            Invoke-PrepareSteamPipeBuild @arguments
        } "STEAMPIPE_NETWORK_PATH_REJECTED"
        Assert-FinalOutputAbsent $output
    }

    Invoke-Case "mapped network roots are rejected before filesystem access" {
        $promoted = New-PromotedFixture (Join-Path $script:FixtureRoot "mapped-promoted")
        $output = Join-Path $script:FixtureRoot "mapped-output"
        $script:OriginalSteamPipeDriveType =
            (Get-Command Get-SteamPipeDriveType).ScriptBlock
        try {
            Set-Item Function:\Get-SteamPipeDriveType {
                param([string]$Path)
                if ($Path.StartsWith("Z:", [StringComparison]::OrdinalIgnoreCase)) {
                    return [IO.DriveType]::Network
                }
                return & $script:OriginalSteamPipeDriveType -Path $Path
            }

            $arguments = New-ValidArguments 'Z:\promoted' $output
            Assert-ThrowsContaining {
                Invoke-PrepareSteamPipeBuild @arguments
            } "STEAMPIPE_NETWORK_PATH_REJECTED"
            Assert-FinalOutputAbsent $output

            $arguments = New-ValidArguments $promoted 'Z:\output'
            Assert-ThrowsContaining {
                Invoke-PrepareSteamPipeBuild @arguments
            } "STEAMPIPE_NETWORK_PATH_REJECTED"

            $arguments = New-ValidArguments $promoted $output
            $arguments.RepositoryRoot = 'Z:\repository'
            Assert-ThrowsContaining {
                Invoke-PrepareSteamPipeBuild @arguments
            } "STEAMPIPE_NETWORK_PATH_REJECTED"
            Assert-FinalOutputAbsent $output
        } finally {
            Set-Item Function:\Get-SteamPipeDriveType `
                $script:OriginalSteamPipeDriveType
            Remove-Variable OriginalSteamPipeDriveType `
                -Scope Script -ErrorAction SilentlyContinue
        }
    }

    Invoke-Case "SetLive execution and credential-like parameters are absent" {
        $promoted = New-PromotedFixture (Join-Path $script:FixtureRoot "surface-promoted")
        foreach ($parameter in @("SetLive", "ExecuteSteamCmd", "SteamUsername")) {
            $output = Join-Path $script:FixtureRoot ("surface-output-" + $parameter)
            $arguments = New-ValidArguments $promoted $output
            Assert-ThrowsContaining {
                if ($parameter -ceq "SetLive") {
                    Invoke-PrepareSteamPipeBuild @arguments -SetLive "branch"
                } elseif ($parameter -ceq "ExecuteSteamCmd") {
                    Invoke-PrepareSteamPipeBuild @arguments -ExecuteSteamCmd
                } else {
                    Invoke-PrepareSteamPipeBuild @arguments -SteamUsername "account"
                }
            } $parameter
            Assert-FinalOutputAbsent $output
        }

        $malicious = ConvertFrom-VdfText @'
"AppBuild"
{
    "AppID" "900000001"
    "SetLive" "branch"
}
'@
        Assert-ThrowsContaining { Assert-VdfForbiddenSurfaceZero $malicious } `
            "STEAMPIPE_VDF_FORBIDDEN_KEY"

        $credentialKey = ConvertFrom-VdfText @'
"AppBuild"
{
    "SteamUsername" "account"
}
'@
        Assert-ThrowsContaining {
            Assert-VdfForbiddenSurfaceZero $credentialKey
        } "STEAMPIPE_VDF_CREDENTIAL_SURFACE_REJECTED"

        $commandValue = ConvertFrom-VdfText @'
"AppBuild"
{
    "Desc" "+login synthetic"
}
'@
        Assert-ThrowsContaining {
            Assert-VdfForbiddenSurfaceZero $commandValue
        } "STEAMPIPE_VDF_CREDENTIAL_SURFACE_REJECTED"
    }

    Invoke-Case "credential-like path components remain valid local paths" {
        $promoted = New-PromotedFixture `
            (Join-Path $script:FixtureRoot "email-team\promoted")
        $output = Join-Path $script:FixtureRoot "login-build\output"
        $arguments = New-ValidArguments $promoted $output
        $result = Invoke-PrepareSteamPipeBuild @arguments
        Assert-True (Test-Path -LiteralPath $result.OutputRoot -PathType Container)
        Assert-True (Test-Path -LiteralPath `
            (Join-Path $output "PRE_APPID_DRY_RUN_SUCCESS.json") -PathType Leaf)
    }

    Invoke-Case "failure cleans preparing directory and leaves no final marker" {
        $promoted = New-PromotedFixture (Join-Path $script:FixtureRoot "atomic-fail-promoted")
        $output = Join-Path $script:FixtureRoot "atomic-fail-output"
        $original = (Get-Command Write-DeterministicJson).ScriptBlock
        try {
            Set-Item Function:\Write-DeterministicJson {
                throw "SYNTHETIC_WRITE_FAILURE"
            }
            $arguments = New-ValidArguments $promoted $output
            Assert-ThrowsContaining {
                Invoke-PrepareSteamPipeBuild @arguments
            } "SYNTHETIC_WRITE_FAILURE"
        } finally {
            Set-Item Function:\Write-DeterministicJson $original
        }
        Assert-FinalOutputAbsent $output
        $preparing = @(Get-ChildItem -LiteralPath $script:FixtureRoot -Directory |
            Where-Object { $_.Name.StartsWith(".preparing-atomic-fail-output-") })
        Assert-Equal 0 $preparing.Count
    }

    Invoke-Case "payload mutation before promotion fails closed and cleans output" {
        $promoted = New-PromotedFixture `
            (Join-Path $script:FixtureRoot "mutation-promoted")
        $output = Join-Path $script:FixtureRoot "mutation-output"
        $script:MutationPayloadPath = Join-Path $promoted "payload\VectorQuake.exe"
        $script:MutationWriteOriginal = (Get-Command Write-DeterministicJson).ScriptBlock
        $script:MutationApplied = $false
        try {
            Set-Item Function:\Write-DeterministicJson {
                param($Value, [string]$Path)
                & $script:MutationWriteOriginal -Value $Value -Path $Path
                if (-not $script:MutationApplied -and
                    [IO.Path]::GetFileName($Path) -ceq
                        "PRE_APPID_DRY_RUN_REPORT.json") {
                    $script:MutationApplied = $true
                    Write-Utf8File $script:MutationPayloadPath "mutated-before-promotion"
                }
            }
            $arguments = New-ValidArguments $promoted $output
            Assert-ThrowsContaining {
                Invoke-PrepareSteamPipeBuild @arguments
            } "STAGING_PROMOTED_MANIFEST_MISMATCH"
        } finally {
            Set-Item Function:\Write-DeterministicJson $script:MutationWriteOriginal
            Remove-Variable MutationPayloadPath -Scope Script -ErrorAction SilentlyContinue
            Remove-Variable MutationWriteOriginal -Scope Script -ErrorAction SilentlyContinue
            Remove-Variable MutationApplied -Scope Script -ErrorAction SilentlyContinue
        }
        Assert-FinalOutputAbsent $output
        $preparing = @(Get-ChildItem -LiteralPath $script:FixtureRoot -Directory |
            Where-Object { $_.Name.StartsWith(".preparing-mutation-output-") })
        Assert-Equal 0 $preparing.Count
    }

    Invoke-Case "success atomically promotes only final output" {
        $promoted = New-PromotedFixture (Join-Path $script:FixtureRoot "atomic-promoted")
        $output = Join-Path $script:FixtureRoot "atomic-output"
        $arguments = New-ValidArguments $promoted $output
        $result = Invoke-PrepareSteamPipeBuild @arguments
        Assert-True (Test-Path -LiteralPath $result.OutputRoot -PathType Container)
        Assert-True (Test-Path -LiteralPath `
            (Join-Path $output "PRE_APPID_DRY_RUN_SUCCESS.json") -PathType Leaf)
        $preparing = @(Get-ChildItem -LiteralPath $script:FixtureRoot -Directory |
            Where-Object { $_.Name.StartsWith(".preparing-atomic-output-") })
        Assert-Equal 0 $preparing.Count
    }

    Invoke-Case "production source has no process network or identity hardcode path" {
        $source = Get-Content -LiteralPath `
            (Join-Path $PSScriptRoot "..\Prepare-SteamPipeBuild.ps1") -Raw
        foreach ($token in @(
            "Start-Process steamcmd", "& steamcmd", "+login", "+run_app_build",
            "Invoke-WebRequest", "Invoke-RestMethod", "System.Net.WebClient")) {
            Assert-True (-not $source.Contains($token)) "Forbidden token: $token"
        }
        Assert-True (-not $source.Contains('AppId = "480"'))
        Assert-True ($source -notmatch '\[string\]\$DepotId\s*=\s*"[1-9]')
    }

    if ($script:Failed -eq 0 -and
        -not [string]::IsNullOrWhiteSpace($PreserveSyntheticEvidenceRoot)) {
        if (Test-Path -LiteralPath $PreserveSyntheticEvidenceRoot) {
            throw "Synthetic evidence root must be fresh: $PreserveSyntheticEvidenceRoot"
        }
        $promoted = New-PromotedFixture `
            (Join-Path $PreserveSyntheticEvidenceRoot "synthetic-promoted-fixture")
        $output = Join-Path $PreserveSyntheticEvidenceRoot "dry-run"
        $arguments = New-ValidArguments $promoted $output
        $result = Invoke-PrepareSteamPipeBuild @arguments
        Write-Host "SYNTHETIC_EVIDENCE_ROOT=$PreserveSyntheticEvidenceRoot"
        Write-Host "SYNTHETIC_VDF_AGGREGATE_SHA256=$($result.VdfAggregateSha256)"
    }
} finally {
    if (Test-Path -LiteralPath $script:FixtureRoot -PathType Container) {
        Remove-Item -LiteralPath $script:FixtureRoot -Recurse -Force
    }
}

$script:Results | ForEach-Object { Write-Host $_ }
Write-Host "TOTAL=$($script:Passed + $script:Failed) PASS=$script:Passed FAIL=$script:Failed"
if ($script:Failed -ne 0) { exit 1 }
exit 0
