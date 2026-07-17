FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ProductGrpcServer/ProductGrpcServer.csproj ProductGrpcServer/
RUN dotnet restore ProductGrpcServer/ProductGrpcServer.csproj

COPY ProductGrpcServer/ ProductGrpcServer/
RUN dotnet publish ProductGrpcServer/ProductGrpcServer.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080 8081
ENTRYPOINT ["dotnet", "ProductGrpcServer.dll"]
