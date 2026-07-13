FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props ./
COPY src/Contracts/Contracts.csproj src/Contracts/
COPY src/OrderConsumer/OrderConsumer.csproj src/OrderConsumer/
RUN dotnet restore src/OrderConsumer/OrderConsumer.csproj
COPY src/Contracts src/Contracts
COPY src/OrderConsumer src/OrderConsumer
RUN dotnet publish src/OrderConsumer/OrderConsumer.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
USER app
ENTRYPOINT ["dotnet", "OrderConsumer.dll"]
