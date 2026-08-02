#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
export PATH="$HOME/.local/bin:$PATH"

gh repo create bunkerflow \
  --public \
  --source=. \
  --remote=origin \
  --push \
  --description "Event-driven integration gateway for bunker trade data: batch and streaming ingestion, Azure Service Bus routing with dead-lettering, Postgres and Parquet landing. .NET 10, Terraform, GitHub Actions."

gh repo view --json url,visibility,name -q '.name + " | " + .visibility + " | " + .url'
