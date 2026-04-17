# ─── Build stage ──────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["PlateformePFA.API/PlateformePFA.API.csproj", "PlateformePFA.API/"]
RUN dotnet restore "PlateformePFA.API/PlateformePFA.API.csproj"

COPY . .
WORKDIR "/src/PlateformePFA.API"
RUN dotnet publish "PlateformePFA.API.csproj" -c Release --no-restore -o /app/publish

# ─── Runtime stage ────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

# The aspnet:8.0 image ships with a non-root 'app' user (UID 1654).
# Drop privileges for the runtime process.
USER app

ENTRYPOINT ["dotnet", "PlateformePFA.API.dll"]
