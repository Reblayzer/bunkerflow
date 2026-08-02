#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
export PATH="$HOME/.local/bin:$PATH"

git add src tests scripts .github docker-compose.yml README.md BunkerFlow.slnx

git -c user.name="Alexandro Bolfa" -c user.email="contact@alexandro-bolfa.com" commit -q -m "feat(api): require an API key, and prove the Kafka offset rules against a real broker

Two gaps closed.

The ingest and query endpoints were unauthenticated. They now require an API
key, compared in constant time so response timing cannot be used to guess it a
byte at a time, and configured as a list so a key can be rotated without a
window where every caller is broken. Health and metrics stay open for probes
and scraping, and the simulated source endpoints stay open because they stand
in for external systems.

The offset-commit behaviour was reasoned about but never observed, because the
consumer loop needs a live broker. A new Testcontainers-backed suite drives
KafkaIngestionWorker against a real Redpanda and asserts on the committed
offset: a record that failed to publish leaves it uncommitted and is
redelivered when the publisher recovers, while a record rejected on data
quality is committed and dead-lettered instead. CI runs it as its own job."

git push origin main
git log --oneline -1
