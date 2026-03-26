# Étape de construction (Build)
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restauration des dépendances
COPY ["PlateformePFA.API/PlateformePFA.API.csproj", "PlateformePFA.API/"]
RUN dotnet restore "PlateformePFA.API/PlateformePFA.API.csproj"

# Copie du code et publication
COPY . .
WORKDIR "/src/PlateformePFA.API"
RUN dotnet publish "PlateformePFA.API.csproj" -c Release -o /app/publish

# Étape d'exécution (Runtime)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "PlateformePFA.API.dll"]