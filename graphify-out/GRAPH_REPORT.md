# Graph Report - bunkerflow  (2026-08-23)

## Corpus Check
- Corpus is ~23,876 words - fits in a single context window. You may not need a graph.

## Summary
- 734 nodes · 1461 edges · 25 communities (24 shown, 1 thin omitted)
- Extraction: 93% EXTRACTED · 7% INFERRED · 0% AMBIGUOUS · INFERRED: 102 edges (avg confidence: 0.83)
- Token cost: 12,000 input · 6,000 output

## Community Hubs (Navigation)
- Namespace Backbone
- REST API Surface
- Azure Infrastructure And Project Scope
- CI And Dead-Letter Path
- Validator And Pipeline Tests
- Local Stack And Auth Filter
- Kafka Offset Commit Tests
- Dependency Injection And Options
- Solution And NuGet Dependencies
- Parquet Landing Writer
- Metrics And Operational Endpoints
- Normalization And Field Aliases
- Event Repository And Query
- Event Endpoints And Publisher Contracts
- API End-To-End Tests
- Data Quality Validation
- Batch Puller And Mock Sources
- API Key Enforcement Tests
- Retry Policy And Backoff
- API Launch Profiles
- Lakehouse Notebook And Scripts
- Deduplication Store
- Worker Launch Profiles
- Integration Exception Types
- Broker Test Script

## God Nodes (most connected - your core abstractions)
1. `IntegrationEvent` - 44 edges
2. `BunkerFlow.Contracts` - 34 edges
3. `IngestionPipeline` - 22 edges
4. `ApiEndpointTests` - 21 edges
5. `SourceRecord` - 18 edges
6. `ServiceBusLandingWorker` - 18 edges
7. `ApiKeyTests` - 17 edges
8. `IngestionPipelineTests` - 17 edges
9. `BunkerTradeValidatorTests` - 16 edges
10. `BunkerFlow.Integration.Landing` - 15 edges

## Surprising Connections (you probably didn't know these)
- `Service Bus Emulator` --semantically_similar_to--> `azurerm_servicebus_namespace.bunkerflow`  [INFERRED] [semantically similar]
  docker-compose.yml → infra/terraform/main.tf
- `Shared API Key Authentication` --references--> `ApiKeyEndpointFilter`  [EXTRACTED]
  README.md → src/BunkerFlow.Api/Security/ApiKeyEndpointFilter.cs
- `PostgreSQL Service` --references--> `EventQuery`  [INFERRED]
  docker-compose.yml → src/BunkerFlow.Integration/Landing/EventQuery.cs
- `Bronze / Silver / Gold Layers` --shares_data_with--> `ParquetLandingWriter`  [INFERRED]
  README.md → src/BunkerFlow.Integration/Landing/ParquetLandingWriter.cs
- `Least Privilege Actually Holds` --references--> `ServiceBusDeadLetterSink`  [INFERRED]
  docs/azure-verification.md → src/BunkerFlow.Integration/Messaging/ServiceBusDeadLetterSink.cs

## Import Cycles
- None detected.

## Hyperedges (group relationships)
- **Ingest → Normalize → Validate → Dedupe → Publish → Land** — src_bunkerflow_worker_ingestion_batchingestionworker_bunkerflow_worker_ingestion_batchingestionworker, src_bunkerflow_worker_ingestion_kafkaingestionworker_bunkerflow_worker_ingestion_kafkaingestionworker, src_bunkerflow_integration_pipeline_ingestionpipeline_bunkerflow_integration_pipeline_ingestionpipeline, src_bunkerflow_integration_normalization_bunkertradenormalizer_bunkerflow_integration_normalization_bunkertradenormalizer, src_bunkerflow_integration_validation_bunkertradevalidator_bunkerflow_integration_validation_bunkertradevalidator, src_bunkerflow_integration_idempotency_postgresdeduplicationstore_bunkerflow_integration_idempotency_postgresdeduplicationstore, src_bunkerflow_integration_messaging_servicebuseventpublisher_bunkerflow_integration_messaging_servicebuseventpublisher, src_bunkerflow_worker_landing_servicebuslandingworker_bunkerflow_worker_landing_servicebuslandingworker, src_bunkerflow_integration_landing_parquetlandingwriter_bunkerflow_integration_landing_parquetlandingwriter [EXTRACTED 1.00]
- **Three Layers Of Duplicate Defence** — readme_deterministic_event_id, readme_reserve_then_release, src_bunkerflow_integration_idempotency_postgresdeduplicationstore_bunkerflow_integration_idempotency_postgresdeduplicationstore, terraform_azurerm_servicebus_topic_events, readme_silver_dedup_noop [EXTRACTED 1.00]
- **Least-Privilege Credential Split** — readme_split_credentials, terraform_azurerm_servicebus_topic_authorization_rule_gateway_send, terraform_azurerm_servicebus_topic_authorization_rule_landing_listen, src_bunkerflow_integration_messaging_servicebuseventpublisher_bunkerflow_integration_messaging_servicebuseventpublisher, src_bunkerflow_integration_messaging_servicebusdeadlettersink_bunkerflow_integration_messaging_servicebusdeadlettersink, docs_azure_verification_least_privilege [EXTRACTED 1.00]

