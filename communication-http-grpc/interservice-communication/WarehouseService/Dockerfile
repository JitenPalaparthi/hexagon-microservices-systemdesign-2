FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Contracts/ Contracts/
COPY WarehouseService/ WarehouseService/
RUN dotnet restore WarehouseService/WarehouseService.csproj
RUN dotnet publish WarehouseService/WarehouseService.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "WarehouseService.dll"]
