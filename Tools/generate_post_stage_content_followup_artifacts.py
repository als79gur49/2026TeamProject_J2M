#!/usr/bin/env python3
from __future__ import annotations

import argparse
import re
import subprocess
import textwrap
import xml.etree.ElementTree as ET
from collections import Counter
from datetime import datetime
from pathlib import Path


LANE_B_KEYWORDS = (
    "directplay",
    "defaultstageid",
    "launcher",
    "launch current scene",
    "replay last stage-backed scene",
    "scene.default-stage-id",
    "scene.direct-play",
    "stageeditordirectplay",
)
LANE_D_KEYWORDS = (
    "audio",
    "bgm",
    "globalaudioflowroot",
    "persistentroot",
    "same-root audio",
)
LANE_E_KEYWORDS = (
    "occupancy",
    "terrain",
    "reservation",
    "legality",
    "settlement",
    "traverse",
    "traversal",
)
LANE_F_KEYWORDS = (
    "governance",
    "documentation",
    "reporting",
    "close note",
    "claim vocabulary",
    "doc test",
    "bounded lane",
)
DISALLOWED_PHRASES = (
    "full-lane green",
    "all regressions are closed",
    "full regression is closed",
    "project-wide green",
)
ALLOWED_SPOT_CHECKS = (
    "same revision",
    "same execution window",
    "Open Functional Backlog / Handoff",
    "Counter Summary",
)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", default=".")
    parser.add_argument("--xml-path", default="TestResults/wsl-unity-full-editmode.xml")
    parser.add_argument("--log-path", default="TestResults/wsl-unity-full-editmode.log")
    parser.add_argument(
        "--historical-doc",
        default="Docs/Testing/Remaining-56-Lane-Reclassification-2026-04-18.md",
    )
    parser.add_argument("--output-dir", default="Docs/Testing")
    parser.add_argument("--full-command", default="./run_tests.sh full")
    return parser.parse_args()


def repo_relative(path: Path, root: Path) -> str:
    return path.resolve().relative_to(root.resolve()).as_posix()


def file_timestamp(path: Path) -> str:
    return datetime.fromtimestamp(path.stat().st_mtime).astimezone().strftime("%Y-%m-%d %H:%M:%S %Z")


def now_timestamp() -> str:
    return datetime.now().astimezone().strftime("%Y-%m-%d %H:%M:%S %Z")


def git_revision(root: Path) -> str:
    result = subprocess.run(
        ["git", "rev-parse", "--short", "HEAD"],
        cwd=root,
        check=True,
        capture_output=True,
        text=True,
    )
    return result.stdout.strip()


def parse_xml_failures(xml_path: Path) -> tuple[dict[str, str], list[dict[str, str]]]:
    root = ET.parse(xml_path).getroot()
    metadata = {
        "total": root.attrib.get("total", "0"),
        "failed": root.attrib.get("failed", "0"),
        "passed": root.attrib.get("passed", "0"),
        "start-time": root.attrib.get("start-time", ""),
        "end-time": root.attrib.get("end-time", ""),
        "duration": root.attrib.get("duration", ""),
    }
    failures: list[dict[str, str]] = []
    for case in root.iter("test-case"):
        if case.attrib.get("result") != "Failed":
            continue
        name = case.attrib.get("fullname") or case.attrib.get("name") or "<unknown>"
        message_node = case.find("./failure/message")
        message = ""
        if message_node is not None and message_node.text:
            message = message_node.text.strip().splitlines()[0].strip()
        failures.append({"name": name, "message": message})
    return metadata, failures


def parse_historical_lane_map(doc_path: Path) -> dict[str, str]:
    current_lane = None
    mapping: dict[str, str] = {}
    lane_header = re.compile(r"^### `(.+?)`$")
    row_line = re.compile(r"^- `(.+?)`$")
    for raw_line in doc_path.read_text(encoding="utf-8").splitlines():
        line = raw_line.strip()
        lane_match = lane_header.match(line)
        if lane_match:
            current_lane = lane_match.group(1)
            continue
        row_match = row_line.match(line)
        if row_match and current_lane:
            mapping[row_match.group(1)] = current_lane
    return mapping


