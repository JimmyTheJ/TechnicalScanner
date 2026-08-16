FROM mcr.microsoft.com/dotnet/sdk:10.0 AS restore
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props ./
COPY src/ ./src/
RUN dotnet restore src/TechnicalScanner.Api/TechnicalScanner.Api.csproj && dotnet restore src/TechnicalScanner.Worker/TechnicalScanner.Worker.csproj

FROM restore AS publish-api
RUN dotnet publish src/TechnicalScanner.Api/TechnicalScanner.Api.csproj -c Release -o /app/publish --no-restore

FROM restore AS publish-worker
RUN dotnet publish src/TechnicalScanner.Worker/TechnicalScanner.Worker.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS api
WORKDIR /app
COPY --from=publish-api /app/publish .
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "TechnicalScanner.Api.dll"]

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS worker
WORKDIR /app
COPY --from=publish-worker /app/publish .
ENTRYPOINT ["dotnet", "TechnicalScanner.Worker.dll"]