## Communities (25 total, 1 thin omitted)

### Community 0 - "Namespace Backbone"
Cohesion: 0.07
Nodes (26): BunkerFlow.Integration.Errors, BunkerFlow.Contracts, BunkerFlow.Integration.DeadLettering, BunkerFlow.Integration.Idempotency, BunkerFlow.Integration.Messaging, BunkerFlow.Integration.Landing, BunkerFlow.Integration.Composition, BunkerFlow.Integration.Pipeline (+18 more)

### Community 1 - "REST API Surface"
Cohesion: 0.05
Nodes (40): BunkerFlow.Api, Loopback Mode, smoke.sh script, ApiError, ApiResponse, ErrorEnvelope, OkEnvelope, EventPage (+32 more)

### Community 2 - "Azure Infrastructure And Project Scope"
Cohesion: 0.06
Nodes (56): Terraform Validation Job, Env-Sourced Split Credentials, Azure Overlay Compose, Least Privilege Actually Holds, Live Azure Verification Run, Region Capacity Constraint, Schema Version Subscription Filter, Proved, Eight-Step Build Plan (+48 more)

### Community 3 - "CI And Dead-Letter Path"
Cohesion: 0.06
Nodes (34): Separate Broker Tests Job, Container Image Build Job, CI Pipeline, Redpanda As Kafka Substitute, IAsyncDisposable, Two Dead-Letter Paths, Kafka Offset Commit Rule, Transient vs Permanent Failure Types (+26 more)

### Community 4 - "Validator And Pipeline Tests"
Cohesion: 0.15
Nodes (12): BunkerTradeValidatorTests, Fact, InlineData, Theory, IngestionPipelineTests, Fact, Task, ScriptedEventPublisher (+4 more)

### Community 5 - "Local Stack And Auth Filter"
Cohesion: 0.07
Nodes (26): byte, Single-Machine Local Stack, PostgreSQL Service, Service Bus Emulator, Deliberate Throwaway API Key, Broker Duplicate Detection, Proved, EndpointFilterDelegate, EndpointFilterInvocationContext (+18 more)

### Community 6 - "Kafka Offset Commit Tests"
Cohesion: 0.11
Nodes (20): bool, BunkerFlow.Broker.Tests, IAsyncLifetime, ICollectionFixture, RedpandaContainer, ScriptedPublisher, KafkaOffsetCommitTests, ScriptedPublisher (+12 more)

### Community 7 - "Dependency Injection And Options"
Cohesion: 0.09
Nodes (21): IConfiguration, IServiceCollection, ProcessErrorEventArgs, ProcessMessageEventArgs, SemaphoreSlim, ServiceBusClient, ServiceBusProcessor, IntegrationServiceCollectionExtensions (+13 more)

### Community 8 - "Solution And NuGet Dependencies"
Cohesion: 0.07
Nodes (25): Azure.Messaging.ServiceBus (7.20.2), Confluent.Kafka (2.15.0), Microsoft.AspNetCore.Mvc.Testing (10.0.10), Microsoft.AspNetCore.OpenApi (10.0.10), Microsoft.Extensions.DependencyInjection.Abstractions (10.0.10), Microsoft.Extensions.Logging.Abstractions (10.0.10), Microsoft.Extensions.Options.ConfigurationExtensions (10.0.10), Microsoft.OpenApi (2.11.0) (+17 more)

### Community 9 - "Parquet Landing Writer"
Cohesion: 0.09
Nodes (20): DateTime, IDisposable, ParquetLandingWriter, TradeRow, CancellationToken, ILogger, IReadOnlyCollection, string (+12 more)

### Community 10 - "Metrics And Operational Endpoints"
Cohesion: 0.10
Nodes (13): ConcurrentCounter, long, Outcome And Channel Metrics, OperationalEndpoints, IEndpointRouteBuilder, IngestionChannel, ConcurrentCounter, PipelineMetrics (+5 more)

### Community 11 - "Normalization And Field Aliases"
Cohesion: 0.16
Nodes (10): IReadOnlyDictionary, Alias-Driven Field Mapping, SourceRecord, BunkerTradeNormalizer, DateTimeOffset, string, TimeProvider, IRecordNormalizer (+2 more)

### Community 12 - "Event Repository And Query"
Cohesion: 0.13
Nodes (14): NpgsqlDataReader, EventQuery, DateTimeOffset, int, InMemoryEventRepository, CancellationToken, ConcurrentDictionary, IReadOnlyList (+6 more)

