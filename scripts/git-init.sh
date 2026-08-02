#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."

git init -b main
git add .gitignore Directory.Build.props BunkerFlow.slnx PROJECT_BRAINSTORM.md scripts src tests
git -c user.name="Alexandro Bolfa" -c user.email="contact@alexandro-bolfa.com" commit -q -m "feat(pipeline): normalize, validate and dedupe bunker trade records

Common IntegrationEvent contract every source is mapped onto, a normalizer
that resolves source-specific field names through aliases, IMO check-digit
and data-quality validation, and an idempotent consumer that reserves the
business key before publishing and releases it again if the publish fails.

47 xUnit tests cover the happy paths, the rejection paths and the
reserve-then-release behaviour."

git log --oneline
