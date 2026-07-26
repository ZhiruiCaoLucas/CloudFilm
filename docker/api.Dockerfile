# syntax=docker/dockerfile:1
# ---------------------------------------------------------------------------
# CloudFilm API
# Multi-stage: the SDK never reaches the runtime image, which keeps the deployed
# surface small and free of build tooling.
# ---------------------------------------------------------------------------

FROM mcr.microsoft.com/dotnet/sdk:9.0-noble AS build
WORKDIR /src

# Restore first, using only the project graph, so that a source-only change does
# not invalidate the (slow) NuGet restore layer.
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY apps/api/src/CloudFilm.Domain/CloudFilm.Domain.csproj                 apps/api/src/CloudFilm.Domain/
COPY apps/api/src/CloudFilm.Application/CloudFilm.Application.csproj       apps/api/src/CloudFilm.Application/
COPY apps/api/src/CloudFilm.Infrastructure/CloudFilm.Infrastructure.csproj apps/api/src/CloudFilm.Infrastructure/
COPY apps/api/src/CloudFilm.Api/CloudFilm.Api.csproj                       apps/api/src/CloudFilm.Api/
RUN dotnet restore apps/api/src/CloudFilm.Api/CloudFilm.Api.csproj

COPY apps/api/src apps/api/src
RUN dotnet publish apps/api/src/CloudFilm.Api/CloudFilm.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app

FROM mcr.microsoft.com/dotnet/aspnet:9.0-noble AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_EnableDiagnostics=0

COPY --from=build /app .

# Runs as the non-root user provided by the base image.
USER $APP_UID
EXPOSE 8080

ENTRYPOINT ["dotnet", "CloudFilm.Api.dll"]
