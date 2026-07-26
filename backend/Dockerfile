# =========================
# BUILD
# =========================
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /app

COPY *.csproj ./
RUN dotnet restore Storefront.Api.csproj

COPY . ./

RUN dotnet publish Storefront.Api.csproj -c Release -o out

# =========================
# MIGRATION BUNDLE
# =========================
# A self-contained executable carrying the migrations, run by the `migrator`
# service in docker-compose.yml. Applying schema changes from a separate
# short-lived container keeps DDL out of the API's startup path: several API
# replicas would otherwise race to migrate the same database, and the running
# API needs no schema-altering privileges at all.
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS migrations-build
WORKDIR /app

ENV PATH="$PATH:/root/.dotnet/tools"
RUN dotnet tool install --global dotnet-ef --version 9.*

COPY *.csproj ./
RUN dotnet restore Storefront.Api.csproj

COPY . ./

# --project, --output and the explicit csproj above all matter: a solution file
# sits next to the project, and once it contains more than one project every
# solution-scoped command becomes ambiguous or publishes the wrong thing.
RUN dotnet ef migrations bundle \
    --project Storefront.Api.csproj \
    --self-contained -r linux-x64 \
    --configuration Release \
    -o /app/efbundle

# =========================
# MIGRATOR RUNTIME
# =========================
FROM mcr.microsoft.com/dotnet/runtime-deps:9.0 AS migrator
WORKDIR /app

COPY --from=migrations-build /app/efbundle ./efbundle

ENTRYPOINT ["./efbundle"]

# =========================
# API RUNTIME
# =========================
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS api
WORKDIR /app

# curl is not in the aspnet base image, and the compose healthcheck needs a way
# to call /health/ready from inside the container.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# Mount point for uploaded promotion images. Backed by a named volume in
# docker-compose.yml so uploads survive container rebuilds.
RUN mkdir -p wwwroot/images/promotions

ENV ASPNETCORE_URLS=http://+:80
EXPOSE 80

COPY --from=build /app/out .

# ASPNETCORE_ENVIRONMENT is intentionally not set here: it defaults to
# Production. docker-compose.dev.yml overrides it for local development.

ENTRYPOINT ["dotnet", "Storefront.Api.dll"]
