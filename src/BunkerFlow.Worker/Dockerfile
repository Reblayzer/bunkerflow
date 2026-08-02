# Build from the repository root:
#   docker build -f src/BunkerFlow.Worker/Dockerfile -t bunkerflow-worker .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

COPY Directory.Build.props ./
COPY src/BunkerFlow.Contracts/BunkerFlow.Contracts.csproj src/BunkerFlow.Contracts/
COPY src/BunkerFlow.Integration/BunkerFlow.Integration.csproj src/BunkerFlow.Integration/
COPY src/BunkerFlow.Worker/BunkerFlow.Worker.csproj src/BunkerFlow.Worker/
RUN dotnet restore src/BunkerFlow.Worker/BunkerFlow.Worker.csproj

COPY src/ src/
RUN dotnet publish src/BunkerFlow.Worker/BunkerFlow.Worker.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app

RUN adduser --disabled-password --gecos "" --uid 10001 bunkerflow \
    && mkdir -p /app/data \
    && chown -R bunkerflow:bunkerflow /app
USER bunkerflow

COPY --from=build --chown=bunkerflow:bunkerflow /app ./

ENTRYPOINT ["dotnet", "BunkerFlow.Worker.dll"]
