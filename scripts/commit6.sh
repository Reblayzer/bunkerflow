#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
export PATH="$HOME/.local/bin:$PATH"

git add README.md scripts

git -c user.name="Alexandro Bolfa" -c user.email="contact@alexandro-bolfa.com" commit -q -m "docs(lakehouse): publish the notebook's results and a way to check them

Puts the channel split and the gold aggregation in the README so a reader sees
what the pipeline produces without running anything, and adds
scripts/sample-aggregates.sh, which recomputes the same figures directly from
the sample Parquet so the published numbers can be verified rather than
trusted."

git push origin main
git log --oneline -1
