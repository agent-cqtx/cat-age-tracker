# syntax=docker/dockerfile:1

# ---- Build stage --------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/CatTracker/CatTracker.csproj ./CatTracker/
RUN dotnet restore ./CatTracker/CatTracker.csproj

COPY src/CatTracker/. ./CatTracker/
RUN dotnet publish ./CatTracker/CatTracker.csproj -c Release -o /app/publish --no-restore

# ---- Runtime stage --------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Run as a non-root user. No --uid pinned: the base image's UID space can
# vary by version/registry, and a fixed UID can collide with something
# already defined in it (seen in the wild). Letting useradd pick a free
# one avoids that entirely.
RUN useradd --user-group --create-home appuser
COPY --from=build /app/publish .
RUN mkdir -p /app/data && chown -R appuser:appuser /app
USER appuser

ENV ASPNETCORE_URLS=http://+:8080
ENV DATA_DIR=/app/data
EXPOSE 8080
VOLUME ["/app/data"]

ENTRYPOINT ["dotnet", "CatTracker.dll"]
