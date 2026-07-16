dotnet run --urls http://localhost:5080

curl -i http://localhost:5080/hello | jq

curl -s http://localhost:5080/products/10 | jq

dotnet dev-certs https --check

dotnet dev-certs https --check --trust


dotnet dev-certs https --clean

dotnet dev-certs https --trust

dotnet run --urls https://localhost:7243

curl -s https://localhost:7243/hello | jq

curl -s https://localhost:7243/products/10 | jq

check handshake -->. curl -v https://localhost:7243/hello

```json

{
  "$schema": "http://json.schemastore.org/launchsettings.json",
  "profiles": {
    "https": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "launchBrowser": false,
      "applicationUrl": "https://localhost:7243;http://localhost:5080",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    },
    "http": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "launchBrowser": false,
      "applicationUrl": "http://localhost:5080",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    }
  }
}
``` 
- Chage this to launch settings in properties/launchSettings.json

dotnet run --launch-profile https

curl http://localhost:5080/hello
curl https://localhost:7243/hello


```csharp
app.UseHttpsRedirection();

add above 

var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.UseHttpsRedirection();

app.MapGet("/hello", () =>

```

curl -i http://localhost:5080/hello

