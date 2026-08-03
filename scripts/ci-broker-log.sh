#!/usr/bin/env bash
set -euo pipefail
export PATH="$HOME/.local/bin:$PATH"

run_id=$(gh run list --repo Reblayzer/bunkerflow --limit 1 --json databaseId -q '.[0].databaseId')
gh run view "$run_id" --repo Reblayzer/bunkerflow --log --job \
  "$(gh run view "$run_id" --repo Reblayzer/bunkerflow --json jobs \
      -q '.jobs[] | select(.name=="Broker tests") | .databaseId')" \
  | grep -Ei "passed|failed|total|Redpanda|test run" | tail -12
