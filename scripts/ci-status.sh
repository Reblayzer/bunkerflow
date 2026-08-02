#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
export PATH="$HOME/.local/bin:$PATH"

gh run list --limit 5 --json databaseId,workflowName,status,conclusion,headSha,createdAt \
  --template '{{range .}}{{.databaseId}} {{.workflowName}} {{.status}} {{.conclusion}} {{slice .headSha 0 7}}{{"\n"}}{{end}}'
