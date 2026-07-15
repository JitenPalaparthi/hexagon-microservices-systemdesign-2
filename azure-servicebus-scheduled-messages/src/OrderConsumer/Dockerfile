FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/OrderConsumer/OrderConsumer.csproj ./
RUN dotnet restore OrderConsumer.csproj

COPY src/OrderConsumer/ ./
RUN dotnet publish OrderConsumer.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./
ENTRYPOINT ["dotnet", "OrderConsumer.dll"]
