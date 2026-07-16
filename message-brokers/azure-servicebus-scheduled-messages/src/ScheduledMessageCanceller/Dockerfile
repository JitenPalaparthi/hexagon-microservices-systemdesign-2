FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/ScheduledMessageCanceller/ScheduledMessageCanceller.csproj ./
RUN dotnet restore ScheduledMessageCanceller.csproj

COPY src/ScheduledMessageCanceller/ ./
RUN dotnet publish ScheduledMessageCanceller.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./
ENTRYPOINT ["dotnet", "ScheduledMessageCanceller.dll"]
