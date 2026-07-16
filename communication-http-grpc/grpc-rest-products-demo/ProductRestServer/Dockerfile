FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ProductRestServer/ProductRestServer.csproj ProductRestServer/
RUN dotnet restore ProductRestServer/ProductRestServer.csproj
COPY ProductRestServer/ ProductRestServer/
RUN dotnet publish ProductRestServer/ProductRestServer.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "ProductRestServer.dll"]
