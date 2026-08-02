#!/usr/bin/env bash
# Fast suite. No containers, no network.
set -euo pipefail
cd "$(dirname "$0")/.."
source scripts/env.sh

dotnet build BunkerFlow.slnx --nologo -warnaserror
dotnet test tests/BunkerFlow.Integration.Tests/BunkerFlow.Integration.Tests.csproj --nologo
