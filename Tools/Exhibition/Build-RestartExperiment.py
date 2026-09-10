#!/usr/bin/env python3
"""Build an opt-in internal experiment; never install, upload, or start a player/Steam."""
import argparse
import datetime
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess


def sha(data):
    return hashlib.sha256(data).hexdigest()


def source_manifest(root):
    sources = list((root / 'Assets/_Features/Exhibition').rglob('*.cs')) + list((root / 'Assets/_Features/Exhibition/Tools').glob('*.ps1'))
    sources += list((root / 'Assets/_Features/Exhibition').rglob('*.asmdef'))
    sources += [root / 'Packages/com.j2m.thirdparty.steamworksnet/Plugins/steam_api64.dll']
    sources += [root / p for p in (
        'Assets/_Features/UI/UI_Application/Runtime/MainMenuPorts.cs',
        'Assets/_Features/UI/UI_Composition/Runtime/MainMenuUiFlowInstaller.cs',
        'Assets/_Features/UI/UI_Screens/Runtime/MainMenuScreenView.cs',
        'Assets/_Features/UI/UI_Tests/EditMode/MainMenuHubTests.cs',
        'Tools/Exhibition/Tests/Restart-Experiment.Tests.ps1',
        'Tools/Exhibition/Tests/Restart-Experiment.Fakes.cs',
        'Tools/Exhibition/Build-RestartExperiment.py',
        'Assets/_Features/Achievements/Achievement_Composition/Runtime/ProductAchievementStartupControl.cs',
        'Assets/_Features/Achievements/Achievement_Composition/Runtime/ProductAchievementApplicationComposition.cs',
        'Assets/_Features/Achievements/Achievement_Composition/Runtime/AssemblyInfo.cs',
        'Packages/com.j2m.platform.steam/Runtime/AssemblyInfo.cs')]
    for directory in ('Assets/_Core/Runtime/Platform', 'Packages/com.j2m.platform.steam/Runtime',
                      'Packages/com.j2m.platform.steam.steamworksnet/Runtime',
                      'Assets/_Features/Achievements/Achievement_Composition/Runtime'):
        sources += list((root / directory).rglob('*.cs'))
        sources += list((root / directory).rglob('*.asmdef'))
    sources += [root / 'Assets/_Features/Stages/Runtime/Campaign/Save/CampaignSaveCompositionProvider.cs',
                root / 'Assets/_Features/Stages/Runtime/Campaign/Save/AtomicTextFileStore.cs']
    sources += list((root / 'Tools/Exhibition').glob('*.py')) + list((root / 'Tools/Exhibition').glob('*.ps1'))
    for directory in ('Assets/_Features/Achievements', 'Assets/_Features/Stages/Runtime/Campaign/Save',
                      'Assets/_Features/Stages/Editor/Tests', 'Packages/com.j2m.platform.steam/Tests'):
        for pattern in ('*.cs', '*.asmdef', '*.asmref', '*.meta'):
            sources += list((root / directory).rglob(pattern))
    sources += list((root / 'Assets/_Features/Exhibition').rglob('*.meta'))
    for pattern in ('*.py', '*.ps1', '*.cs'):
        sources += list((root / 'Tools/Exhibition/Tests').glob(pattern))
    sources += [root / p for p in ('Packages/manifest.json', 'Packages/packages-lock.json',
                                 'ProjectSettings/ProjectSettings.asset', 'ProjectSettings/EditorBuildSettings.asset')]
    return {str(p.relative_to(root)): sha(p.read_bytes()) for p in sorted(set(sources))}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--run-id', default=datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%dT%H%M%SZ'))
    purpose = parser.add_mutually_exclusive_group()
    purpose.add_argument('--overlay-observation', action='store_true')
    purpose.add_argument('--reset-overlay-trial', action='store_true')
    purpose.add_argument('--overlay-observation-v3', action='store_true')
    parser.add_argument('--validated-sources')
    args = parser.parse_args()
    if (args.overlay_observation or args.reset_overlay_trial or args.overlay_observation_v3) and not args.validated_sources:
        raise SystemExit('Observation/reset trial builds require the same-source validation manifest')
    if not re.fullmatch(r'[A-Za-z0-9_-]+', args.run_id):
        raise SystemExit('Invalid run id')
    root = Path(__file__).resolve().parents[2]
    if not str(root).startswith('/mnt/d/J2M/worktrees/'):
        raise SystemExit('Diagnostic Unity build requires a D-drive J2M worktree')
    family = 'overlay-observation-v3' if args.overlay_observation_v3 else 'reset-overlay-trial' if args.reset_overlay_trial else 'overlay-handoff-observation' if args.overlay_observation else 'participant-restart-preflight'
    evidence = Path('/mnt/d/J2M/evidence') / family / args.run_id / 'build'
    output = Path('/mnt/d/J2M/builds') / family / args.run_id / '.staging-experiment' / 'VectorQuake.exe'
    if evidence.exists() or output.parent.exists():
        raise SystemExit('Use a fresh run id; existing evidence/builds are preserved')
    evidence.mkdir(parents=True)
    output.parent.mkdir(parents=True)
    win = lambda p: subprocess.check_output(['wslpath', '-w', str(p)], text=True).strip()
    settings = root / 'ProjectSettings/ProjectSettings.asset'
    before = settings.read_bytes()
    symbol = b'J2M_PARTICIPANT_RESTART_EXPERIMENT;J2M_PARTICIPANT_RESET_DIAGNOSTICS'
    if args.overlay_observation_v3:
        symbol += b';J2M_OVERLAY_OBSERVATION_ONLY'
    match = re.search(rb'(  scriptingDefineSymbols:.*?\n    Standalone: )([^\r\n]*)', before, re.S)
    if not match or any(flag in before for flag in symbol.split(b';')):
        raise SystemExit('Unexpected define baseline; no settings changed')
    (evidence / 'ProjectSettings.before.asset').write_bytes(before)
    # Unity/Addressables can rewrite these generated files. Preserve pre-existing user bytes.
    protected = [root / 'Assets/AddressableAssetsData' / n for n in ('Windows.meta', 'link.xml', 'link.xml.meta')]
    backups = {p: p.read_bytes() for p in protected if p.exists()}
    for i, data in enumerate(backups.values()):
        (evidence / ('generated-before-' + str(i))).write_bytes(data)
    manifest = source_manifest(root)
    if args.validated_sources and manifest != json.loads(Path(args.validated_sources).read_text()):
        raise SystemExit('Sources differ from the validated snapshot')
    (evidence / 'source-hashes.json').write_text(json.dumps(manifest, indent=2))
    (evidence / 'git-head.txt').write_bytes(subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=root))
    (evidence / 'working-copy.diff').write_bytes(subprocess.check_output(['git', 'diff'], cwd=root))
    (evidence / 'git-status.txt').write_bytes(subprocess.check_output(['git', 'status', '--short', '--branch'], cwd=root))
    command = [os.environ.get('J2M_EXPERIMENT_UNITY', '/mnt/c/Users/user/Desktop/6000.3.11f1/Editor/Unity.exe'),
               '-batchmode', '-nographics', '-projectPath', win(root), '-buildTarget', 'Win64',
               '-executeMethod', 'WindowsReleaseBuildCli.BuildWindowsX64NonDevelopment',
               '-logFile', win(evidence / 'unity-build.log'), '-releaseOutputPath', win(output),
               '-releaseRunId', args.run_id, '-releaseArtifactId', 'restart-experiment-uncommitted',
               '-releaseSourceSha', 'uncommitted-working-copy', '-releaseSourceTree', 'uncommitted-working-copy',
               '-releaseIntermediateMetadataPath', win(evidence / 'intermediate.json'),
               '-releaseBuildReportPath', win(evidence / 'build-report.json'),
               '-releaseBuildReportDetailsPath', win(evidence / 'build-details.json'),
               '-releaseSettingsTransactionPath', win(evidence / 'settings-transaction.json'),
               '-releaseDistributionTarget', 'steam-windows']
    (evidence / 'arguments.json').write_text(json.dumps(command, indent=2))
    code = 1
    settings.write_bytes(before[:match.end(2)] + b';' + symbol + before[match.end(2):])
    try:
        with (evidence / 'console.log').open('w') as log:
            code = subprocess.call(command, cwd=root, stdout=log, stderr=subprocess.STDOUT)
        (evidence / 'exit-code.txt').write_text(str(code))
    finally:
        for path, data in backups.items():
            path.write_bytes(data)
        # Remove only our temporary define; do not overwrite unexpected concurrent settings edits.
        restored = settings.read_bytes().replace(b';' + symbol, b'')
        settings.write_bytes(restored)
        (evidence / 'define-transaction.json').write_text(json.dumps({
            'beforeSha256': sha(before), 'afterSha256': sha(restored), 'restoredExact': restored == before,
            'symbol': symbol.decode()}, indent=2))
        if restored != before:
            raise RuntimeError('Settings differ after removing experiment define; inspect saved baseline')
    if code:
        raise SystemExit(code)
    if manifest != source_manifest(root):
        raise RuntimeError('Source changed during build; artifact is not validated')
    artifacts = {str(p.relative_to(output.parent)): sha(p.read_bytes()) for p in output.parent.rglob('*') if p.is_file()}
    if any(Path(p).name.lower() == 'steam_appid.txt' for p in artifacts):
        raise RuntimeError('Unexpected steam_appid.txt')
    for name in ('RestartExperiment.cs', 'RestartExperimentWindows.cs', 'RestartExperimentNativeProbe.cs', 'ObservationV3Wire.cs', 'ObservationV3Handoff.cs', 'ObservationV3Pipe.cs', 'ObservationV3RuntimeWire.cs', 'ObservationV3RuntimeProtocol.cs', 'ObservationV3Admission.cs', 'ObservationV3JournalReader.cs', 'ObservationV3Session.cs', 'ObservationV3WindowsEnvironment.cs', 'ObservationV3WindowsHost.cs'):
        if artifacts.get('RestartExperiment/' + name) != manifest['Assets/_Features/Exhibition/Integration/' + name]:
            raise RuntimeError('Diagnostic source artifact mismatch: ' + name)
    if artifacts.get('RestartExperiment/Restart-Experiment.ps1') != manifest['Assets/_Features/Exhibition/Tools/Restart-Experiment.ps1']:
        raise RuntimeError('Diagnostic helper mismatch')
    prerequisite = json.loads((output.parent / 'RestartExperiment/prerequisites.example.json').read_text())
    if any(prerequisite.get(name) is not False for name in
           ('ShutdownCommandVerified', 'ProbeContractReviewed', 'FailedInitExitReviewed', 'CallbackPumpReviewed')):
        raise RuntimeError('Distributed prerequisites must remain disabled')
    if prerequisite.get('GateAEvidence') != '' or prerequisite.get('GateBEvidence') != '':
        raise RuntimeError('Distributed gate evidence must remain empty')
    (evidence / 'artifact-hashes.json').write_text(json.dumps(artifacts, indent=2))
    print('Diagnostic build prepared:', output)
    print('Evidence:', evidence)
    print('Installation, upload, player/Steam execution: not performed')


if __name__ == '__main__':
    main()