def short_message(message: str) -> str:
    trimmed = " ".join(message.split())
    if len(trimmed) <= 160:
        return trimmed
    return trimmed[:157] + "..."


def escape_cell(text: str) -> str:
    return (text or "").replace("|", "\\|").replace("\n", " ").strip()


def keyword_hit(name: str, message: str, keywords: tuple[str, ...]) -> bool:
    haystack = f"{name} {message}".lower()
    return any(keyword in haystack for keyword in keywords)


def classify_row(
    failure: dict[str, str],
    historical_lane: str | None,
    source_artifact: str,
    reviewed_at: str,
) -> dict[str, str]:
    name = failure["name"]
    message = failure["message"]
    row: dict[str, str] = {
        "row id/test name": name,
        "classification date": reviewed_at.split()[0],
        "source artifact": source_artifact,
        "last reviewed at": reviewed_at,
        "failure shape summary": short_message(message) or "missing failure message",
        "linked historical row": historical_lane or "",
        "confirming artifact": "",
        "handoff target": "",
    }

    if keyword_hit(name, message, LANE_F_KEYWORDS):
        row.update(
            {
                "status": "blocked",
                "first wrong oracle": "governance / close-note / claim vocabulary surface",
                "owner lane": "Lane F",
                "current owner": "Lane A triage owner",
                "blocking claim": "Lane F governance hygiene close",
                "required evidence": "same-revision doc test or grep/audit artifact proving the governance mismatch",
                "next action": "Create handoff row and request Lane F acceptance.",
                "rationale summary": "The failure surface points at governance/reporting vocabulary rather than runtime behavior.",
                "classification confidence": "Medium",
                "handoff target": "Lane F",
            }
        )
        return row

    if keyword_hit(name, message, LANE_B_KEYWORDS):
        row.update(
            {
                "status": "blocked",
                "first wrong oracle": "direct-play launcher / catalog coverage / plain Play unsupported interpretation",
                "owner lane": "Lane B",
                "current owner": "Lane A triage owner",
                "blocking claim": "Lane B hard adoption close",
                "required evidence": "same-revision launcher/cycle evidence or validator artifact proving the direct-play contract issue",
                "next action": "Create handoff row and request Lane B acceptance.",
                "rationale summary": "The failure surface belongs to launcher-only direct-play adoption rather than Lane A recovery.",
                "classification confidence": "Medium",
                "handoff target": "Lane B",
            }
        )
        return row

    if keyword_hit(name, message, LANE_D_KEYWORDS):
        row.update(
            {
                "status": "blocked",
                "first wrong oracle": "audio / BGM ownership or registry surface",
                "owner lane": "Lane D",
                "current owner": "Lane A triage owner",
                "blocking claim": "cross-lane blocking handoff queue",
                "required evidence": "same-revision row plus failure capture showing audio/BGM ownership or registry scope",
                "next action": "Create handoff row and request Lane D acceptance.",
                "rationale summary": "Audio/BGM rows are deferred to the dedicated ADR-gated lane.",
                "classification confidence": "Medium",
                "handoff target": "Lane D",
            }
        )
        return row

    if keyword_hit(name, message, LANE_E_KEYWORDS):
        row.update(
            {
                "status": "blocked",
                "first wrong oracle": "terrain / occupancy / reservation / legality surface",
                "owner lane": "Lane E",
                "current owner": "Lane A triage owner",
                "blocking claim": "cross-lane blocking handoff queue",
                "required evidence": "same-revision row plus failure capture showing terrain or occupancy semantics scope",
                "next action": "Create handoff row and request Lane E acceptance.",
                "rationale summary": "Terrain and occupancy semantics remain deferred behind the dedicated gate ADR.",
                "classification confidence": "Medium",
                "handoff target": "Lane E",
            }
        )
        return row

    if historical_lane in {"consumer/view", "topology/view/post-fx"}:
        row.update(
            {
                "status": "active",
                "first wrong oracle": "host/view/bootstrap adjacency",
                "owner lane": "A2",
                "current owner": "Lane A triage owner",
                "blocking claim": "full-lane baseline recovered",
                "required evidence": "same-revision first-wrong-oracle capture at host/view/bootstrap or scene composition surface",
                "next action": "Run a targeted reproducer to confirm the first wrong oracle on the host/view/bootstrap boundary.",
                "rationale summary": f"Historical lane map previously locked this row to `{historical_lane}` and it stays nearest to A2.",
                "classification confidence": "Medium",
            }
        )
        return row

    if historical_lane:
        row.update(
            {
                "status": "active",
                "first wrong oracle": "historical continuation carryover",
                "owner lane": "A4",
                "current owner": "Lane A triage owner",
                "blocking claim": "full-lane baseline recovered",
                "required evidence": "same-revision carryover confirmation plus updated rationale against the current XML and message shape",
                "next action": "Confirm carryover status against the current XML and keep the row in the live continuation ledger.",
                "rationale summary": f"Historical lane map previously locked this row to `{historical_lane}` and no same-revision evidence has moved it yet.",
                "classification confidence": "Medium",
            }
        )
        return row

    row.update(
        {
            "status": "triaged",
            "first wrong oracle": "newness / failure-shape change",
            "owner lane": "A1",
            "current owner": "Lane A triage owner",
            "blocking claim": "full-lane baseline recovered",
            "required evidence": "same-revision targeted reproducer or one bounded confirm rerun to prove the new failure shape",
            "next action": "Isolate as A1 provisional and run one bounded same-revision recheck.",
            "rationale summary": "The row was not found in the 2026-04-18 pinned lane split, so it stays in A1 provisional until rechecked.",
            "classification confidence": "Low",
        }
    )
    return row


