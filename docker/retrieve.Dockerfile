# syntax=docker/dockerfile:1
# ---------------------------------------------------------------------------
# CloudFilm Retrieve Service
# The only component permitted to reach a hospital network. Kept as a separate
# image so it can be deployed inside the hospital DMZ independently of the API.
# ---------------------------------------------------------------------------

FROM mcr.microsoft.com/dotnet/sdk:9.0-noble AS build
WORKDIR /src

COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY apps/retrieve/src/CloudFilm.Retrieve.Hospital/CloudFilm.Retrieve.Hospital.csproj apps/retrieve/src/CloudFilm.Retrieve.Hospital/
COPY apps/retrieve/src/CloudFilm.Retrieve/CloudFilm.Retrieve.csproj                   apps/retrieve/src/CloudFilm.Retrieve/
RUN dotnet restore apps/retrieve/src/CloudFilm.Retrieve/CloudFilm.Retrieve.csproj

COPY apps/retrieve/src apps/retrieve/src
RUN dotnet publish apps/retrieve/src/CloudFilm.Retrieve/CloudFilm.Retrieve.csproj \
    --configuration Release \
    --no-restore \
    --output /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_EnableDiagnostics=0

COPY --from=build /app .

USER $APP_UID
EXPOSE 8080

ENTRYPOINT ["dotnet", "CloudFilm.Retrieve.dll"]