### Community 13 - "Event Endpoints And Publisher Contracts"
Cohesion: 0.11
Nodes (18): EventEndpoints, CancellationToken, DateTimeOffset, IEndpointRouteBuilder, IResult, Task, CancellationToken, IResult (+10 more)

### Community 14 - "API End-To-End Tests"
Cohesion: 0.18
Nodes (8): Action, ApiEndpointTests, Dictionary, Fact, HttpClient, List, Task, WebApplicationFactory

### Community 15 - "Data Quality Validation"
Cohesion: 0.13
Nodes (14): decimal, GeneratedRegex, IReadOnlySet, Data Quality Checks, Validation Reports Every Failure, Regex, BunkerTradeValidator, TimeProvider (+6 more)

### Community 16 - "Batch Puller And Mock Sources"
Cohesion: 0.13
Nodes (14): BackgroundService, IHttpClientFactory, Simulated Source Systems, MockSourceEndpoints, Dictionary, IEndpointRouteBuilder, List, string (+6 more)

### Community 17 - "API Key Enforcement Tests"
Cohesion: 0.18
Nodes (11): HttpRequestMessage, IClassFixture, Program, ApiKeyTests, Fact, HttpClient, InlineData, string (+3 more)

### Community 18 - "Retry Policy And Backoff"
Cohesion: 0.21
Nodes (10): RetryPolicy, CancellationToken, Func, int, Task, TimeProvider, TimeSpan, RetryPolicyTests (+2 more)

### Community 19 - "API Launch Profiles"
Cohesion: 0.13
Nodes (15): ASPNETCORE_ENVIRONMENT, applicationUrl, commandName, dotnetRunMessages, environmentVariables, launchBrowser, applicationUrl, commandName (+7 more)

### Community 20 - "Lakehouse Notebook And Scripts"
Cohesion: 0.15
Nodes (10): Gold Table In Databricks, Bronze / Silver / Gold Layers, Half-Up Rounding Parity With Spark, DOTNET_CLI_TELEMETRY_OPTOUT, DOTNET_NOLOGO, DOTNET_SYSTEM_GLOBALIZATION_INVARIANT, PATH, env.sh script (+2 more)

### Community 21 - "Deduplication Store"
Cohesion: 0.29
Nodes (7): InMemoryDeduplicationStore, CancellationToken, ConcurrentDictionary, Task, InMemoryDeduplicationStoreTests, Fact, Task

### Community 22 - "Worker Launch Profiles"
Cohesion: 0.25
Nodes (7): commandName, dotnetRunMessages, environmentVariables, DOTNET_ENVIRONMENT, profiles, BunkerFlow.Worker, $schema

### Community 23 - "Integration Exception Types"
Cohesion: 0.53
Nodes (5): Exception, IntegrationException, NormalizationException, PermanentPublishException, TransientPublishException

## Knowledge Gaps
- **58 isolated node(s):** `env.sh script`, `PATH`, `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT`, `DOTNET_CLI_TELEMETRY_OPTOUT`, `DOTNET_NOLOGO` (+53 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **1 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `IntegrationEvent` connect `REST API Surface` to `Azure Infrastructure And Project Scope`, `Validator And Pipeline Tests`, `Local Stack And Auth Filter`, `Kafka Offset Commit Tests`, `Dependency Injection And Options`, `Parquet Landing Writer`, `Metrics And Operational Endpoints`, `Normalization And Field Aliases`, `Event Repository And Query`, `Event Endpoints And Publisher Contracts`, `API End-To-End Tests`, `Data Quality Validation`?**
  _High betweenness centrality (0.246) - this node is a cross-community bridge._
- **Why does `IngestionPipeline` connect `REST API Surface` to `Namespace Backbone`, `Azure Infrastructure And Project Scope`, `CI And Dead-Letter Path`, `Validator And Pipeline Tests`, `Kafka Offset Commit Tests`, `Metrics And Operational Endpoints`, `Normalization And Field Aliases`, `Event Endpoints And Publisher Contracts`, `Batch Puller And Mock Sources`, `Retry Policy And Backoff`?**
  _High betweenness centrality (0.123) - this node is a cross-community bridge._
- **Why does `BunkerFlow.Contracts` connect `Namespace Backbone` to `REST API Surface`, `Azure Infrastructure And Project Scope`, `Metrics And Operational Endpoints`, `Local Stack And Auth Filter`?**
  _High betweenness centrality (0.112) - this node is a cross-community bridge._
- **What connects `env.sh script`, `PATH`, `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT` to the rest of the system?**
  _58 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Namespace Backbone` be split into smaller, more focused modules?**
  _Cohesion score 0.07191316146540028 - nodes in this community are weakly interconnected._
- **Should `REST API Surface` be split into smaller, more focused modules?**
  _Cohesion score 0.0506558118498417 - nodes in this community are weakly interconnected._
- **Should `Azure Infrastructure And Project Scope` be split into smaller, more focused modules?**
  _Cohesion score 0.05711263881544157 - nodes in this community are weakly interconnected._