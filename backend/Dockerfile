# ─── Build stage ──────────────────────────────────────────────
# Pinned SDK + runtime tags for reproducible builds (the floating "8.0" tag
# would silently re-resolve to a newer patch on every CI rebuild).
FROM mcr.microsoft.com/dotnet/sdk:8.0.404-bookworm-slim AS build
WORKDIR /src

COPY ["PlateformePFA.API/PlateformePFA.API.csproj", "PlateformePFA.API/"]
RUN dotnet restore "PlateformePFA.API/PlateformePFA.API.csproj"

COPY . .
WORKDIR "/src/PlateformePFA.API"
RUN dotnet publish "PlateformePFA.API.csproj" -c Release --no-restore -o /app/publish

# ─── Runtime stage ────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0.11-bookworm-slim AS final
WORKDIR /app

# Install curl for the Docker healthcheck (aspnet:8.0 ships without it).
# Must run as root before dropping to 'app'.
RUN apt-get update \
 && apt-get install -y --no-install-recommends curl \
 && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

# The aspnet:8.0 image ships with a non-root 'app' user (UID 1654).
# Drop privileges for the runtime process.
USER app

ENTRYPOINT ["dotnet", "PlateformePFA.API.dll"]
