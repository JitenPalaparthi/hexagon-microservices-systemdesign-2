FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Contracts/ Contracts/
COPY InventoryWorker/ InventoryWorker/
RUN dotnet restore InventoryWorker/InventoryWorker.csproj
RUN dotnet publish InventoryWorker/InventoryWorker.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "InventoryWorker.dll"]
