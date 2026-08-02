#!/usr/bin/env bash
set -euo pipefail
export PATH="$HOME/.local/bin:$PATH"

gh run list --repo Reblayzer/bunkerflow --limit 1 \
  --json databaseId,status,conclusion,url \
  --template '{{range .}}run {{.databaseId}} {{.status}} {{.conclusion}}{{"\n"}}{{.url}}{{"\n"}}{{end}}'

echo "--- jobs ---"
run_id=$(gh run list --repo Reblayzer/bunkerflow --limit 1 --json databaseId -q '.[0].databaseId')
gh run view "$run_id" --repo Reblayzer/bunkerflow \
  --json jobs --template '{{range .jobs}}{{.name}}: {{.conclusion}}{{"\n"}}{{end}}'