def summarize_counter(rows: list[dict[str, str]], key: str) -> list[tuple[str, int]]:
    counter = Counter(row[key] for row in rows if row.get(key))
    return sorted(counter.items(), key=lambda item: (-item[1], item[0]))


def markdown_table(headers: list[str], rows: list[list[str]]) -> str:
    header_row = "| " + " | ".join(headers) + " |"
    separator = "| " + " | ".join(["---"] * len(headers)) + " |"
    body = ["| " + " | ".join(escape_cell(cell) for cell in row) + " |" for row in rows]
    return "\n".join([header_row, separator, *body])


def render_baseline_note(
    note_path: Path,
    xml_rel: str,
    log_rel: str,
    xml_stamp: str,
    log_stamp: str,
    reviewed_at: str,
    revision: str,
    metadata: dict[str, str],
    rows: list[dict[str, str]],
    historical_doc_rel: str,
    full_command: str,
) -> None:
    owner_counts = summarize_counter(rows, "owner lane")
    owner_lines = "\n".join(f"- `{lane}`: `{count}`" for lane, count in owner_counts)
    content = f"""# Lane A Full Baseline Refreeze {reviewed_at.split()[0]}

## Scope
- same-revision full baseline refreeze for Lane A live row import only
- no stage-content canonical-path reopening
- no Lane B manual smoke claim

## Executed Commands
- `{full_command}`

## Artifact List With Exact Dates
- `{xml_rel}` ({xml_stamp})
- `{log_rel}` ({log_stamp})
- `{historical_doc_rel}` (historical comparison only)

## Result Summary
- same-revision full XML was re-frozen as the live Lane A oracle for this revision
- current result: `{metadata['total']} total / {metadata['failed']} failed / {metadata['passed']} passed`
- live row ledger import target: `{len(rows)}` failed rows

## Imported Ledger Summary
- revision: `{revision}`
- reviewed at: `{reviewed_at}`
- owner-lane counts:
{owner_lines}

## Explicit Non-Claims
- this note does not claim `full-lane baseline recovered`
- this note does not claim any broader recovery beyond this baseline refreeze note
- this note does not reuse historical artifacts as same-revision recovery evidence
"""
    note_path.write_text(content + "\n", encoding="utf-8")


