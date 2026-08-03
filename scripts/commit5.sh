#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
export PATH="$HOME/.local/bin:$PATH"

git add notebooks samples scripts README.md

git -c user.name="Alexandro Bolfa" -c user.email="contact@alexandro-bolfa.com" commit -q -m "feat(lakehouse): Databricks notebook over real landed Parquet

Adds a bronze/silver/gold notebook that turns the gateway's Parquet output
into Delta tables: bronze as landed with the dt partition picked up, silver
with snake_case columns, schema-version filtering and deduplication on the
derived event id, gold aggregated by port and fuel grade.

It reads samples/landing/, which is genuine output from a compose run: 50
events, 47 from the batch pullers and 3 from the Kafka topic, so the notebook
runs on a fresh Databricks Free Edition workspace with no cloud subscription
and nothing to upload.

Written against the real Parquet schema rather than an assumed one; the
gateway writes .NET property names, so columns arrive PascalCase and the
silver layer standardizes them to match the Postgres query store."

git push origin main
git log --oneline -1
