# Build from the repository root:
#   docker build -f src/BunkerFlow.Api/Dockerfile -t bunkerflow-api .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

# Restore against the project files alone, so a source-only change reuses the
# cached restore layer.
COPY Directory.Build.props ./
COPY src/BunkerFlow.Contracts/BunkerFlow.Contracts.csproj src/BunkerFlow.Contracts/
COPY src/BunkerFlow.Integration/BunkerFlow.Integration.csproj src/BunkerFlow.Integration/
COPY src/BunkerFlow.Api/BunkerFlow.Api.csproj src/BunkerFlow.Api/
RUN dotnet restore src/BunkerFlow.Api/BunkerFlow.Api.csproj

COPY src/ src/
RUN dotnet publish src/BunkerFlow.Api/BunkerFlow.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Never run as root.
RUN adduser --disabled-password --gecos "" --uid 10001 bunkerflow \
    && mkdir -p /app/data \
    && chown -R bunkerflow:bunkerflow /app
USER bunkerflow

COPY --from=build --chown=bunkerflow:bunkerflow /app ./

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "BunkerFlow.Api.dll"]
