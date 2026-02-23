# =========================
# BUILD STAGE
# =========================
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /app

COPY *.csproj ./
RUN dotnet restore

COPY . ./

# wwwroot creation for images
RUN mkdir -p wwwroot/images/promotions

RUN dotnet publish -c Release -o out

# =========================
# RUNTIME STAGE
# =========================
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app

RUN mkdir -p wwwroot/images/promotions

ENV ASPNETCORE_URLS=http://+:80 
EXPOSE 80  

COPY --from=build /app/out .

ENV ASPNETCORE_ENVIRONMENT=Development

ENTRYPOINT ["dotnet", "PharmacyWorkerAPI.dll"]
