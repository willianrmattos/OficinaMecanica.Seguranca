FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY nuget.config ./
COPY OficinaMecanica.Seguranca.sln ./
COPY src/OficinaMecanica.Seguranca.Domain/OficinaMecanica.Seguranca.Domain.csproj src/OficinaMecanica.Seguranca.Domain/
COPY src/OficinaMecanica.Seguranca.Application/OficinaMecanica.Seguranca.Application.csproj src/OficinaMecanica.Seguranca.Application/
COPY src/OficinaMecanica.Seguranca.Infrastructure/OficinaMecanica.Seguranca.Infrastructure.csproj src/OficinaMecanica.Seguranca.Infrastructure/
COPY src/OficinaMecanica.Seguranca.Functions/OficinaMecanica.Seguranca.Functions.csproj src/OficinaMecanica.Seguranca.Functions/

RUN dotnet restore src/OficinaMecanica.Seguranca.Functions/OficinaMecanica.Seguranca.Functions.csproj

COPY src/ src/
RUN dotnet publish src/OficinaMecanica.Seguranca.Functions/OficinaMecanica.Seguranca.Functions.csproj -c Release -o /home/site/wwwroot --no-restore

FROM mcr.microsoft.com/azure-functions/dotnet-isolated:4-dotnet-isolated8.0
ENV AzureFunctionsJobHost__Logging__Console__IsEnabled=true

COPY --from=build ["/home/site/wwwroot", "/home/site/wwwroot"]
