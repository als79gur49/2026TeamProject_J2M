#!/usr/bin/env python3
"""Inspect/stage a local observation candidate and prepare config. Never upload, install or execute the game."""
import argparse
import hashlib
import importlib.util
import json
import re
import uuid
from pathlib import Path
import shutil
import subprocess


def digest(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def read(path):
    def unique(pairs):
        result = {}
        for key, value in pairs:
            if key in result:
                raise ValueError('Duplicate JSON field: ' + key)
            result[key] = value
        return result
    return json.loads(Path(path).read_text(encoding='utf-8-sig'), object_pairs_hook=unique)


def write(path, value):
    with Path(path).open('x') as stream:
        json.dump(value, stream, indent=2)
        stream.write('\n')


def hashes(root):
    return {p.relative_to(root).as_posix(): digest(p) for p in sorted(root.rglob('*')) if p.is_file()}


def win(path):
    return subprocess.check_output(['wslpath', '-w', str(path)], text=True).strip()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--run-id', required=True)
    parser.add_argument('--reset-overlay-trial', action='store_true')
    parser.add_argument('--validated-sources', type=Path, required=True)
    parser.add_argument('--identity-config', type=Path, required=True)
    parser.add_argument('--ready-journal', type=Path, required=True)
    parser.add_argument('--installed-root', type=Path, required=True)
    parser.add_argument('--config', type=Path, required=True)
    args = parser.parse_args()
    if not args.run_id or any(c not in 'abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_-' for c in args.run_id):
        raise SystemExit('Invalid run ID')
    root = Path(__file__).resolve().parents[2]
    family = 'reset-overlay-trial' if args.reset_overlay_trial else 'overlay-handoff-observation'
    evidence = Path('/mnt/d/J2M/evidence') / family / args.run_id
    build = Path('/mnt/d/J2M/builds') / family / args.run_id
    raw = build / '.staging-experiment'
    payload = build / 'candidate/payload'
    if not str(root).startswith('/mnt/d/J2M/worktrees/') or not str(args.config.resolve()).startswith('/mnt/d/J2M/evidence/'):
        raise SystemExit('Observation preparation requires D worktree/evidence paths')
    if args.config.exists() or payload.exists():
        raise SystemExit('Existing config/candidate is preserved; use fresh output paths')
    spec = importlib.util.spec_from_file_location('observation_builder', root / 'Tools/Exhibition/Build-RestartExperiment.py')
    builder = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(builder)
    validated = read(args.validated_sources)
    assert validated == read(evidence / 'build/source-hashes.json') == builder.source_manifest(root), 'Source snapshot differs'
    assert read(evidence / 'build/define-transaction.json')['restoredExact'], 'Temporary defines not restored'
    assert digest(root / 'ProjectSettings/ProjectSettings.asset') == digest(evidence / 'build/ProjectSettings.before.asset')
    artifacts = read(evidence / 'build/artifact-hashes.json')
    assert hashes(raw) == artifacts, 'Raw build changed before inspection'
    metadata_path = evidence / 'player-metadata-check.json'
    with (evidence / 'metadata-inspection-console.log').open('x') as log:
        subprocess.run(['powershell.exe', '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File',
                        win(root / 'Tools/Exhibition/Inspect-OverlayObservationCandidate.ps1'),
                        '-RawRoot', win(raw), '-ReportPath', win(metadata_path)] + (['-RequireResetTrial'] if args.reset_overlay_trial else []), stdout=log, stderr=subprocess.STDOUT, check=True)
    inspection = read(metadata_path)
    binaries = {n: h for n, h in artifacts.items() if Path(n).suffix.lower() in ('.exe', '.dll')}
    assert inspection['Verified'] and inspection['RawBinaryHashes'] == binaries
    metadata = read(evidence / 'build/intermediate.json')
    assert metadata['buildResult'] == 'Succeeded' and metadata['errorCount'] == 0
    metadata.update(branch=subprocess.check_output(['git', 'branch', '--show-current'], cwd=root, text=True).strip(),
                    sourceDirty=True, diagnosticPurpose='Single GameOnly achievement reset comparison' if args.reset_overlay_trial else 'Overlay observation without reset')
    write(raw / 'build-metadata.json', metadata)
    for name in ('ThirdPartyNotices.txt', 'UnityPlayerThirdPartyNotices.pdf'):
        shutil.copy2(root / name, raw / name)
    write(evidence / 'packaging-inputs.json', {name: digest(raw / name) for name in
          ('build-metadata.json', 'ThirdPartyNotices.txt', 'UnityPlayerThirdPartyNotices.pdf')})
    with (evidence / 'stage-console.log').open('x') as log:
        subprocess.run(['powershell.exe', '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File',
                        win(root / 'Tools/Build/Stage-WindowsDistribution.ps1'), '-SourceBuildRoot', win(raw),
                        '-DistributionTarget', 'steam-windows', '-OutputRoot', win(build / 'base-staged'),
                        '-RepositoryRoot', win(root), '-RunId', args.run_id], stdout=log, stderr=subprocess.STDOUT, check=True)
    base = build / 'base-staged'
    assert (base / 'evidence/SUCCESS.json').is_file()
    expected = {f['relativePath']: f['sha256'] for f in read(base / 'evidence/distribution-manifest.json')['files']}
    assert hashes(base / 'payload') == expected
    shutil.copytree(base / 'payload', payload)
    extras = ('RestartExperiment.cs', 'RestartExperimentWindows.cs', 'RestartExperimentNativeProbe.cs',
              'Restart-Experiment.ps1', 'prerequisites.example.json')
    assert sorted(p.name for p in (raw / 'RestartExperiment').iterdir()) == sorted(extras)
    shutil.copytree(raw / 'RestartExperiment', payload / 'RestartExperiment')
    for name in extras:
        expected['RestartExperiment/' + name] = digest(raw / 'RestartExperiment' / name)
    assert hashes(payload) == expected
    final_binaries = {n: h for n, h in expected.items() if Path(n).suffix.lower() in ('.exe', '.dll')}
    assert all(binaries.get(n) == h for n, h in final_binaries.items()), 'Final DLL/EXE differs from inspected raw build'
    for name in ('VectorQuake.exe', 'VectorQuake_Data/Managed/Game.Exhibition.Integration.dll',
                 'VectorQuake_Data/Managed/Game.Platform.Steam.dll', 'VectorQuake_Data/Managed/Game.Platform.Steam.SteamworksNet.dll',
                 'VectorQuake_Data/Managed/Game.Product.Achievements.Composition.dll', 'VectorQuake_Data/Managed/Game.Feature.UI.Composition.dll'):
        assert name in final_binaries, 'Required compiled observation dependency missing: ' + name
    assert not any(Path(n).name.lower() == 'steam_appid.txt' for n in expected)
    assert builder.source_manifest(root) == validated, 'Source changed during packaging'
    manifest_path = evidence / 'diagnostic-payload-manifest.json'
    write(manifest_path, {'purpose': 'GameOnly reset comparison' if args.reset_overlay_trial else 'Overlay handoff observation only', 'fileCount': len(expected),
                         'files': [{'relativePath': n, 'sha256': h, 'size': (payload / n).stat().st_size} for n, h in sorted(expected.items())]})
    ready_before = digest(args.ready_journal)
    ready = read(args.ready_journal)
    identity = read(args.identity_config)
    assert ready['SchemaVersion'] == 1 and ready['State'] == 'Ready' and ready['AppId'] == identity['AppId'] == 5218360
    assert uuid.UUID(ready['OperationId']).hex == ready['OperationId']
    mapping = re.search(r'public const string MappingVersion = "([^"]+)"',
                        (root / 'Assets/_Features/Exhibition/Application/ExhibitionResetCoordinator.cs').read_text()).group(1)
    assert ready['MappingVersion'] == mapping
    assert type(identity['SteamId']) is int and identity['SteamId'] != 0 and ready['SteamId'] == identity['SteamId']
    installed = hashes(args.installed_root)
    write(evidence / 'installed-comparison.json', {'matchesCandidate': installed == expected,
          'missing': sorted(set(expected) - set(installed)), 'extra': sorted(set(installed) - set(expected)),
          'changed': sorted(n for n in expected.keys() & installed.keys() if expected[n] != installed[n])})
    assert digest(args.ready_journal) == ready_before
    args.config.parent.mkdir(parents=True, exist_ok=True)
    config = {'Version': 2 if args.reset_overlay_trial else 1, 'AppId': 5218360, 'SteamId': identity['SteamId'], 'PayloadManifestPath': win(manifest_path)}
    if not args.reset_overlay_trial:
        config['EvidenceRoot'] = r'D:\J2M\evidence\overlay-handoff-observation'
    write(args.config, config)
    write(evidence / 'candidate-verification.json', {'verifiedLocalCandidate': True, 'sameValidatedSources': True,
          'compiledPayloadMatchesInspectedRawBuild': True, 'manifestSha256': digest(manifest_path),
          'configSha256': digest(args.config), 'readyJournalSha256': ready_before, 'accountMatchesReady': True,
          'readyOperationId': ready['OperationId'], 'mappingVersion': ready['MappingVersion'],
          'installedMatchesCandidate': installed == expected, 'uploaded': False, 'branchChanged': False,
          'installed': False, 'gameOrNativeExecuted': False, 'manualVisualValidation': 'NotRun'})
    instruction = ('Prepared for a future Steam launch only after this candidate is installed and its complete payload is verified.\n'
                   'Steam user launch options (effective provider must already be exactly one Steam provider):\n' +
                   ('-j2mResetOverlayTrial "' if args.reset_overlay_trial else '-j2mOverlayHandoffObservation "') + win(args.config) + '"\n'
                   'Upload, branch selection, installation, native account verification and manual visual trial were not performed.\n')
    (evidence / 'prepared-start-options.txt').write_text(instruction)
    print('Local candidate:', payload)
    print('Config prepared:', args.config)
    print('Installed payload matches candidate:', installed == expected)


if __name__ == '__main__':
    main()
