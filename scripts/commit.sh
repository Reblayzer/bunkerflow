#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."

git add \
  src/BunkerFlow.Integration/Messaging \
  src/BunkerFlow.Integration/Landing \
  src/BunkerFlow.Integration/Publishing \
  src/BunkerFlow.Integration/Idempotency \
  src/BunkerFlow.Integration/Composition \
  src/BunkerFlow.Integration/BunkerFlow.Integration.csproj \
  src/BunkerFlow.Api \
  src/BunkerFlow.Worker \
  tests/BunkerFlow.Integration.Tests \
  infra \
  .github \
  docker-compose.yml \
  README.md \
  scripts

git -c user.name="Alexandro Bolfa" -c user.email="contact@alexandro-bolfa.com" commit -q -m "feat(gateway): batch and streaming ingestion, Service Bus routing, landing and IaC

Batch puller and Kafka consumer feeding the shared pipeline, a Service Bus
publisher with deterministic message ids and dead-lettering, Postgres and
Parquet landing behind a repository interface, and a REST gateway with
ingest, query, health and Prometheus metrics endpoints.

Terraform provisions the namespace, topic, subscription, schema filter and
dead-letter queue with send-only auth rules. GitHub Actions runs build,
tests, terraform fmt and validate, and both container images.

61 tests, including the API driven in-process."

git log --oneline
