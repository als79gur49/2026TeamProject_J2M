# Terminal Iris Evidence Integrity

The evidence workflow has two independent execution units.

Produce and close a fresh bundle:

```bash
Tools/TerminalIrisEvidenceIntegrity/run_bundle.sh --produce
```

The producer writes only under:

```text
TestLogs/TerminalIrisCoreArtEvidenceIntegrity/Bundles/<BundleId>/
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