def render_row_ledger(
    ledger_path: Path,
    reviewed_at: str,
    revision: str,
    xml_rel: str,
    rows: list[dict[str, str]],
) -> None:
    status_counts = summarize_counter(rows, "status")
    owner_counts = summarize_counter(rows, "owner lane")
    confidence_counts = summarize_counter(rows, "classification confidence")
    handoff_counts = summarize_counter(rows, "handoff target")
    status_lines = "\n".join(f"- `{key}`: `{count}`" for key, count in status_counts)
    owner_lines = "\n".join(f"- `{key}`: `{count}`" for key, count in owner_counts)
    confidence_lines = "\n".join(f"- `{key}`: `{count}`" for key, count in confidence_counts)
    handoff_lines = "\n".join(f"- `{key}`: `{count}`" for key, count in handoff_counts) or "- none"
    active_rows = [row for row in rows if row["status"] == "active"][:10]
    active_lines = "\n".join(
        f"- `{row['row id/test name']}` -> `{row['owner lane']}` / `{row['next action']}`"
        for row in active_rows
    ) or "- no active rows yet"
    headers = [
        "row id/test name",
        "status",
        "classification date",
        "source artifact",
        "first wrong oracle",
        "owner lane",
        "current owner",
        "blocking claim",
        "required evidence",
        "next action",
        "last reviewed at",
        "rationale summary",
        "handoff target",
        "classification confidence",
        "failure shape summary",
        "linked historical row",
        "confirming artifact",
    ]
    table_rows = [[row.get(header, "") for header in headers] for row in rows]
    content = f"""# Lane A Live Row Ledger {reviewed_at.split()[0]}

## Scope
- same-revision full XML import for Lane A live triage only
- row owner lock and handoff proposal tracking

## Source Artifact
- revision: `{revision}`
- live oracle: `{xml_rel}`
- reviewed at: `{reviewed_at}`

## Summary
- imported rows: `{len(rows)}`
- status counts:
{status_lines}
- owner-lane counts:
{owner_lines}
- classification confidence counts:
{confidence_lines}
- handoff target counts:
{handoff_lines}

## First Active Queue
{active_lines}

## Rows
{markdown_table(headers, table_rows)}
"""
    ledger_path.write_text(content + "\n", encoding="utf-8")


def render_handoff_ledger(
    handoff_path: Path,
    reviewed_at: str,
    revision: str,
    source_artifact: str,
    rows: list[dict[str, str]],
) -> None:
    handoff_rows = [row for row in rows if row.get("handoff target")]
    headers = [
        "source lane",
        "target lane",
        "row id/test name",
        "reason",
        "blocking claim",
        "required evidence",
        "created at",
        "accepted by",
        "status",
        "source artifact",
        "historical comparison artifact",
    ]
    table_rows = []
    for row in handoff_rows:
        table_rows.append(
            [
                row.get("owner lane", "Lane A"),
                row["handoff target"],
                row["row id/test name"],
                row["rationale summary"],
                row["blocking claim"],
                row["required evidence"],
                reviewed_at,
                "pending",
                "pending-acceptance",
                source_artifact,
                row.get("linked historical row", ""),
            ]
        )

    target_counts = summarize_counter(
        [{"target": row["handoff target"]} for row in handoff_rows],
        "target",
    )
    target_lines = "\n".join(f"- `{target}`: `{count}`" for target, count in target_counts) or "- none"
    content = f"""# Lane A Handoff Ledger {reviewed_at.split()[0]}

## Scope
- current same-revision handoff proposals created from the live Lane A row import

## Source Artifact
- revision: `{revision}`
- source artifact: `{source_artifact}`
- created at: `{reviewed_at}`

## Summary
- handoff rows: `{len(handoff_rows)}`
- target counts:
{target_lines}

## Rows
{markdown_table(headers, table_rows)}
"""
    handoff_path.write_text(content + "\n", encoding="utf-8")


