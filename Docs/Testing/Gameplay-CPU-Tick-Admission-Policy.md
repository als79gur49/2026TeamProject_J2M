# CPU/Tick performance admission policy

`./run_tests.sh gameplay-performance` defaults to `strict-v1`. Set `GAMEPLAY_PERFORMANCE_ADMISSION_POLICY=cpu-tick-v1` explicitly for a CPU/Tick-primary campaign. Unknown policies fail closed. Historical strict reports retain their original format and meaning; new CPU-primary reports record `admissionPolicy`, `primaryVerdict` and phase-local `gpuCoverage`.

Both policies require valid schema, revision/source/artifact identities, settings, runtime success, exact frame/CPU-main counts and attempted/executed/Tick-summary counts. GPU summary domains, finite ordered values and count consistency remain mandatory. CPU-primary only separates coverage: N valid samples is complete, 0<n<N is partial, zero samples is unavailable with all-zero summary. A count outside 0..N, boolean/noninteger count, malformed summary, NaN/Infinity remains a failure. Coverage is not proof of distinct timestamp/phase-correct GPU frames.

GPU partial/unavailable does not reject a CPU-primary run and never causes a CPU campaign retry. Do not use incomplete GPU values for GPU performance/nonregression claims. CPU-render and other optional counter contracts are unchanged. The Player workload and probe remain unchanged; do not extend gameplay until GPU samples fill.

The policy is recorded in both preflight and artifact KV manifests. CLI policy must match both. Canonical final-manifest and terminal validation recompute the report using that policy. Policy, runner and validator hashes must match across A/B. Absent policy in legacy context means strict-v1; partial/mismatched policy records fail closed.

For bundle measurement, use a new cohort and freeze revisions, stage, policy, settings and ordered slots before reading speed values. Take the first primary-valid attempt per slot, regardless of GPU coverage or performance. Preserve all failed attempts and historical reports; do not relabel retries as extra slots. Existing +/-5% decision bands, >10% spread gate, serial A-B-B-A order and one optional B-A-A-B block remain. Post-hoc old-evidence analysis is exploratory only.

Tool validation covers strict N-1 rejection, GPU complete/partial/unavailable, malformed data, mandatory CPU/Tick failures, CLI-context binding, canonical report validation and input immutability. This tooling change does not change product runtime/UI/assets or establish project-wide regression status.
