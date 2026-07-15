FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Contracts/ Contracts/
COPY NotificationWorker/ NotificationWorker/
RUN dotnet restore NotificationWorker/NotificationWorker.csproj
RUN dotnet publish NotificationWorker/NotificationWorker.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "NotificationWorker.dll"]
