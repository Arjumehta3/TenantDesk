FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first so this layer is cached until the .csproj changes.
COPY src/TenantDesk.Api/TenantDesk.Api.csproj src/TenantDesk.Api/
RUN dotnet restore src/TenantDesk.Api/TenantDesk.Api.csproj

COPY src/ src/
RUN dotnet publish src/TenantDesk.Api/TenantDesk.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .

# .NET container images listen on port 8080 by default.
EXPOSE 8080
ENTRYPOINT ["dotnet", "TenantDesk.Api.dll"]
