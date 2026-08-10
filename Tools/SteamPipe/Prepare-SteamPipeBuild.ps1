[CmdletBinding()]
param(
    [string]$AppId = "",
    [string]$DepotId = "",
    [string]$PromotedSteamWindowsRoot = "",
    [string]$OutputRoot = "",
    [ValidateSet("Synthetic", "Actual")][string]$IdentityMode = "Synthetic",
    [string]$RepositoryRoot = "",
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

function Get-SteamPipeDriveType {
    param([Parameter(Mandatory)][string]$Path)

    $pathRoot = [IO.Path]::GetPathRoot([IO.Path]::GetFullPath($Path))
    try {
        return ([IO.DriveInfo]::new($pathRoot)).DriveType
    } catch {
        throw "STEAMPIPE_DRIVE_TYPE_UNAVAILABLE: $Path"
    }
}

function Assert-AbsolutePathWithoutTraversal {
    param(
        [Parameter(Mandatory)][AllowEmptyString()][string]$Path,
        [Parameter(Mandatory)][string]$Name
    )

    if ([string]::IsNullOrWhiteSpace($Path)) {
        throw "STEAMPIPE_PATH_ESCAPE_REJECTED: $Name must be an absolute path without traversal."
    }

    $normalized = $Path.Replace('\', '/')
    if ($normalized.StartsWith('//', [StringComparison]::Ordinal)) {
        throw "STEAMPIPE_NETWORK_PATH_REJECTED: $Name must be a local filesystem path."
    }

    if (-not [IO.Path]::IsPathRooted($Path) -or
        $normalized.Split('/') -contains '..') {
        throw "STEAMPIPE_PATH_ESCAPE_REJECTED: $Name must be an absolute path without traversal."
    }

    $driveType = Get-SteamPipeDriveType -Path $Path
    if ($driveType -notin @(
            [IO.DriveType]::Fixed,
            [IO.DriveType]::Removable,
            [IO.DriveType]::Ram)) {
        throw "STEAMPIPE_NETWORK_PATH_REJECTED: $Name must use a verified local drive."
    }
}

function Test-PathIsSameOrUnder {
    param(
        [Parameter(Mandatory)][string]$Candidate,
        [Parameter(Mandatory)][string]$Parent
    )

    $candidateFull = [IO.Path]::GetFullPath($Candidate).TrimEnd('\', '/') + '\'
    $parentFull = [IO.Path]::GetFullPath($Parent).TrimEnd('\', '/') + '\'
    return $candidateFull.StartsWith(
        $parentFull,
        [StringComparison]::OrdinalIgnoreCase)
}

function ConvertTo-SteamIdentityId {
    param(
        [Parameter(Mandatory)][AllowEmptyString()][string]$Value,
        [Parameter(Mandatory)][string]$Name
    )

    if ([string]::IsNullOrWhiteSpace($Value)) {
        throw "STEAMPIPE_ID_INVALID: $Name is required."
    }
    foreach ($character in $Value.ToCharArray()) {
        if ($character -lt '0' -or $character -gt '9') {
            throw "STEAMPIPE_ID_INVALID: $Name must be an unsigned decimal integer."
        }
    }

    $parsed = [uint32]0
    if (-not [uint32]::TryParse(
            $Value,
            [Globalization.NumberStyles]::None,
            [Globalization.CultureInfo]::InvariantCulture,
            [ref]$parsed) -or $parsed -eq 0) {
        throw "STEAMPIPE_ID_INVALID: $Name must be in the uint32 positive range."
    }
    return $parsed
}

function Assert-NoReparseAncestors {
    param([Parameter(Mandatory)][string]$Path)

    $ancestors = @()
    $current = [IO.Path]::GetFullPath($Path)
    while (-not [string]::IsNullOrWhiteSpace($current)) {
        $ancestors += $current
        $parent = [IO.Path]::GetDirectoryName($current.TrimEnd('\', '/'))
        if ([string]::IsNullOrWhiteSpace($parent) -or
            $parent -ceq $current) {
            break
        }
        $current = $parent
    }
    [array]::Reverse($ancestors)
    foreach ($ancestor in $ancestors) {
        if (-not (Test-Path -LiteralPath $ancestor)) {
            break
        }
        $item = Get-Item -LiteralPath $ancestor -Force
        if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "STEAMPIPE_REPARSE_POINT_REJECTED: $ancestor"
        }
    }
}

function Import-WindowsDistributionValidationTypes {
    param([Parameter(Mandatory)][string]$Root)

    $wrapper = Join-Path $Root "Tools\Build\Stage-WindowsDistribution.ps1"
    Assert-NoReparseAncestors -Path $wrapper
    if (-not (Test-Path -LiteralPath $wrapper -PathType Leaf)) {
        throw "STEAMPIPE_PROMOTED_VALIDATOR_MISSING: $wrapper"
    }

    $previousMode = $env:VECTORQUAKE_DISTRIBUTION_STAGER_TEST_MODE
    try {
        $env:VECTORQUAKE_DISTRIBUTION_STAGER_TEST_MODE = "1"
        . $wrapper
        Import-WindowsDistributionStagerTypes -Root $Root -OfflineOnly
    } finally {
        $env:VECTORQUAKE_DISTRIBUTION_STAGER_TEST_MODE = $previousMode
    }
}

function Get-FileSha256 {
    param([Parameter(Mandatory)][string]$Path)
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function ConvertTo-ManifestFileArray {
    param([Parameter(Mandatory)]$Files)

    $typed = @()
    foreach ($file in @($Files)) {
        $entry = New-Object WindowsDistributionManifestFile
        $entry.RelativePath = [string]$file.relativePath
        $entry.Size = [long]$file.size
        $entry.Sha256 = [string]$file.sha256
        $typed += $entry
    }
    return [WindowsDistributionManifestFile[]]$typed
}

function Test-JsonBooleanTrue {
    param($Value)
    return $Value -is [bool] -and $Value
}

function Invoke-PromotedSteamWindowsPreflight {
    param(
        [Parameter(Mandatory)][string]$PromotedRoot,
        [Parameter(Mandatory)][string]$RepositoryRoot
    )

    Import-WindowsDistributionValidationTypes -Root $RepositoryRoot
    $promotedFull = (Resolve-Path -LiteralPath $PromotedRoot).Path
    $payloadRoot = Join-Path $promotedFull "payload"
    $evidenceRoot = Join-Path $promotedFull "evidence"
    $manifestPath = Join-Path $evidenceRoot "distribution-manifest.json"
    $successPath = Join-Path $evidenceRoot "SUCCESS.json"
    Assert-NoReparseAncestors -Path $payloadRoot
    Assert-NoReparseAncestors -Path $manifestPath
    Assert-NoReparseAncestors -Path $successPath
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf) -or
        -not (Test-Path -LiteralPath $successPath -PathType Leaf)) {
        throw "STEAMPIPE_PROMOTED_EVIDENCE_MISSING"
    }

    try {
        $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
        $success = Get-Content -LiteralPath $successPath -Raw | ConvertFrom-Json
    } catch {
        throw "STEAMPIPE_PROMOTED_EVIDENCE_INVALID: $($_.Exception.Message)"
    }

    if ([string]$manifest.distributionTargetId -cne "steam-windows" -or
        [string]$success.distributionTarget -cne "steam-windows") {
        throw "STEAMPIPE_PROMOTED_TARGET_REJECTED"
    }
    $launchArguments = [string[]]@($manifest.expectedLaunchArguments |
        ForEach-Object { [string]$_ })
    $launchFailure = [WindowsDistributionTargetPolicy]::ValidateLaunchArguments(
        [WindowsDistributionTargetPolicy]::SteamWindows,
        $launchArguments)
    if ($launchFailure -ne [WindowsDistributionValidationFailure]::None) {
        throw "STEAMPIPE_PROMOTED_LAUNCH_ARGUMENT_MISMATCH: $launchFailure"
    }
    if ([string]$manifest.scriptingBackend -cne [string]$success.scriptingBackend -or
        [string]$manifest.expectedProviderId -cne "steam" -or
        [string]$success.status -cne "SUCCESS" -or
        [int]$manifest.deniedArtifactCount -ne 0 -or
        [int]$success.deniedArtifactCount -ne 0 -or
        -not (Test-JsonBooleanTrue $success.promotedArtifactContractPassed) -or
        -not (Test-JsonBooleanTrue $success.copyByteIdentity) -or
        -not (Test-JsonBooleanTrue $success.sourceBuildImmutable)) {
        throw "STEAMPIPE_PROMOTED_EVIDENCE_INVALID"
    }

    $manifestSha = Get-FileSha256 -Path $manifestPath
    if ([string]$success.manifestSha256 -cne $manifestSha) {
        throw "STEAMPIPE_PROMOTED_MANIFEST_HASH_MISMATCH"
    }

    $request = New-Object WindowsDistributionPromotedValidationRequest
    $request.PromotedRoot = $promotedFull
    $request.DistributionTargetId = "steam-windows"
    $request.ScriptingBackend = [string]$manifest.scriptingBackend
    $request.ManifestFiles = ConvertTo-ManifestFileArray -Files $manifest.files
    $request.ManifestDeniedArtifactCount = [int]$manifest.deniedArtifactCount
    $request.ManifestFileCount = [int]$manifest.fileCount
    $request.ManifestTotalBytes = [long]$manifest.totalBytes
    $validation = [WindowsDistributionStager]::ValidatePromotedArtifact($request)
    if ([string]$validation.ManifestSha256 -cne $manifestSha) {
        throw "STEAMPIPE_PROMOTED_MANIFEST_CHANGED"
    }

    if ([int]$success.fileCount -ne $validation.FileCount -or
        [long]$success.totalBytes -ne $validation.TotalBytes) {
        throw "STEAMPIPE_PROMOTED_SUCCESS_MISMATCH"
    }

    return [pscustomobject][ordered]@{
        PromotedRoot = $validation.PromotedRoot
        PayloadRoot = $validation.PayloadRoot
        EvidenceRoot = $validation.EvidenceRoot
        ManifestPath = $validation.ManifestPath
        SuccessPath = $validation.SuccessPath
        ManifestSha256 = $manifestSha
        SuccessSha256 = Get-FileSha256 -Path $successPath
        DistributionTargetId = $validation.DistributionTargetId
        FileCount = $validation.FileCount
        TotalBytes = $validation.TotalBytes
        DeniedArtifactCount = $validation.DeniedArtifactCount
        SteamNativeCount = $validation.SteamNativeCount
        SteamManagedCount = $validation.SteamManagedCount
        SteamAppIdCount = $validation.SteamAppIdCount
    }
}

function New-VdfPair {
    param([Parameter(Mandatory)][string]$Key, [Parameter(Mandatory)][string]$Value)
    return [pscustomobject][ordered]@{ Kind = "Pair"; Key = $Key; Value = $Value }
}

function New-VdfSection {
    param([Parameter(Mandatory)][string]$Key, [Parameter(Mandatory)][object[]]$Entries)
    return [pscustomobject][ordered]@{ Kind = "Section"; Key = $Key; Entries = $Entries }
}

function ConvertTo-VdfQuoted {
    param([Parameter(Mandatory)][AllowEmptyString()][string]$Value)

    $builder = [Text.StringBuilder]::new()
    foreach ($character in $Value.ToCharArray()) {
        switch ($character) {
            '"' { [void]$builder.Append('\"'); break }
            '\' { [void]$builder.Append('\\'); break }
            "`r" { [void]$builder.Append('\r'); break }
            "`n" { [void]$builder.Append('\n'); break }
            "`t" { [void]$builder.Append('\t'); break }
            default {
                if ([int]$character -lt 0x20) {
                    throw "STEAMPIPE_VDF_CONTROL_CHARACTER_REJECTED"
                }
                [void]$builder.Append($character)
            }
        }
    }
    return '"' + $builder.ToString() + '"'
}

function Add-VdfNodeText {
    param(
        [Parameter(Mandatory)]$Node,
        [Parameter(Mandatory)][Text.StringBuilder]$Builder,
        [int]$Depth = 0
    )

    $indent = "    " * $Depth
    if ($Node.Kind -ceq "Pair") {
        [void]$Builder.Append($indent)
        [void]$Builder.Append((ConvertTo-VdfQuoted $Node.Key))
        [void]$Builder.Append("    ")
        [void]$Builder.Append((ConvertTo-VdfQuoted $Node.Value))
        [void]$Builder.Append("`n")
        return
    }

    [void]$Builder.Append($indent)
    [void]$Builder.Append((ConvertTo-VdfQuoted $Node.Key))
    [void]$Builder.Append("`n")
    [void]$Builder.Append($indent)
    [void]$Builder.Append("{`n")
    foreach ($entry in @($Node.Entries)) {
        Add-VdfNodeText -Node $entry -Builder $Builder -Depth ($Depth + 1)
    }
    [void]$Builder.Append($indent)
    [void]$Builder.Append("}`n")
}

function ConvertTo-VdfText {
    param([Parameter(Mandatory)]$Root)
    $builder = [Text.StringBuilder]::new()
    Add-VdfNodeText -Node $Root -Builder $builder
    return $builder.ToString()
}

function Read-VdfTokens {
    param([Parameter(Mandatory)][string]$Text)

    $tokens = [Collections.Generic.List[object]]::new()
    $index = 0
    while ($index -lt $Text.Length) {
        $character = $Text[$index]
        if ([char]::IsWhiteSpace($character)) { $index++; continue }
        if ($character -eq '{' -or $character -eq '}') {
            $tokens.Add([pscustomobject]@{
                Kind = if ($character -eq '{') { "Open" } else { "Close" }
                Value = [string]$character
            })
            $index++
            continue
        }
        if ($character -ne '"') {
            throw "STEAMPIPE_VDF_SYNTAX_INVALID: expected quoted token."
        }

        $index++
        $builder = [Text.StringBuilder]::new()
        $closed = $false
        while ($index -lt $Text.Length) {
            $character = $Text[$index++]
            if ($character -eq '"') { $closed = $true; break }
            if ($character -eq '\') {
                if ($index -ge $Text.Length) {
                    throw "STEAMPIPE_VDF_SYNTAX_INVALID: incomplete escape."
                }
                $escaped = $Text[$index++]
                switch ($escaped) {
                    '"' { [void]$builder.Append('"') }
                    '\' { [void]$builder.Append('\') }
                    'r' { [void]$builder.Append("`r") }
                    'n' { [void]$builder.Append("`n") }
                    't' { [void]$builder.Append("`t") }
                    default { throw "STEAMPIPE_VDF_SYNTAX_INVALID: unsupported escape." }
                }
            } else {
                [void]$builder.Append($character)
            }
        }
        if (-not $closed) {
            throw "STEAMPIPE_VDF_SYNTAX_INVALID: unterminated quote."
        }
        $tokens.Add([pscustomobject]@{ Kind = "String"; Value = $builder.ToString() })
    }
    return $tokens.ToArray()
}

function Read-VdfSectionEntries {
    param([Parameter(Mandatory)][object[]]$Tokens, [Parameter(Mandatory)][ref]$Index)

    $entries = @()
    while ($Index.Value -lt $Tokens.Count) {
        if ($Tokens[$Index.Value].Kind -ceq "Close") {
            $Index.Value++
            return [pscustomobject]@{ Entries = $entries }
        }
        if ($Tokens[$Index.Value].Kind -cne "String") {
            throw "STEAMPIPE_VDF_SYNTAX_INVALID: expected key."
        }
        $key = [string]$Tokens[$Index.Value++].Value
        if ($Index.Value -ge $Tokens.Count) {
            throw "STEAMPIPE_VDF_SYNTAX_INVALID: missing value."
        }
        $next = $Tokens[$Index.Value++]
        if ($next.Kind -ceq "String") {
            $entries += New-VdfPair -Key $key -Value ([string]$next.Value)
        } elseif ($next.Kind -ceq "Open") {
            $child = Read-VdfSectionEntries -Tokens $Tokens -Index $Index
            $entries += New-VdfSection -Key $key -Entries @($child.Entries)
        } else {
            throw "STEAMPIPE_VDF_SYNTAX_INVALID: unexpected closing brace."
        }
    }
    throw "STEAMPIPE_VDF_SYNTAX_INVALID: unbalanced braces."
}

function ConvertFrom-VdfText {
    param([Parameter(Mandatory)][string]$Text)

    $tokens = @(Read-VdfTokens -Text $Text)
    if ($tokens.Count -lt 3 -or $tokens[0].Kind -cne "String" -or
        $tokens[1].Kind -cne "Open") {
        throw "STEAMPIPE_VDF_SYNTAX_INVALID: missing root section."
    }
    $index = 2
    $section = Read-VdfSectionEntries -Tokens $tokens -Index ([ref]$index)
    if ($index -ne $tokens.Count) {
        throw "STEAMPIPE_VDF_SYNTAX_INVALID: trailing tokens."
    }
    return New-VdfSection -Key ([string]$tokens[0].Value) -Entries @($section.Entries)
}

function Get-VdfEntries {
    param([Parameter(Mandatory)]$Section, [Parameter(Mandatory)][string]$Key)
    return @($Section.Entries | Where-Object { $_.Key -ceq $Key })
}

function Get-RequiredVdfPairValue {
    param([Parameter(Mandatory)]$Section, [Parameter(Mandatory)][string]$Key)
    $entries = @(Get-VdfEntries -Section $Section -Key $Key)
    if ($entries.Count -ne 1 -or $entries[0].Kind -cne "Pair") {
        throw "STEAMPIPE_VDF_SCHEMA_INVALID: required pair $Key must occur exactly once."
    }
    return [string]$entries[0].Value
}

function Assert-VdfForbiddenSurfaceZero {
    param([Parameter(Mandatory)]$Section)

    $forbiddenKeys = @("SetLive", "Local")
    $forbiddenFragments = @(
        ("+" + "login"), ("+" + "run_app_build"),
        "steamguard", "password", "credential",
        "ssfn", "config.vdf", "steamid", "email")
    foreach ($entry in @($Section.Entries)) {
        if ($forbiddenKeys -contains $entry.Key) {
            throw "STEAMPIPE_VDF_FORBIDDEN_KEY: $($entry.Key)"
        }
        $values = @([string]$entry.Key)
        if ($entry.Kind -ceq "Pair") { $values += [string]$entry.Value }
        foreach ($value in $values) {
            foreach ($fragment in $forbiddenFragments) {
                if ($value.IndexOf($fragment, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
                    throw "STEAMPIPE_VDF_CREDENTIAL_SURFACE_REJECTED"
                }
            }
        }
        if ($entry.Kind -ceq "Section") {
            Assert-VdfForbiddenSurfaceZero -Section $entry
        }
    }
}

function Assert-AppVdfSchema {
    param(
        [Parameter(Mandatory)]$Root,
        [Parameter(Mandatory)][string]$ExpectedAppId,
        [Parameter(Mandatory)][string]$ExpectedDepotId,
        [Parameter(Mandatory)][string]$ExpectedDepotFile,
        [Parameter(Mandatory)][string]$ExpectedContentRoot,
        [Parameter(Mandatory)][string]$ExpectedBuildOutput
    )

    if ($Root.Key -cne "AppBuild") { throw "STEAMPIPE_APP_VDF_ROOT_INVALID" }
    Assert-VdfForbiddenSurfaceZero -Section $Root
    if ((Get-RequiredVdfPairValue $Root "AppID") -cne $ExpectedAppId -or
        (Get-RequiredVdfPairValue $Root "Preview") -cne "1" -or
        (Get-RequiredVdfPairValue $Root "ContentRoot") -cne $ExpectedContentRoot -or
        (Get-RequiredVdfPairValue $Root "BuildOutput") -cne $ExpectedBuildOutput) {
        throw "STEAMPIPE_APP_VDF_SCHEMA_INVALID"
    }
    [void](Get-RequiredVdfPairValue $Root "Desc")
    $depots = @(Get-VdfEntries -Section $Root -Key "Depots")
    if ($depots.Count -ne 1 -or $depots[0].Kind -cne "Section") {
        throw "STEAMPIPE_APP_VDF_DEPOTS_INVALID"
    }
    if ((Get-RequiredVdfPairValue $depots[0] $ExpectedDepotId) -cne
        $ExpectedDepotFile) {
        throw "STEAMPIPE_APP_VDF_DEPOT_REFERENCE_MISMATCH"
    }
}

function Assert-DepotVdfSchema {
    param([Parameter(Mandatory)]$Root, [Parameter(Mandatory)][string]$ExpectedDepotId)

    if ($Root.Key -cne "DepotBuild") { throw "STEAMPIPE_DEPOT_VDF_ROOT_INVALID" }
    Assert-VdfForbiddenSurfaceZero -Section $Root
    if ((Get-RequiredVdfPairValue $Root "DepotID") -cne $ExpectedDepotId -or
        (Get-RequiredVdfPairValue $Root "FileExclusion") -cne "steam_appid.txt") {
        throw "STEAMPIPE_DEPOT_VDF_SCHEMA_INVALID"
    }
    $mapping = @(Get-VdfEntries -Section $Root -Key "FileMapping")
    if ($mapping.Count -ne 1 -or $mapping[0].Kind -cne "Section" -or
        (Get-RequiredVdfPairValue $mapping[0] "LocalPath") -cne "*" -or
        (Get-RequiredVdfPairValue $mapping[0] "DepotPath") -cne "." -or
        (Get-RequiredVdfPairValue $mapping[0] "Recursive") -cne "1") {
        throw "STEAMPIPE_DEPOT_VDF_MAPPING_INVALID"
    }
}

function Write-DeterministicJson {
    param([Parameter(Mandatory)]$Value, [Parameter(Mandatory)][string]$Path)
    $json = ($Value | ConvertTo-Json -Depth 12).Replace("`r`n", "`n") + "`n"
    [IO.File]::WriteAllText($Path, $json, [Text.UTF8Encoding]::new($false))
}

function Get-AggregateSha256 {
    param([Parameter(Mandatory)][byte[]]$First, [Parameter(Mandatory)][byte[]]$Second)
    $combined = New-Object byte[] ($First.Length + $Second.Length)
    [Array]::Copy($First, 0, $combined, 0, $First.Length)
    [Array]::Copy($Second, 0, $combined, $First.Length, $Second.Length)
    $algorithm = [Security.Cryptography.SHA256]::Create()
    try {
        return -join ($algorithm.ComputeHash($combined) |
            ForEach-Object { $_.ToString("x2") })
    } finally {
        $algorithm.Dispose()
    }
}

function Invoke-PrepareSteamPipeBuild {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][AllowEmptyString()][string]$AppId,
        [Parameter(Mandatory)][AllowEmptyString()][string]$DepotId,
        [Parameter(Mandatory)][string]$PromotedSteamWindowsRoot,
        [Parameter(Mandatory)][string]$OutputRoot,
        [ValidateSet("Synthetic", "Actual")][string]$IdentityMode = "Synthetic",
        [Parameter(Mandatory)][string]$RepositoryRoot,
        [switch]$DryRun
    )

    if (-not $DryRun) { throw "STEAMPIPE_DRY_RUN_REQUIRED" }
    Assert-AbsolutePathWithoutTraversal -Path $PromotedSteamWindowsRoot `
        -Name "PromotedSteamWindowsRoot"
    Assert-AbsolutePathWithoutTraversal -Path $OutputRoot -Name "OutputRoot"
    Assert-AbsolutePathWithoutTraversal -Path $RepositoryRoot -Name "RepositoryRoot"
    Assert-NoReparseAncestors -Path $PromotedSteamWindowsRoot
    Assert-NoReparseAncestors -Path $OutputRoot
    Assert-NoReparseAncestors -Path $RepositoryRoot
    $appIdValue = ConvertTo-SteamIdentityId -Value $AppId -Name "AppId"
    $depotIdValue = ConvertTo-SteamIdentityId -Value $DepotId -Name "DepotId"
    $isActualIdentity = [string]::Equals(
        $IdentityMode,
        "Actual",
        [StringComparison]::OrdinalIgnoreCase)
    $identityModeValue = if ($isActualIdentity) { "Actual" } else { "Synthetic" }
    if ($appIdValue -eq $depotIdValue) { throw "STEAMPIPE_ID_COLLISION" }
    if ($isActualIdentity -and $appIdValue -eq 480) {
        throw "STEAMPIPE_SPACEWAR_APPID_REJECTED"
    }

    $repositoryFull = (Resolve-Path -LiteralPath $RepositoryRoot).Path
    $outputFull = [IO.Path]::GetFullPath($OutputRoot).TrimEnd('\', '/')
    $promotedInputFull = [IO.Path]::GetFullPath(
        $PromotedSteamWindowsRoot).TrimEnd('\', '/')
    if ((Test-PathIsSameOrUnder -Candidate $outputFull -Parent $repositoryFull) -or
        (Test-PathIsSameOrUnder -Candidate $repositoryFull -Parent $outputFull)) {
        throw "STEAMPIPE_REPOSITORY_OUTPUT_REJECTED"
    }
    if (Test-PathIsSameOrUnder -Candidate $promotedInputFull -Parent $outputFull) {
        throw "STEAMPIPE_PROMOTED_OUTPUT_OVERLAP_REJECTED"
    }
    if (Test-Path -LiteralPath $outputFull) {
        throw "STEAMPIPE_OUTPUT_COLLISION"
    }

    $preflight = Invoke-PromotedSteamWindowsPreflight `
        -PromotedRoot $PromotedSteamWindowsRoot `
        -RepositoryRoot $repositoryFull
    $contentRoot = [IO.Path]::GetFullPath($preflight.PayloadRoot).TrimEnd('\', '/')
    $buildOutput = Join-Path $outputFull "build-output"
    if ((Test-PathIsSameOrUnder -Candidate $buildOutput -Parent $contentRoot) -or
        (Test-PathIsSameOrUnder -Candidate $contentRoot -Parent $buildOutput) -or
        (Test-PathIsSameOrUnder -Candidate $outputFull -Parent $contentRoot)) {
        throw "STEAMPIPE_CONTENT_OUTPUT_OVERLAP_REJECTED"
    }
    if (Test-PathIsSameOrUnder -Candidate $outputFull -Parent $promotedInputFull) {
        throw "STEAMPIPE_PROMOTED_OUTPUT_OVERLAP_REJECTED"
    }
    if ([IO.Path]::GetFileName($contentRoot) -cne "payload" -or
        [IO.Path]::GetDirectoryName($contentRoot) -cne $preflight.PromotedRoot) {
        throw "STEAMPIPE_CONTENT_ROOT_INVALID"
    }

    $appIdText = $appIdValue.ToString([Globalization.CultureInfo]::InvariantCulture)
    $depotIdText = $depotIdValue.ToString([Globalization.CultureInfo]::InvariantCulture)
    $appFileName = "app_build_$appIdText.vdf"
    $depotFileName = "depot_build_$depotIdText.vdf"
    $description = if (-not $isActualIdentity) {
        "VectorQuake SYNTHETIC_VALIDATION_ONLY local dry-run"
    } else {
        "VectorQuake actual-identity local dry-run"
    }

    $appNode = New-VdfSection "AppBuild" @(
        (New-VdfPair "AppID" $appIdText),
        (New-VdfPair "Desc" $description),
        (New-VdfPair "Preview" "1"),
        (New-VdfPair "ContentRoot" $contentRoot),
        (New-VdfPair "BuildOutput" $buildOutput),
        (New-VdfSection "Depots" @(
            (New-VdfPair $depotIdText $depotFileName)
        ))
    )
    $depotNode = New-VdfSection "DepotBuild" @(
        (New-VdfPair "DepotID" $depotIdText),
        (New-VdfSection "FileMapping" @(
            (New-VdfPair "LocalPath" "*"),
            (New-VdfPair "DepotPath" "."),
            (New-VdfPair "Recursive" "1")
        )),
        (New-VdfPair "FileExclusion" "steam_appid.txt")
    )
    $appText = ConvertTo-VdfText -Root $appNode
    $depotText = ConvertTo-VdfText -Root $depotNode
    Assert-AppVdfSchema `
        -Root (ConvertFrom-VdfText $appText) `
        -ExpectedAppId $appIdText `
        -ExpectedDepotId $depotIdText `
        -ExpectedDepotFile $depotFileName `
        -ExpectedContentRoot $contentRoot `
        -ExpectedBuildOutput $buildOutput
    Assert-DepotVdfSchema `
        -Root (ConvertFrom-VdfText $depotText) `
        -ExpectedDepotId $depotIdText

    $parent = [IO.Path]::GetDirectoryName($outputFull)
    if ([string]::IsNullOrWhiteSpace($parent)) { throw "STEAMPIPE_OUTPUT_PARENT_INVALID" }
    [IO.Directory]::CreateDirectory($parent) | Out-Null
    $temporary = Join-Path $parent (
        ".preparing-" + [IO.Path]::GetFileName($outputFull) + "-" +
        [Guid]::NewGuid().ToString("N"))
    try {
        [IO.Directory]::CreateDirectory($temporary) | Out-Null
        [IO.Directory]::CreateDirectory((Join-Path $temporary "build-output")) | Out-Null
        $appPath = Join-Path $temporary $appFileName
        $depotPath = Join-Path $temporary $depotFileName
        $encoding = [Text.UTF8Encoding]::new($false)
        [IO.File]::WriteAllText($appPath, $appText, $encoding)
        [IO.File]::WriteAllText($depotPath, $depotText, $encoding)
        $appBytes = [IO.File]::ReadAllBytes($appPath)
        $depotBytes = [IO.File]::ReadAllBytes($depotPath)
        $aggregate = Get-AggregateSha256 -First $appBytes -Second $depotBytes
        $classification = if (-not $isActualIdentity) {
            "SYNTHETIC_VALIDATION_ONLY"
        } else {
            "ACTUAL_IDENTITY_LOCAL_DRY_RUN"
        }
        $actualIdentityConfigured = $isActualIdentity
        $report = [ordered]@{
            schemaVersion = 1
            classification = $classification
            identityMode = $identityModeValue
            contentRoot = $contentRoot
            buildOutput = $buildOutput
            distributionTarget = $preflight.DistributionTargetId
            manifestSha256 = $preflight.ManifestSha256
            fileCount = $preflight.FileCount
            totalBytes = $preflight.TotalBytes
            steamNativeCount = $preflight.SteamNativeCount
            steamManagedCount = $preflight.SteamManagedCount
            steamAppIdCount = $preflight.SteamAppIdCount
            appVdf = $appFileName
            depotVdf = $depotFileName
            vdfAggregateSha256 = $aggregate
        }
        Write-DeterministicJson $report (Join-Path $temporary "PRE_APPID_DRY_RUN_REPORT.json")
        $success = [ordered]@{
            schemaVersion = 1
            classification = $classification
            uploadAuthority = "NOT_UPLOADABLE"
            identityAuthority = if ($actualIdentityConfigured) {
                "ACTUAL_IDENTITY_INPUT_ONLY"
            } else {
                "NOT_ACTUAL_STEAM_IDENTITY"
            }
            backendContact = "NO_STEAM_BACKEND_CONTACT"
            adminConfiguration = "NO_APP_ADMIN_CONFIGURATION"
            networkOperation = $false
            steamCmdExecuted = $false
            actualIdentityConfigured = $actualIdentityConfigured
            appVdf = $appFileName
            depotVdf = $depotFileName
            contentManifestSha256 = $preflight.ManifestSha256
            vdfAggregateSha256 = $aggregate
        }
        Write-DeterministicJson $success `
            (Join-Path $temporary "PRE_APPID_DRY_RUN_SUCCESS.json")
        $finalPreflight = Invoke-PromotedSteamWindowsPreflight `
            -PromotedRoot $preflight.PromotedRoot `
            -RepositoryRoot $repositoryFull
        if ($finalPreflight.ManifestSha256 -cne $preflight.ManifestSha256 -or
            $finalPreflight.SuccessSha256 -cne $preflight.SuccessSha256 -or
            $finalPreflight.FileCount -ne $preflight.FileCount -or
            $finalPreflight.TotalBytes -ne $preflight.TotalBytes -or
            $finalPreflight.SteamNativeCount -ne $preflight.SteamNativeCount -or
            $finalPreflight.SteamManagedCount -ne $preflight.SteamManagedCount -or
            $finalPreflight.SteamAppIdCount -ne $preflight.SteamAppIdCount) {
            throw "STEAMPIPE_PROMOTED_SOURCE_MUTATED"
        }
        [IO.Directory]::Move($temporary, $outputFull)
    } catch {
        if (Test-Path -LiteralPath $temporary -PathType Container) {
            Remove-Item -LiteralPath $temporary -Recurse -Force
        }
        throw
    }

    return [pscustomobject][ordered]@{
        OutputRoot = $outputFull
        AppVdf = Join-Path $outputFull $appFileName
        DepotVdf = Join-Path $outputFull $depotFileName
        BuildOutput = $buildOutput
        ContentRoot = $contentRoot
        Classification = $classification
        VdfAggregateSha256 = $aggregate
        ContentManifestSha256 = $preflight.ManifestSha256
        SteamCmdExecuted = $false
        NetworkOperation = $false
    }
}

if ($env:VECTORQUAKE_STEAMPIPE_DRYRUN_TEST_MODE -ne "1") {
    if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
        $RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
    }
    try {
        $result = Invoke-PrepareSteamPipeBuild `
            -AppId $AppId `
            -DepotId $DepotId `
            -PromotedSteamWindowsRoot $PromotedSteamWindowsRoot `
            -OutputRoot $OutputRoot `
            -IdentityMode $IdentityMode `
            -RepositoryRoot $RepositoryRoot `
            -DryRun:$DryRun
        Write-Host "STEAMPIPE_PREAPPID_LOCAL_DRY_RUN_PASS"
        $result | Format-List
        exit 0
    } catch {
        Write-Error $_.Exception.Message
        exit 141
    }
}
