FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props ./
COPY src/OrderPublisher/OrderPublisher.csproj src/OrderPublisher/
RUN dotnet restore src/OrderPublisher/OrderPublisher.csproj
COPY src/OrderPublisher/ src/OrderPublisher/
RUN dotnet publish src/OrderPublisher/OrderPublisher.csproj -c Release --no-restore -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "OrderPublisher.dll"]
