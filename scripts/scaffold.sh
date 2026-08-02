#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
source scripts/env.sh

dotnet new sln --name BunkerFlow

dotnet new classlib -o src/BunkerFlow.Contracts   -n BunkerFlow.Contracts   --framework net10.0
dotnet new classlib -o src/BunkerFlow.Integration -n BunkerFlow.Integration --framework net10.0
dotnet new web      -o src/BunkerFlow.Api         -n BunkerFlow.Api         --framework net10.0
dotnet new worker   -o src/BunkerFlow.Worker      -n BunkerFlow.Worker      --framework net10.0
dotnet new xunit    -o tests/BunkerFlow.Integration.Tests -n BunkerFlow.Integration.Tests --framework net10.0

dotnet sln add src/BunkerFlow.Contracts/BunkerFlow.Contracts.csproj
dotnet sln add src/BunkerFlow.Integration/BunkerFlow.Integration.csproj
dotnet sln add src/BunkerFlow.Api/BunkerFlow.Api.csproj
dotnet sln add src/BunkerFlow.Worker/BunkerFlow.Worker.csproj
dotnet sln add tests/BunkerFlow.Integration.Tests/BunkerFlow.Integration.Tests.csproj

dotnet add src/BunkerFlow.Integration/BunkerFlow.Integration.csproj reference src/BunkerFlow.Contracts/BunkerFlow.Contracts.csproj
dotnet add src/BunkerFlow.Api/BunkerFlow.Api.csproj reference src/BunkerFlow.Integration/BunkerFlow.Integration.csproj
dotnet add src/BunkerFlow.Worker/BunkerFlow.Worker.csproj reference src/BunkerFlow.Integration/BunkerFlow.Integration.csproj
dotnet add tests/BunkerFlow.Integration.Tests/BunkerFlow.Integration.Tests.csproj reference src/BunkerFlow.Integration/BunkerFlow.Integration.csproj

rm -f src/BunkerFlow.Contracts/Class1.cs src/BunkerFlow.Integration/Class1.cs tests/BunkerFlow.Integration.Tests/UnitTest1.cs

echo "--- scaffold complete ---"
find . -name '*.csproj' | sort
