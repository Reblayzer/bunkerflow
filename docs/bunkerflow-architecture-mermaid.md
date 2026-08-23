# BunkerFlow — Architecture Diagrams

Generated with the `architecture-diagram` skill. Components and edges taken from the
graphify knowledge graph (734 nodes, 1,461 edges) plus the project files and DDL.

## System Architecture

```mermaid
graph TD
    subgraph sources["Source systems (simulated)"]
        TD[trading-desk<br/>REST, camelCase]
        ERP[erp<br/>REST, snake_case]
        PT[port-telemetry<br/>Kafka topic]
    end

    subgraph api["BunkerFlow.Api"]
        IE[IngestEndpoints<br/>POST /ingest]
        EE[EventEndpoints<br/>GET /events]
        OE[OperationalEndpoints<br/>/health /metrics]
        AK[ApiKeyEndpointFilter]
    end

    subgraph worker["BunkerFlow.Worker"]
        BW[BatchIngestionWorker]
        KW[KafkaIngestionWorker]
        LW[ServiceBusLandingWorker]
    end

    subgraph integration["BunkerFlow.Integration"]
        P[IngestionPipeline]
        N[BunkerTradeNormalizer]
        V[BunkerTradeValidator]
        D[PostgresDeduplicationStore]
        R[RetryPolicy]
        PUB[ServiceBusEventPublisher]
        DLS[ServiceBusDeadLetterSink]
        M[PipelineMetrics]
    end

    subgraph azure["Azure Service Bus"]
        T[[bunkerflow-events<br/>duplicate detection]]
        SUB[landing subscription<br/>schemaVersion = 1]
        DLQ[[bunkerflow-deadletter]]
    end

    subgraph landing["Landing"]
        PG[(PostgreSQL<br/>landed_events)]
        PQ[Parquet<br/>dt=YYYY-MM-DD]
    end

    DBX[Databricks<br/>bronze / silver / gold]

    TD --> BW
    ERP --> BW
    PT --> KW
    AK -.guards.-> IE
    AK -.guards.-> EE

    BW --> P
    KW --> P
    IE --> P

    P --> N
    P --> V
    P --> D
    P --> R
    R --> PUB
    P --> DLS
    P --> M

    PUB --> T
    DLS --> DLQ
    T --> SUB
    SUB --> LW
    LW --> PG
    LW --> PQ
    PQ --> DBX
    PG --> EE
    M --> OE
```

## Data Flow

```mermaid
sequenceDiagram
    participant S as Source system
    participant W as Ingestion worker
    participant P as IngestionPipeline
    participant DS as Dedupe store
    participant SB as Service Bus
    participant LW as Landing worker
    participant DB as Postgres + Parquet

    S->>W: raw record (own field names)
    W->>P: ProcessAsync(SourceRecord)
    P->>P: Normalize → IntegrationEvent
    alt normalization or validation fails
        P->>SB: dead-letter with reason
        P-->>W: Rejected
    else valid
        P->>DS: TryReserveAsync(businessKey)
        alt key already claimed
            DS-->>P: false
            P-->>W: Duplicate
        else claimed
            DS-->>P: true
            P->>SB: PublishAsync (MessageId = EventId)
            alt publish fails
                P->>DS: ReleaseAsync(key)
                P-->>W: Failed
                Note over W: Kafka: Seek(offset), do NOT commit
            else published
                P-->>W: Accepted
                Note over W: Kafka: Commit(offset)
                SB->>LW: deliver (schemaVersion = 1)
                LW->>DB: append row + buffer Parquet
            end
        end
    end
```

## Dependency Graph

```mermaid
graph LR
    Contracts[BunkerFlow.Contracts]
    Integration[BunkerFlow.Integration]
    Api[BunkerFlow.Api]
    Worker[BunkerFlow.Worker]
    ITests[BunkerFlow.Integration.Tests]
    BTests[BunkerFlow.Broker.Tests]

    Integration --> Contracts
    Api --> Integration
    Worker --> Integration
    ITests --> Integration
    ITests --> Api
    BTests --> Worker
```

## Database Schema

```mermaid
erDiagram
    landed_events {
        TEXT event_id PK
        TEXT event_type
        INTEGER schema_version
        TEXT source_system
        TEXT source_record_id
        TEXT channel
        TIMESTAMPTZ occurred_at_utc
        TIMESTAMPTZ ingested_at_utc
        TIMESTAMPTZ landed_at_utc
        TEXT trade_reference
        TEXT vessel_imo
        TEXT port
        TEXT product
        NUMERIC quantity_mt
        NUMERIC price_usd_per_mt
        TEXT counterparty
        TIMESTAMPTZ traded_at_utc
    }

    ingested_keys {
        TEXT deduplication_key PK
        TIMESTAMPTZ reserved_at_utc
    }

    landed_events ||--|| ingested_keys : "source_system:source_record_id"
```

`ingested_keys` has no foreign key to `landed_events` — the claim is taken *before*
publication and released if publishing fails, so a key can exist with no landed row.
The relationship is by convention on the business key, not enforced by the database.
