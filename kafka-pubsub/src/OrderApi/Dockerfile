FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props ./
COPY src/Contracts/Contracts.csproj src/Contracts/
COPY src/OrderApi/OrderApi.csproj src/OrderApi/
RUN dotnet restore src/OrderApi/OrderApi.csproj
COPY src/Contracts src/Contracts
COPY src/OrderApi src/OrderApi
RUN dotnet publish src/OrderApi/OrderApi.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "OrderApi.dll"]
