# =========================
# BUILD STAGE
# =========================
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /app

COPY *.csproj ./
RUN dotnet restore

COPY . ./

RUN dotnet publish -c Release -o out

# =========================
# RUNTIME STAGE
# =========================
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app

# Mount point for uploaded promotion images. Backed by a named volume in
# docker-compose.yml so uploads survive container rebuilds.
RUN mkdir -p wwwroot/images/promotions

ENV ASPNETCORE_URLS=http://+:80
EXPOSE 80

COPY --from=build /app/out .

# ASPNETCORE_ENVIRONMENT is intentionally not set here: it defaults to
# Production. docker-compose.dev.yml overrides it for local development.

ENTRYPOINT ["dotnet", "PharmacyWorkerAPI.dll"]
