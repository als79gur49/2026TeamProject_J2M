# Terminal Iris Evidence Integrity

The evidence workflow has two independent execution units.

Run producer and verifier unit tests:

```bash
python3 -m unittest -v test_produce_bundle.py test_verify_bundle.py
```

Produce and close a fresh bundle:

```bash
Tools/TerminalIrisEvidenceIntegrity/run_bundle.sh --produce
```

By default, the producer writes under:

```text
TestLogs/TerminalIrisCoreArtEvidenceIntegrity/Bundles/<BundleId>/
```

Storage-policy or CI runs can keep the same behavior while selecting an
external parent directory:

```bash
TERMINAL_IRIS_EVIDENCE_BUNDLE_ROOT=/mnt/d/J2M/evidence/terminal-transition/bundles \
  TERMINAL_IRIS_QUALITY_PLAYER_BUILD_ROOT=/mnt/d/J2M/builds/terminal-transition/evidence \
  Tools/TerminalIrisEvidenceIntegrity/run_bundle.sh --produce
```

It records the source freeze, runs the exact 12-lane matrix, writes
`artifact-hashes.sha256`, `bundle-produced.json`, and `BUNDLE_CLOSED`, and does
not run verification.

Verify a closed bundle into an external root:

```bash
python3 Tools/TerminalIrisEvidenceIntegrity/verify_bundle.py \
  --bundle TestLogs/TerminalIrisCoreArtEvidenceIntegrity/Bundles/<BundleId> \
  --contract Tools/TerminalIrisEvidenceIntegrity/evidence-contract-v1.json \
  --output TestLogs/TerminalIrisCoreArtEvidenceIntegrity/Verifications/<BundleId>/<VerificationId>
```

Run verifier mutation tests after the primary verification:

```bash
python3 Tools/TerminalIrisEvidenceIntegrity/run_negative_tests.py \
  --bundle TestLogs/TerminalIrisCoreArtEvidenceIntegrity/Bundles/<BundleId> \
  --contract Tools/TerminalIrisEvidenceIntegrity/evidence-contract-v1.json \
  --output TestLogs/TerminalIrisCoreArtEvidenceIntegrity/Verifications/<BundleId>/<VerificationId>
```

The verifier never writes inside the bundle. It snapshots every input file
before and after verification, validates the producer-created manifest, and
writes all reports under the supplied external output root.
