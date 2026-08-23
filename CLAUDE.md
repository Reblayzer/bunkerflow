# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Integrity rules (from PROJECT_BRAINSTORM.md — do not cross)

This project is deliberately honest about its own boundaries, and that constraint applies to
anything written about it — README text, docs, commit messages, diagrams:

- Describe behaviour and stack only. **No invented metrics, users, or "deployed in production" claims.**
- The source systems are **simulated** (`/mock-sources/*`), and the README says so. Keep it that way.
- Databricks / Microsoft Fabric are the **documented** production target, not a built-and-deployed claim.
- Terraform was applied to a real subscription once to prove it, then destroyed. Nothing runs in Azure now.
- Any figure quoted in docs must be reproducible — `scripts/sample-aggregates.sh` exists so the
  README's gold-table numbers can be checked rather than taken on trust.

## Commands

Scripts source `scripts/env.sh`, which sets `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1`. Source it
before bare `dotnet` commands, or culture-sensitive parsing (the ERP feed's comma decimals) behaves
differently than it does in CI.

```bash
source scripts/env.sh

./scripts/test.sh           # fast suite: builds with -warnaserror, then in-process tests. No containers.
./scripts/test-broker.sh    # Kafka offset tests against a real Redpanda via Testcontainers. Needs Docker.
./scripts/smoke.sh          # end-to-end demo in loopback mode — no Docker, no infrastructure
./scripts/sample-aggregates.sh   # recompute the README's gold figures from samples/landing
./scripts/inspect-parquet.sh     # print the landed Parquet schema
./scripts/tf.sh apply -var location=northeurope   # Terraform against a real Azure subscription

docker compose up --build   # Postgres, Redpanda, Service Bus emulator, API, worker
./scripts/seed-kafka.sh     # put four trades on the Kafka topic (exercises the streaming path)
```

Run a single test:

```bash
dotnet test tests/BunkerFlow.Integration.Tests/BunkerFlow.Integration.Tests.csproj \
  --filter "FullyQualifiedName~Should_release_the_business_key_when_publishing_never_succeeded"
```

## Build enforcement

`Directory.Build.props` sets `TreatWarningsAsErrors` and `EnforceCodeStyleInBuild`. In
`.editorconfig`, `IDE1006` plus four naming rules sit at `warning` severity, so **a naming-convention
violation is a build failure**: private fields `_camelCase`, constants and statics PascalCase, types
and members PascalCase. Formatting and whitespace rules are *not* build-enforced (`IDE0005` is only a
suggestion), so a stray blank line will not break the build.

## Architecture

Three ingestion channels — a scheduled REST puller, a Kafka consumer, and `POST /ingest` — all call
the same `IngestionPipeline.ProcessAsync`. That convergence is the whole design: adding a source is
an adapter plus alias entries in `BunkerTradeNormalizer`, never a new code path.

The pipeline runs **normalize → validate → claim key → publish**, then lands via Service Bus:

- `BunkerTradeNormalizer` maps each source's own field names through eight alias arrays onto
  `IntegrationEvent`, the single contract everything downstream uses. It is the most connected type
  in the codebase; nothing past the normalizer knows which system a record came from.
- `ServiceBusLandingWorker` consumes the subscription and writes **twice**: a Postgres row for
  `GET /events`, and buffered Parquet partitioned by trade date for the lakehouse.
- `notebooks/bunkerflow_lakehouse.py` builds bronze/silver/gold Delta tables over that Parquet,
  reading the committed sample in `samples/landing/` so it runs with no setup.

Projects: `Contracts` (the event contract, no dependencies) ← `Integration` (normalization,
validation, dedupe, retry, messaging, landing) ← `Api` and `Worker`.

## Invariants — break these and trades are silently lost

These are the subtle rules the tests exist to pin. Changing `IngestionPipeline.cs` or
`KafkaIngestionWorker.cs` means re-reading them first.

1. **Reserve-then-release, not mark-as-seen.** The dedupe store claims the business key *before*
   publishing and releases it if publishing fails. Marking a key seen up front makes a retry of a
   failed publish look like a duplicate, and the trade disappears.
   Pinned by `Should_release_the_business_key_when_publishing_never_succeeded`.

2. **A failed publish must NOT commit the Kafka offset.** The consumer seeks back so the broker
   redelivers once infrastructure recovers. A record *rejected on data quality* is the opposite case
   and **is** committed — resending bad data would only fail again — and goes to the dead-letter
   queue. Both pinned by `KafkaOffsetCommitTests` against a real broker, which is why that suite
   needs Docker and runs as its own CI job.

3. **Only `TransientPublishException` is retried.** A missing topic or oversized payload is
   `PermanentPublishException` and fails on the first attempt rather than spending the retry budget.

4. **Deterministic event ids.** `EventId` is a SHA-256 of `sourceSystem:sourceRecordId`, so a replay
   produces the same id and Service Bus duplicate detection can reject it before any consumer sees
   it. The dedupe store is the durable backstop, the broker is the second guard.

5. **`/health/live` must not touch dependencies.** Tying liveness to the database would make an
   orchestrator restart a healthy pod during a database blip. `/health/ready` is the one that checks.

6. **Validation reports every failure, not the first**, so a source system fixing one field at a time
   is not stuck in a slow loop.

## Dead-lettering

Two paths, deliberately kept apart: records rejected *before* publication go to the
`bunkerflow-deadletter` queue via `ServiceBusDeadLetterSink`; messages that fail *after* delivery go
to the subscription's own DLQ. "Bad data" and "broken plumbing" are different incidents.

## Credentials

Service Bus uses three separate SAS credentials — send, listen, dead-letter — none with Manage.
Applying the real Terraform forced this: per-entity rules produce entity-scoped connection strings,
so the single connection string that works against the emulator does not work against Azure.
See `docs/azure-verification.md`.
