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

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app ./

# The .NET images already ship a non-root user; APP_UID is its id. Only the
# landing directory needs to be writable by it.
RUN mkdir -p /app/data && chown -R $APP_UID:$APP_UID /app
USER $APP_UID

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "BunkerFlow.Worker.dll"]
