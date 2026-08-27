# syntax=docker/dockerfile:1

# ---- Build stage --------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/HenryTracker/HenryTracker.csproj ./HenryTracker/
RUN dotnet restore ./HenryTracker/HenryTracker.csproj

COPY src/HenryTracker/. ./HenryTracker/
RUN dotnet publish ./HenryTracker/HenryTracker.csproj -c Release -o /app/publish --no-restore

# ---- Runtime stage --------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Run as a non-root user
RUN useradd --uid 1654 --user-group --create-home henry
COPY --from=build /app/publish .
RUN mkdir -p /app/data && chown -R henry:henry /app
USER henry

ENV ASPNETCORE_URLS=http://+:8080
ENV DATA_DIR=/app/data
EXPOSE 8080
VOLUME ["/app/data"]

ENTRYPOINT ["dotnet", "HenryTracker.dll"]
