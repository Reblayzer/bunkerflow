#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
export PATH="$HOME/.local/bin:$PATH"

git add tests/BunkerFlow.Integration.Tests/ParquetLandingWriterTests.cs scripts

git -c user.name="Alexandro Bolfa" -c user.email="contact@alexandro-bolfa.com" commit -q -m "test(landing): read the Parquet back instead of trusting the write

Writing a file no reader can open is a silent failure: the pipeline reports
success and the lakehouse gets nothing usable. These tests round-trip through
ParquetSerializer and assert the values, the date partitioning and the
batch size."

git push origin main
git log --oneline -1
