FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ServiceBusTransactions.sln ./
COPY src/ServiceBusTransactionDemo/ServiceBusTransactionDemo.csproj src/ServiceBusTransactionDemo/
RUN dotnet restore src/ServiceBusTransactionDemo/ServiceBusTransactionDemo.csproj
COPY src/ServiceBusTransactionDemo/ src/ServiceBusTransactionDemo/
RUN dotnet publish src/ServiceBusTransactionDemo/ServiceBusTransactionDemo.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ServiceBusTransactionDemo.dll"]
CMD ["help"]