def render_audit_artifact(
    audit_path: Path,
    reviewed_at: str,
    revision: str,
    searched_paths: list[Path],
    root: Path,
) -> None:
    rel_paths = [repo_relative(path, root) for path in searched_paths]
    disallowed_rows: list[list[str]] = []
    for phrase in DISALLOWED_PHRASES:
        count = 0
        hits: list[str] = []
        for path in searched_paths:
            text = path.read_text(encoding="utf-8")
            path_hits = text.count(phrase)
            count += path_hits
            if path_hits:
                hits.append(f"{repo_relative(path, root)} ({path_hits})")
        disallowed_rows.append([phrase, str(count), ", ".join(hits) or "none"])

    allowed_rows = []
    for phrase in ALLOWED_SPOT_CHECKS:
        hits = [repo_relative(path, root) for path in searched_paths if phrase in path.read_text(encoding="utf-8")]
        allowed_rows.append([phrase, str(len(hits)), ", ".join(hits) or "none"])

    content = f"""# Lane F Claim Vocabulary Audit {reviewed_at.split()[0]}

## Scope
- claim vocabulary grep/audit for produced post-stage-content follow-up notes and templates

## Searched Paths
- searched paths listed below
""" + "\n".join(f"- `{path}`" for path in rel_paths) + f"""

## Disallowed Phrase Matches
- disallowed phrases are recorded with exact match count and path summary
{markdown_table(["phrase", "match count", "paths"], disallowed_rows)}

## Allowed Phrase Spot-Check
- allowed phrase spot-check is recorded below
{markdown_table(["phrase", "match count", "paths"], allowed_rows)}

## Audit Metadata
- revision: `{revision}`
- date/time: `{reviewed_at}`
"""
    audit_path.write_text(content + "\n", encoding="utf-8")


def main() -> None:
    args = parse_args()
    root = Path(args.root).resolve()
    output_dir = (root / args.output_dir).resolve()
    output_dir.mkdir(parents=True, exist_ok=True)

    xml_path = (root / args.xml_path).resolve()
    log_path = (root / args.log_path).resolve()
    historical_doc = (root / args.historical_doc).resolve()
    reviewed_at = now_timestamp()
    revision = git_revision(root)
    metadata, failures = parse_xml_failures(xml_path)
    historical_lane_map = parse_historical_lane_map(historical_doc)
    source_artifact = f"{repo_relative(xml_path, root)} ({file_timestamp(xml_path)})"

    rows = []
    for failure in failures:
        historical_lane = historical_lane_map.get(failure["name"])
        rows.append(classify_row(failure, historical_lane, source_artifact, reviewed_at))

    rows.sort(key=lambda row: (row["owner lane"], row["status"], row["row id/test name"]))

    date_str = reviewed_at.split()[0]
    baseline_path = output_dir / f"Lane-A-Full-Baseline-Refreeze-{date_str}.md"
    row_ledger_path = output_dir / f"Lane-A-Live-Row-Ledger-{date_str}.md"
    handoff_path = output_dir / f"Lane-A-Handoff-Ledger-{date_str}.md"
    audit_path = output_dir / f"Lane-F-Claim-Vocabulary-Audit-{date_str}.md"

    render_baseline_note(
        baseline_path,
        repo_relative(xml_path, root),
        repo_relative(log_path, root),
        file_timestamp(xml_path),
        file_timestamp(log_path),
        reviewed_at,
        revision,
        metadata,
        rows,
        repo_relative(historical_doc, root),
        args.full_command,
    )
    render_row_ledger(
        row_ledger_path,
        reviewed_at,
        revision,
        repo_relative(xml_path, root),
        rows,
    )
    render_handoff_ledger(
        handoff_path,
        reviewed_at,
        revision,
        source_artifact,
        rows,
    )
    render_audit_artifact(
        audit_path,
        reviewed_at,
        revision,
        [
            root / "Docs/Testing/Stage-Editor-Direct-Play-Smoke-Cycle-Template.md",
            baseline_path,
            row_ledger_path,
            handoff_path,
        ],
        root,
    )


if __name__ == "__main__":
    main()
