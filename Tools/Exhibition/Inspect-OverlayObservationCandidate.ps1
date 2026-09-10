[CmdletBinding()]
param([Parameter(Mandatory)][string]$RawRoot, [Parameter(Mandatory)][string]$ReportPath,
      [switch]$RequireResetTrial, [switch]$RequireObservationV3, [string]$CecilPath = 'C:\Users\user\Desktop\6000.3.11f1\Editor\Data\Managed\Unity.Cecil.dll')
$ErrorActionPreference = 'Stop'
Add-Type -Path $CecilPath
function Get-BinaryHashes {
    $hashes = @{}
    foreach ($file in (Get-ChildItem -LiteralPath $RawRoot -Recurse -File | Where-Object { $_.Extension -in @('.dll', '.exe') })) {
        $relative = $file.FullName.Substring($RawRoot.Length).TrimStart('\').Replace('\', '/')
        $hashes[$relative] = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    }
    return $hashes
}
function Assert-Call($Type, [string]$Method, [string]$Target) {
    $methodDefinition = $Type.Methods | Where-Object Name -CEQ $Method
    if (-not ($methodDefinition.Body.Instructions | Where-Object { $_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.Name -ceq $Target })) {
        throw "Compiled connection missing: $($Type.FullName).$Method -> $Target"
    }
}
function Get-CallsIncludingNested($Type) {
    foreach ($method in $Type.Methods) {
        if ($method.HasBody) {
            foreach ($instruction in $method.Body.Instructions) {
                if ($instruction.Operand -is [Mono.Cecil.MethodReference]) { $instruction.Operand }
            }
        }
    }
    foreach ($nested in $Type.NestedTypes) { Get-CallsIncludingNested $nested }
}
$before = Get-BinaryHashes
$managed = Join-Path $RawRoot 'VectorQuake_Data/Managed'
$assemblies = @{}
try {
    foreach ($name in @('Game.Exhibition.Integration', 'Game.Platform.Runtime', 'Game.Platform.Steam', 'Game.Platform.Steam.SteamworksNet', 'Game.Product.Achievements.Composition', 'Game.Feature.UI.Composition', 'Game.Exhibition.Application', 'Game.Product.Achievements.Infrastructure', 'Game.Feature.Stages')) {
        $assemblies[$name] = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $managed ($name + '.dll')))
    }
    $types = $assemblies['Game.Exhibition.Integration'].MainModule.Types
    foreach ($name in @('OverlayHandoffObservation', 'OverlayHandoffObservationRuntime', 'OverlayHandoffObservationPresentation', 'OverlayHandoffObservationOptions')) {
        if (-not ($types | Where-Object FullName -CEQ ('Game.Exhibition.Integration.' + $name))) { throw "Missing compiled observation type: $name" }
    }
    $app = $types | Where-Object Name -CEQ 'ExhibitionApplication'
    Assert-Call $app 'InspectJournal' 'InspectProcessStartup'
    Assert-Call $app 'InspectProcessStartup' 'InspectObservationStartup'
    Assert-Call $app 'InspectProcessStartup' 'InspectStartup'
    Assert-Call $app 'InspectStartup' 'InspectObservationStartup'
    Assert-Call $app 'InspectObservationStartup' 'Present'
    Assert-Call $app 'ComposeObservation' '.ctor'
    $compose = $app.Methods | Where-Object Name -CEQ 'ComposeObservation'
    if (-not ($compose.Body.Instructions | Where-Object { $_.OpCode.Name -ceq 'newobj' -and $_.Operand.DeclaringType.Name -ceq 'OverlayHandoffObservationRuntime' })) { throw 'Observation runtime composition missing' }
    foreach ($forbidden in @('ExhibitionResetCoordinator', 'ParticipantResetService', 'SteamExhibitionResetAdapter', 'ParticipantProgressResetAdapter')) {
        if ($compose.Body.Instructions | Where-Object { $_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.DeclaringType.Name -ceq $forbidden }) {
            throw "Observation composes forbidden reset type: $forbidden"
        }
    }
    $inspect = $app.Methods | Where-Object Name -CEQ 'InspectJournal'
    if (-not ($inspect.Body.Instructions | Where-Object { $_.OpCode.Name -ceq 'ldc.i4.1' })) { throw 'Observation opt-in is not enabled in this candidate' }
    $runtime = $assemblies['Game.Platform.Steam'].MainModule.Types | Where-Object Name -CEQ 'SteamPlatformRuntime'
    Assert-Call $runtime 'Tick' 'RunCallbacks'
    Assert-Call $runtime 'Tick' 'get_Requested'
    Assert-Call $runtime 'Initialize' 'Starting'
    Assert-Call $runtime 'ReadObservationIdentity' 'GetSteamId'
    $observation = $types | Where-Object Name -CEQ 'OverlayHandoffObservation'
    $observationRuntime = $types | Where-Object Name -CEQ 'OverlayHandoffObservationRuntime'
    foreach ($pair in @(@('ReportAsync', 'SaveObservationAsync'), @('ReplaceAsync', 'RevalidateAsync'), @('ReplaceAsync', 'Claim'), @('ReplaceAsync', 'StartHelper'))) {
        $stateMachine = $observation.NestedTypes | Where-Object { $_.Name.StartsWith('<' + $pair[0] + '>') }
        Assert-Call $stateMachine 'MoveNext' $pair[1]
    }
    Assert-Call $observationRuntime 'StartHelper' 'ValidateOriginReport'
    $wire = $types | Where-Object Name -CEQ 'OverlayObservationWire'
    Assert-Call $wire 'ReadContext' 'ValidateOriginReport'
    Assert-Call $wire 'WriteReceipt' 'ReadContext'
    $context = $types | Where-Object Name -CEQ 'OverlayObservationContext'
    foreach ($field in @('OriginObservationPath', 'OriginObservationSha256')) {
        if (-not ($context.Fields | Where-Object Name -CEQ $field)) { throw "Missing report pin: $field" }
    }
    if ($context.Fields | Where-Object { $_.Name -like '*Sdk*' }) { throw 'SDK context dependency remains' }
    foreach ($assembly in $assemblies.Values) {
        foreach ($type in $assembly.MainModule.Types) {
            if ($type.Name -in @('SteamOverlayObservationSubscription', 'SteamOverlaySample', 'SteamOverlayActivation',
                    'OverlayObservationLogCollector', 'OverlayObservationScreenFiles', 'ObservationSdkBaseline', 'ObservationScreenCapture')) {
                throw "Removed observation producer remains: $($type.Name)"
            }
        }
    }
    foreach ($method in @('ValidateRequest', 'ValidateContext', 'WriteReceipt', 'Matches')) {
        $body = ($wire.Methods | Where-Object Name -CEQ $method).Body
        if (-not ($body.Instructions | Where-Object { $_.OpCode.Name -ceq 'ldc.i4.2' })) { throw "Minimal wire v2 missing: $method" }
    }
    $native = $assemblies['Game.Platform.Steam.SteamworksNet'].MainModule.Types | Where-Object Name -CEQ 'SteamworksNetNativeApi'
    Assert-Call $native 'SetAchievement' 'RequireWritesAllowed'
    Assert-Call $native 'StoreStats' 'RequireWritesAllowed'
    $installer = $assemblies['Game.Feature.UI.Composition'].MainModule.Types | Where-Object Name -CEQ 'MainMenuUiFlowInstaller'
    Assert-Call $installer 'RefreshParticipantState' 'get_OwnsStatusPresentation'
    if ($RequireResetTrial) {
        $reset = $types | Where-Object Name -CEQ 'ResetOverlayTrial'
        $resetRuntime = $types | Where-Object Name -CEQ 'ResetOverlayTrialRuntime'
        $resetWire = $types | Where-Object Name -CEQ 'ResetOverlayWire'
        $receipt = $types | Where-Object Name -CEQ 'ResetOverlayReceipt'
        foreach ($t in @($reset, $resetRuntime, $resetWire, $receipt)) { if (-not $t) { throw 'Compiled reset dependency missing' } }
        Assert-Call $app 'InspectProcessStartup' 'InspectResetStartup'
        Assert-Call $app 'InspectStartup' 'InspectResetStartup'
        Assert-Call $app 'InspectResetStartup' 'Present'
        Assert-Call $app 'InspectResetStartup' 'InhibitForResetTrial'
        foreach ($pair in @(@('ReportCoreAsync', 'SaveReportAsync'), @('RequestCoreAsync', 'RevalidateAsync'),
                @('RequestCoreAsync', 'Claim'), @('RequestCoreAsync', 'RequestReset'), @('RequestCoreAsync', 'StartHelper'),
                @('PrepareCoreAsync', 'ResumeAsync'), @('PrepareCoreAsync', 'VerifyResetAsync'))) {
            $state = $reset.NestedTypes | Where-Object { $_.Name.StartsWith('<' + $pair[0] + '>') }
            Assert-Call $state 'MoveNext' $pair[1]
            Assert-Call $state 'MoveNext' 'Guard'
        }
        Assert-Call $resetRuntime 'StartHelper' 'ReadContext'
        Assert-Call $resetRuntime 'ValidateRecord' 'ValidateClient'
        Assert-Call $resetRuntime 'ValidateClient' 'Same'
        $reportState = $resetRuntime.NestedTypes | Where-Object { $_.Name.StartsWith('<SaveReportAsync>') }
        Assert-Call $reportState 'MoveNext' 'ValidateReport'
        Assert-Call $reportState 'MoveNext' 'CreateAsync'
        Assert-Call $resetRuntime 'VerifyLocal' 'LoadReadOnly'
        Assert-Call $resetWire 'ClaimChildCreation' 'ReadContext'
        Assert-Call $resetWire 'ReadContext' 'ValidateBaseline'
        Assert-Call $resetWire 'ReadContext' 'ValidateReport'
        Assert-Call $receipt 'Write' 'ReadContext'
        Assert-Call $receipt 'Matches' 'ReadContext'
        $interface = $types | Where-Object Name -CEQ 'IResetOverlayTrialRuntime'
        foreach ($name in @('StartServices', 'Reconcile', 'Launch', 'ContinueAfterOverlay')) {
            if (($reset.Methods + $resetRuntime.Methods + $interface.Methods) | Where-Object Name -CEQ $name) { throw "Forbidden reset capability: $name" }
        }
        foreach ($type in @($reset, $resetRuntime) + @($reset.NestedTypes) + @($resetRuntime.NestedTypes)) {
            foreach ($method in $type.Methods) {
                if ($method.HasBody -and ($method.Body.Instructions | Where-Object { $_.Operand -is [Mono.Cecil.MethodReference] -and
                    $_.Operand.Name -in @('StartDeferredServices', 'StartPublication', 'ReconcileCampaign', 'StartSteam', 'RequestSteamExit') })) {
                    throw "Reset path resumes services or cycles Steam: $($method.FullName)"
                }
            }
        }
        $make = $resetRuntime.Methods | Where-Object Name -CEQ 'MakeRequest'
        $stores = @($make.Body.Instructions | Where-Object { $_.OpCode.Name -ceq 'stfld' -and $_.Operand.Name -ceq 'Trial' })
        if ($stores.Count -ne 1 -or $stores[0].Previous.OpCode.Name -cne 'ldc.i4.0') { throw 'Reset launch must hardcode Trial.GameOnly' }
        $maintenance = $assemblies['Game.Platform.Steam'].MainModule.Types | Where-Object Name -CEQ 'SteamAchievementMaintenanceAccess'
        Assert-Call $runtime 'StartDeferredPublication' 'get_ResetTrial'
        Assert-Call $runtime 'Tick' 'get_ResetTrial'
        $repository = $assemblies['Game.Product.Achievements.Infrastructure'].MainModule.Types | Where-Object Name -CEQ 'FileProductAchievementRepository'
        Assert-Call $repository 'LoadReadOnly' 'TryRead'
        $readOnly = $repository.Methods | Where-Object Name -CEQ 'LoadReadOnly'
        if ($readOnly.Body.Instructions | Where-Object { $_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.Name -in @('CleanupTempFiles','TryRestoreBackup','TryQuarantine','WriteAllTextAtomic','Load') }) {
            throw 'Read-only result verification has a mutation/recovery dependency'
        }
        $context = $types | Where-Object Name -CEQ 'ResetOverlayTrialContext'
        foreach ($name in @('HandoffNonce','ReadyOperationId','OperationId','BaselineSha256','OriginReportSha256','PendingFilesSha256','Steam')) {
            if (-not ($context.Fields | Where-Object Name -CEQ $name)) { throw "Reset pin missing: $name" }
        }
    }
    if ($RequireObservationV3) {
        if ($RequireResetTrial) { throw 'V3 observation and reset candidate modes are exclusive.' }
        foreach ($name in @('ObservationV3Options','ObservationV3RuntimeWire','ObservationV3WindowsHost','ObservationV3WindowsCycleEnvironment','ObservationV3Runtime','ObservationV3Flow','ObservationV3Presentation','ObservationV3Admission','ObservationV3JournalReader','ObservationV3Session','ObservationV3IndependentContext','ObservationV3ReplacementClaim','ObservationV3ReplacementStarted')) {
            if (-not ($types | Where-Object Name -CEQ $name)) { throw "Missing compiled v3 type: $name" }
        }
        $wire = $types | Where-Object Name -CEQ 'ObservationV3RuntimeWire'
        if (($wire.Fields | Where-Object Name -CEQ 'Revision').Constant -cne 'observation-v3-runtime-3') { throw 'Runtime-3 revision required.' }
        Assert-Call $app 'InspectProcessStartup' 'InspectV3Startup'
        Assert-Call $app 'InspectStartup' 'InspectV3Startup'
        Assert-Call $app 'InspectV3Startup' 'BlockNativeStartup'
        Assert-Call $app 'InspectV3Startup' 'StartObservation'
        $v3Runtime = $types | Where-Object Name -CEQ 'ObservationV3Runtime'
        Assert-Call $v3Runtime 'StartObservation' 'Request'
        $v3Host = $types | Where-Object Name -CEQ 'ObservationV3WindowsHost'
        $v3Launch = $types | Where-Object Name -CEQ 'ObservationV3WindowsLaunch'
        $runtimeCalls = @(Get-CallsIncludingNested $v3Runtime)
        $hostCalls = @(Get-CallsIncludingNested $v3Host)
        foreach ($name in @('StartReplacement','InitializeNative','AcceptReport','ShutdownOnce')) {
            if (-not ($runtimeCalls | Where-Object Name -CEQ $name)) { throw "Missing independent runtime connection: $name" }
        }
        if (-not ($hostCalls | Where-Object Name -CEQ 'SubmitAndReturn')) { throw 'Short helper submission missing.' }
        Assert-Call $v3Launch 'SubmitAndReturn' 'InvokeAndReturn'
        foreach ($call in @($runtimeCalls + $hostCalls)) {
            if ($call.DeclaringType.Name -in @('ObservationV3Pipe','ObservationV3RuntimeHandoff','ObservationV3RuntimeHostPorts')) {
                throw "Runtime-3 still depends on child pipe/handoff: $($call.FullName)"
            }
        }
        if ($types | Where-Object Name -CEQ 'ObservationV3RuntimeHostPorts') { throw 'Retired long-lived production host remains.' }
        $independent = $types | Where-Object Name -CEQ 'ObservationV3IndependentContext'
        if ($independent.Fields | Where-Object Name -CEQ 'Endpoint') { throw 'Independent context still requires endpoint.' }

        $access = $assemblies['Game.Platform.Steam'].MainModule.Types | Where-Object Name -CEQ 'SteamOverlayObservationAccess'
        Assert-Call $access 'Starting' 'Consume'
        Assert-Call $runtime 'ReadAchievement' 'GetAchievement'
        $bootstrap = $assemblies['Game.Platform.Runtime'].MainModule.Types | Where-Object Name -CEQ 'PlatformRuntimeBootstrap'
        Assert-Call $bootstrap 'RunAutomaticBootstrap' 'get_IsDeferred'
        foreach ($methodName in @('InspectProcessStartup','InspectStartup')) {
            $calls = @(($app.Methods | Where-Object Name -CEQ $methodName).Body.Instructions | Where-Object { $_.Operand -is [Mono.Cecil.MethodReference] })
            $v3 = @($calls | Where-Object { $_.Operand.Name -ceq 'InspectV3Startup' })[0]
            $ordinary = @($calls | Where-Object { $_.Operand.Name -ceq 'InspectObservationStartup' })[0]
            if ($v3.Offset -ge $ordinary.Offset) { throw 'V3 must guard ordinary startup first.' }
        }
        $stages = $assemblies['Game.Feature.Stages'].MainModule.Types | Where-Object Name -CEQ 'CampaignSaveCompositionProvider'
        $policy = $stages.Methods | Where-Object Name -CEQ 'get_ObservationBuild'
        if (-not ($policy.Body.Instructions | Where-Object { $_.OpCode.Name -ceq 'ldc.i4.1' })) { throw 'Compiled always-inhibited candidate policy absent.' }
        Assert-Call $stages 'RequireProductionWritesAllowed' 'get_ObservationBuild'
        $startup = $assemblies['Game.Product.Achievements.Composition'].MainModule.Types | Where-Object Name -CEQ 'ProductAchievementStartupControl'
        Assert-Call $startup 'get_ObservationOnly' 'get_ObservationBuild'
        Assert-Call $startup 'get_IsDeferred' 'get_ObservationBuild'
        $access = $assemblies['Game.Platform.Steam'].MainModule.Types | Where-Object Name -CEQ 'SteamOverlayObservationAccess'
        Assert-Call $access 'get_Requested' 'get_ObservationBuild'
        Assert-Call $access 'Starting' 'get_ObservationBuild'
        $v3Entry = $app.Methods | Where-Object Name -CEQ 'InspectV3Startup'
        foreach ($call in $v3Entry.Body.Instructions | Where-Object { $_.Operand -is [Mono.Cecil.MethodReference] }) {
            if ($call.Operand.Name -in @('Compose','ComposeObservation','StartHelper','StartSteam','StartGame','Initialize','AcquireSessionLock')) {
                throw "Live v3 startup connected prematurely: $($call.Operand.Name)"
            }
        }
    }
    $after = Get-BinaryHashes
    if ($before.Count -ne $after.Count) { throw 'Binary set changed during metadata inspection' }
    foreach ($name in $before.Keys) { if ($before[$name] -cne $after[$name]) { throw "Binary changed during inspection: $name" } }
    @{ Verified = $true; RawBuildDirectory = $RawRoot; RawBinaryHashes = $before;
       Mode = 'Cecil metadata read only'; ObservationComposition = $true; NativeIdentityAccess = $true; ResetTrialChecked = [bool]$RequireResetTrial; ObservationV3Checked = [bool]$RequireObservationV3;
       NativeWriteGuards = $true; StatusOwnership = $true; PlayerOrNativeExecuted = $false } |
        ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $ReportPath -Encoding UTF8
} finally { foreach ($assembly in $assemblies.Values) { $assembly.Dispose() } }
