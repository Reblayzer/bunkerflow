#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
source scripts/env.sh

dotnet new xunit -o tests/BunkerFlow.Broker.Tests -n BunkerFlow.Broker.Tests --framework net10.0
rm -f tests/BunkerFlow.Broker.Tests/UnitTest1.cs

dotnet sln add tests/BunkerFlow.Broker.Tests/BunkerFlow.Broker.Tests.csproj
dotnet add tests/BunkerFlow.Broker.Tests/BunkerFlow.Broker.Tests.csproj reference src/BunkerFlow.Worker/BunkerFlow.Worker.csproj
dotnet add tests/BunkerFlow.Broker.Tests/BunkerFlow.Broker.Tests.csproj package Testcontainers.Redpanda

echo "--- done ---"
