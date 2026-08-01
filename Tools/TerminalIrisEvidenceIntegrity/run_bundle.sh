#!/bin/bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

if [ "$#" -ne 1 ] || [ "$1" != "--produce" ]; then
    echo "Usage: Tools/TerminalIrisEvidenceIntegrity/run_bundle.sh --produce" >&2
    exit 2
fi

exec python3 "$script_dir/produce_bundle.py" --produce
