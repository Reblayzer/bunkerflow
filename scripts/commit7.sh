#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
export PATH="$HOME/.local/bin:$PATH"

git add README.md docs scripts

git -c user.name="Alexandro Bolfa" -c user.email="contact@alexandro-bolfa.com" commit -q -m "fix(scripts): match Spark's rounding, and show the notebook output

The verification script rounded with C#'s default banker's rounding while
Spark's round() is half-up, so on an exact midpoint it reported 3190.4 where
the notebook showed 3190.5. Comparing the two is the whole point of the
script, so it now rounds half-up and the figures agree on every row.

Also embeds a screenshot of the gold table from a Free Edition run, so the
published numbers can be seen as the notebook actually rendered them."

git push origin main
git log --oneline -1
