FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props ./
COPY src/TopicSubscriber/TopicSubscriber.csproj src/TopicSubscriber/
RUN dotnet restore src/TopicSubscriber/TopicSubscriber.csproj
COPY src/TopicSubscriber/ src/TopicSubscriber/
RUN dotnet publish src/TopicSubscriber/TopicSubscriber.csproj -c Release --no-restore -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "TopicSubscriber.dll"]
