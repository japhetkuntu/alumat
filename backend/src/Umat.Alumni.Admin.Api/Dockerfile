FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files
COPY Umat.Alumni.sln .
COPY src/Umat.Alumni.Common.Sdk/Umat.Alumni.Common.Sdk.csproj src/Umat.Alumni.Common.Sdk/
COPY src/Umat.Alumni.PostgresDb.Sdk/Umat.Alumni.PostgresDb.Sdk.csproj src/Umat.Alumni.PostgresDb.Sdk/
COPY src/Umat.Alumni.Redis.Sdk/Umat.Alumni.Redis.Sdk.csproj src/Umat.Alumni.Redis.Sdk/
COPY src/Umat.Alumni.Storage.Sdk/Umat.Alumni.Storage.Sdk.csproj src/Umat.Alumni.Storage.Sdk/
COPY src/Umat.Alumni.Mailtrap.Sdk/Umat.Alumni.Mailtrap.Sdk.csproj src/Umat.Alumni.Mailtrap.Sdk/
COPY src/Umat.Alumni.Mailhog.Sdk/Umat.Alumni.Mailhog.Sdk.csproj src/Umat.Alumni.Mailhog.Sdk/
COPY src/Umat.Alumni.Paystack.Sdk/Umat.Alumni.Paystack.Sdk.csproj src/Umat.Alumni.Paystack.Sdk/
COPY src/Umat.Alumni.Admin.Api/Umat.Alumni.Admin.Api.csproj src/Umat.Alumni.Admin.Api/
COPY src/Umat.Alumni.Member.Api/Umat.Alumni.Member.Api.csproj src/Umat.Alumni.Member.Api/

RUN dotnet restore src/Umat.Alumni.Admin.Api/Umat.Alumni.Admin.Api.csproj

COPY . .

RUN dotnet publish src/Umat.Alumni.Admin.Api/Umat.Alumni.Admin.Api.csproj \
    -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Umat.Alumni.Admin.Api.dll"]
