# Lane A Full Baseline Refreeze 2026-04-22

## Scope
- same-revision full baseline refreeze for Lane A live row import only
- no stage-content canonical-path reopening
- no Lane B manual smoke claim

## Executed Commands
- `./run_tests.sh full`

## Artifact List With Exact Dates
- `TestResults/wsl-unity-full-editmode.xml` (2026-04-22 20:43:27 KST)
- `TestResults/wsl-unity-full-editmode.log` (2026-04-22 20:43:32 KST)
- `Docs/Testing/Remaining-56-Lane-Reclassification-2026-04-18.md` (historical comparison only)

## Result Summary
- same-revision full XML was re-frozen as the live Lane A oracle for this revision
- current result: `1248 total / 75 failed / 1172 passed`
- live row ledger import target: `75` failed rows

## Imported Ledger Summary
- revision: `534e861`
- reviewed at: `2026-04-22 20:43:49 KST`
- owner-lane counts:
- `A4`: `25`
- `A1`: `23`
- `A2`: `20`
- `Lane E`: `4`
- `Lane D`: `3`

## Explicit Non-Claims
- this note does not claim `full-lane baseline recovered`
- this note does not claim any broader recovery beyond this baseline refreeze note
- this note does not reuse historical artifacts as same-revision recovery evidence

