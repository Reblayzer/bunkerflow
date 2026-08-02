#!/usr/bin/env bash
# Run this after: gh auth refresh -s workflow
# The repo already exists and the remote is already configured.
set -euo pipefail
cd "$(dirname "$0")/.."
export PATH="$HOME/.local/bin:$PATH"

git push -u origin main

echo "--- CI ---"
gh run list --limit 3
echo
echo "Watch it with: gh run watch"
