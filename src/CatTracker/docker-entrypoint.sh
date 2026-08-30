#!/bin/sh
set -e

# Fix bind-mount ownership at container start. Build-time chown is
# overridden when a host directory is bind-mounted over /app/data.
# This runs as root, so the chown always succeeds, and then we drop
# to appuser before starting the app.
mkdir -p /app/data
chown appuser:appuser /app/data

exec su -s /bin/sh -c 'exec dotnet CatTracker.dll' appuser