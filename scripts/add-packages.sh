#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
source scripts/env.sh

dotnet add src/BunkerFlow.Integration/BunkerFlow.Integration.csproj package Microsoft.Extensions.Logging.Abstractions
dotnet add src/BunkerFlow.Integration/BunkerFlow.Integration.csproj package Azure.Messaging.ServiceBus
dotnet add src/BunkerFlow.Integration/BunkerFlow.Integration.csproj package Confluent.Kafka
dotnet add src/BunkerFlow.Integration/BunkerFlow.Integration.csproj package Npgsql
dotnet add src/BunkerFlow.Integration/BunkerFlow.Integration.csproj package Parquet.Net

dotnet add src/BunkerFlow.Worker/BunkerFlow.Worker.csproj package Microsoft.Extensions.Http

echo "--- packages added ---"
