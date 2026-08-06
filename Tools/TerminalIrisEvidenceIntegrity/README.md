# Terminal Iris Evidence Integrity

The evidence workflow has two independent execution units.

Run producer and verifier unit tests:

```bash
(
  cd Tools/TerminalIrisEvidenceIntegrity
  python3 -m unittest discover -p 'test_*.py' -v
)
```

Produce and close a fresh bundle:

```bash
Tools/TerminalIrisEvidenceIntegrity/run_bundle.sh --produce
```

By default, the producer writes under:

```text
J2M worktree:
<J2M-root>/evidence/TerminalIrisCoreArtEvidenceIntegrity/Bundles/<BundleId>/

Other repository layout:
<repo-parent>/J2M-Evidence/TerminalIrisCoreArtEvidenceIntegrity/Bundles/<BundleId>/
```

The default is deliberately outside the repository so the strict clean-source
gate cannot reject producer or verifier output as untracked source. Explicit
overrides must also resolve outside the repository; repository-internal roots
are rejected. Storage-policy or CI runs can select an external parent:

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
  --bundle /external/TerminalIrisCoreArtEvidenceIntegrity/Bundles/<BundleId> \
  --contract Tools/TerminalIrisEvidenceIntegrity/evidence-contract-v1.json \
  --output /external/TerminalIrisCoreArtEvidenceIntegrity/Verifications/<BundleId>/<VerificationId>
```

Run verifier mutation tests after the primary verification:

```bash
python3 Tools/TerminalIrisEvidenceIntegrity/run_negative_tests.py \
  --bundle /external/TerminalIrisCoreArtEvidenceIntegrity/Bundles/<BundleId> \
  --contract Tools/TerminalIrisEvidenceIntegrity/evidence-contract-v1.json \
  --output /external/TerminalIrisCoreArtEvidenceIntegrity/Verifications/<BundleId>/<VerificationId>
```

The verifier never writes inside the bundle. It snapshots every input file
before and after verification, validates the producer-created manifest, and
writes all reports under the supplied external output root. Verification is
bound to the exact clean repository, commit, and tree recorded by the source
freeze; a different HEAD is rejected even when every required-source hash is
unchanged. Unity XML is also rejected when a root or suite failure summary is
nonzero, when a suite reports a failed result, or when any descendant test case
is failed. Known-center aperture metrics are independently recalculated from
raw coverage buffers or decoded shader PNGs. Final-close selectors are derived
from decoded frame pixels and adjacent-frame deltas, and Player captures are
bound to the exact labels and directory for each resolution/FPS/focus cell.
