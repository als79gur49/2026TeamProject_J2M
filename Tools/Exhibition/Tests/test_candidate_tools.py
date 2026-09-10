"""Pure file/argument checks. No Unity, Steam, player, installed payload or participant save access."""
import importlib.util
import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[3]
def module(name):
    spec = importlib.util.spec_from_file_location(name, ROOT / 'Tools/Exhibition' / (name + '.py'))
    value = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(value)
    return value

builder = module('Build-RestartExperiment')
prepare = module('Prepare-OverlayObservationCandidate')

class CandidateToolsTests(unittest.TestCase):
    def test_both_modes_require_validation_before_build(self):
        for flag in ('--overlay-observation', '--reset-overlay-trial', '--overlay-observation-v3'):
            with self.subTest(flag=flag), patch.object(sys, 'argv', ['builder', flag]), self.assertRaises(SystemExit):
                builder.main()

    def test_mode_flags_are_exclusive(self):
        with patch.object(sys, 'argv', ['builder', '--overlay-observation', '--reset-overlay-trial']), self.assertRaises(SystemExit):
            builder.main()

    def test_manifest_covers_actual_changed_clusters_and_tooling(self):
        values = builder.source_manifest(ROOT)
        for name in ('Assets/_Features/Exhibition/Integration/ResetOverlayTrial.cs',
                     'Assets/_Features/Exhibition/Integration/RestartExperimentWindows.cs',
                     'Assets/_Features/Exhibition/Tests/ResetOverlayRuntimeAndWireTests.cs',
                     'Assets/_Features/Exhibition/Tests/PlayMode/ResetOverlayPresentationTests.cs',
                     'Assets/_Features/Achievements/Achievement_Infrastructure/Runtime/FileProductAchievementRepository.cs',
                     'Packages/com.j2m.platform.steam/Runtime/SteamPlatformRuntime.cs',
                     'Tools/Exhibition/Tests/Restart-Experiment.Fakes.cs',
                     'Tools/Exhibition/Inspect-OverlayObservationCandidate.ps1',
                     'Tools/Exhibition/Tests/test_candidate_tools.py',
                     'ProjectSettings/ProjectSettings.asset'):
            self.assertIn(name, values)
            self.assertEqual(values[name], builder.sha((ROOT / name).read_bytes()))

    def test_unique_evidence_and_complete_hash_set(self):
        base = Path('/mnt/d/J2M/evidence/reset-overlay-fakes'); base.mkdir(parents=True, exist_ok=True)
        with tempfile.TemporaryDirectory(dir=base) as directory:
            root = Path(directory); path = root / 'row.json'
            prepare.write(path, {'Version': 2})
            self.assertEqual(prepare.read(path), {'Version': 2})
            with self.assertRaises(FileExistsError): prepare.write(path, {})
            path.write_text('{"Version":2,"Version":1}')
            with self.assertRaises(ValueError): prepare.read(path)
            (root / 'nested').mkdir(); extra = root / 'nested/extra'; extra.write_bytes(b'extra')
            self.assertEqual(prepare.hashes(root), {'row.json': prepare.digest(path), 'nested/extra': prepare.digest(extra)})

if __name__ == '__main__':
    unittest.main()
