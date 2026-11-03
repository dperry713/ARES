# syntax=docker/dockerfile:1.7
#
# ARES management plane — multi-stage build.
#
# Stage 1 restores against the project files only, so a source-only change
# reuses the NuGet layer cache. Stage 2 publishes framework-dependent and runs
# as the image's built-in non-root user.
#
#   docker build -t ares-api:local .
#   docker run --rm -p 8080:8080 -e ARES_BOOTSTRAP_ADMIN_KEY=... ares-api:local

ARG DOTNET_VERSION=10.0

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
WORKDIR /src

ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1 \
    DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1

# Restore inputs: build configuration plus every project file in the
# Ares.Api closure (Ares.Api -> {Contracts, Domain, Engagements, Identity,
# Policy, Approvals, Execution, Evidence, Infrastructure}).
COPY Directory.Build.props Directory.Packages.props global.json ./
COPY src/backend/Ares.Api/Ares.Api.csproj                         src/backend/Ares.Api/
COPY src/backend/Ares.Contracts/Ares.Contracts.csproj             src/backend/Ares.Contracts/
COPY src/backend/Ares.Domain/Ares.Domain.csproj                   src/backend/Ares.Domain/
COPY src/backend/Ares.Engagements/Ares.Engagements.csproj         src/backend/Ares.Engagements/
COPY src/backend/Ares.Identity/Ares.Identity.csproj               src/backend/Ares.Identity/
COPY src/backend/Ares.Policy/Ares.Policy.csproj                   src/backend/Ares.Policy/
COPY src/backend/Ares.Approvals/Ares.Approvals.csproj             src/backend/Ares.Approvals/
COPY src/backend/Ares.Execution/Ares.Execution.csproj             src/backend/Ares.Execution/
COPY src/backend/Ares.Evidence/Ares.Evidence.csproj               src/backend/Ares.Evidence/
COPY src/backend/Ares.Infrastructure/Ares.Infrastructure.csproj   src/backend/Ares.Infrastructure/

RUN dotnet restore src/backend/Ares.Api/Ares.Api.csproj

COPY src/backend/ src/backend/

ARG VERSION=1.0.0
RUN dotnet publish src/backend/Ares.Api/Ares.Api.csproj \
        --configuration Release \
        --no-restore \
        --output /app/publish \
        -p:Version=${VERSION} \
        -p:UseAppHost=false \
        -p:ContinuousIntegrationBuild=true

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS runtime
WORKDIR /app

# curl backs the container HEALTHCHECK. The aspnet image ships no HTTP client.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    ARES_DATA_DIR=/app/data

COPY --from=build /app/publish ./

# Durable journal lives on a volume owned by the unprivileged runtime user.
RUN mkdir -p /app/data && chown -R $APP_UID:$APP_UID /app/data
VOLUME ["/app/data"]

USER $APP_UID
EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
    CMD curl -fsS http://127.0.0.1:8080/health/live || exit 1

ENTRYPOINT ["dotnet", "Ares.Api.dll"]
