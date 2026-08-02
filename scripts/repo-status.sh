#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
export PATH="$HOME/.local/bin:$PATH"

gh repo view Reblayzer/bunkerflow --json name,visibility,url,isEmpty
echo "--- remote ---"
git remote -v | head -2
echo "--- unpushed commits ---"
git log --oneline
