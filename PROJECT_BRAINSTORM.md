# BunkerFlow — tailored project brief (Unit IT, Integration Engineer)

**One-line pitch:** An event-driven integration gateway that ingests data from multiple
business systems, both as scheduled batch pulls and as real-time streams, routes it through
Azure Service Bus and Kafka, normalizes it, and lands it in a lakehouse-style store, all
exposed and monitored over a REST API.

This is the integration layer of a "Unified Data Platform" in miniature, which is exactly the
shape of work the Unit IT posting describes (building integration capabilities for the Bunker
Holding Group Unified Data Platform).

## Posting technologies it demonstrates

- **Azure Service Bus** — topic/queue routing of integration events, dead-letter handling.
- **Kafka** — real-time streaming ingestion topic, idempotent consumer.
- **Event-driven architecture** — producers/consumers, message contracts, fan-out.
- **Batch and real-time streaming ingestion pipelines** — a scheduled batch puller and a Kafka stream consumer feeding the same normalization layer.
- **APIs / REST** — ingestion + query endpoints, OpenAPI documented.
- **Infrastructure as Code / Terraform** — provisions the Azure Service Bus namespace, topics, subscriptions, and queues.
- **CI/CD** — GitHub Actions: build, unit tests, `terraform validate`, container build.
- **Observability / reliability** — structured logging, `/health` and `/metrics` endpoints, retry with backoff, dead-letter inspection.
- **Distributed systems / reusable integration patterns** — idempotent consumer, dead-letter queue, schema validation, retry/backoff written as reusable building blocks.
- **Databricks / Microsoft Fabric** — documented as the production landing target; v1 lands to a local Delta/Parquet-style store (Databricks/Fabric is the stated next step, not claimed as built).

## Architecture sketch

```
 mock business systems
   |                 \
   | (batch: scheduled REST pull)   (streaming: Kafka topic)
   v                                    v
 +-------------------- Ingestion workers --------------------+
 |  - normalize to a common event contract                   |
 |  - schema validation + data-quality checks                |
 |  - idempotent (dedupe on business key)                    |
 +-----------------------------+------------------------------+
                               | publish
                               v
                   Azure Service Bus topic
                   (subscriptions per consumer,
                    dead-letter queue on failure)
                               |
                               v
            Landing writer  ->  lakehouse-style store
            (Delta/Parquet locally; Databricks / Microsoft
             Fabric documented as the production target)
                               ^
 REST API (ingest + query + health/metrics) --------- observability
```

## Stack

- **.NET 8 / C#** minimal API gateway and worker (Azure Service Bus has a first-class .NET SDK; matches Alex's Schneider .NET experience).
- **Azure Service Bus** (real namespace on a free tier, or the local emulator) for routing and dead-lettering.
- **Kafka** via Docker (Redpanda or Confluent) for the streaming path.
- **Terraform** provisions the Service Bus namespace, topics, subscriptions, queues.
- **PostgreSQL + Parquet/Delta** local landing store; Databricks / Microsoft Fabric documented as the production target.
- **GitHub Actions** CI: build, xUnit tests, `terraform validate`, Docker build.
- **Docker Compose** to run the whole thing locally with one command.
- Built with **Claude Code** (agentic, custom skills, MCP), consistent with Alex's AI-native workflow.

## v1 scope

**In:**
- One batch source (scheduled pull from a mock REST endpoint) and one streaming source (Kafka topic).
- Common event contract + schema validation + dedupe (idempotent consumer).
- Azure Service Bus topic with one subscription and a dead-letter path.
- Landing writer to local Delta/Parquet + Postgres.
- REST API: `POST /ingest`, `GET /events`, `GET /health`, `GET /metrics`.
- Terraform for the Service Bus resources; GitHub Actions CI green; docker compose up.
- README with the architecture diagram and an honest "simulated sources, Databricks/Fabric is the production target" note.

**Out (v1):**
- Real Databricks / Microsoft Fabric deployment (documented as the next step).
- Real business-system connectors (sources are mocked).
- Auth/RBAC beyond a basic API key.
- Horizontal scaling / multi-region.

## Build plan

1. Scaffold .NET solution (API + worker), Docker Compose with Kafka + Postgres, README skeleton.
2. Define the common event contract and the normalization + schema-validation + dedupe layer with xUnit tests.
3. Batch puller (scheduled) + Kafka streaming consumer, both feeding normalization.
4. Wire Azure Service Bus publish/subscribe + dead-letter handling.
5. Landing writer (Parquet/Delta + Postgres) and the query/health/metrics endpoints.
6. Terraform for the Service Bus namespace/topics/subscriptions; `terraform validate` in CI.
7. GitHub Actions: build, test, terraform validate, Docker build. Make CI green.
8. README: architecture diagram, run instructions, honest scope note, push public.

## Integrity notes (do not cross)

- Describe behaviour and stack only. No invented metrics, users, or "deployed in production".
- Sources are simulated and the README says so. Databricks / Microsoft Fabric are the documented
  production target, not a built-and-deployed claim.
- Add the public repo link to the CV only once the repo exists (same day Alex starts building).
