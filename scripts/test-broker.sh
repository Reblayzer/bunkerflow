#!/usr/bin/env bash
# Broker-backed tests. Starts a Redpanda container through Testcontainers, so a
# reachable Docker daemon is required. Slower than scripts/test.sh.
set -euo pipefail
cd "$(dirname "$0")/.."
source scripts/env.sh

dotnet test tests/BunkerFlow.Broker.Tests/BunkerFlow.Broker.Tests.csproj --nologo
