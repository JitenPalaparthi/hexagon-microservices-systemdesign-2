FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Contracts/ Contracts/
COPY CheckoutService/ CheckoutService/
RUN dotnet restore CheckoutService/CheckoutService.csproj
RUN dotnet publish CheckoutService/CheckoutService.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "CheckoutService.dll"]
