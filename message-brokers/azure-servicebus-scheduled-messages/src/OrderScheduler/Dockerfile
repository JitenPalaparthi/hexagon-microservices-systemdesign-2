FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/OrderScheduler/OrderScheduler.csproj ./
RUN dotnet restore OrderScheduler.csproj

COPY src/OrderScheduler/ ./
RUN dotnet publish OrderScheduler.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./
ENTRYPOINT ["dotnet", "OrderScheduler.dll"]
