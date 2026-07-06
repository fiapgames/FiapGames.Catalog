FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY nuget.config .
COPY FiapGames.Core/FiapGames.Core.csproj FiapGames.Core/
COPY FiapGames.Data/FiapGames.Data.csproj FiapGames.Data/
COPY FiapGames.Services/FiapGames.Services.csproj FiapGames.Services/
COPY FiapGames.Catalog/FiapGames.Catalog.csproj FiapGames.Catalog/
RUN dotnet restore FiapGames.Catalog/FiapGames.Catalog.csproj

COPY FiapGames.Core/ FiapGames.Core/
COPY FiapGames.Data/ FiapGames.Data/
COPY FiapGames.Services/ FiapGames.Services/
COPY FiapGames.Catalog/ FiapGames.Catalog/
RUN dotnet publish FiapGames.Catalog/FiapGames.Catalog.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .

EXPOSE 8080
ENTRYPOINT ["dotnet", "FiapGames.Catalog.dll"]
