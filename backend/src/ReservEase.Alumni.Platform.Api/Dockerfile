FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files
COPY ReservEase.Alumni.sln .
COPY src/ReservEase.Alumni.Common.Sdk/ReservEase.Alumni.Common.Sdk.csproj src/ReservEase.Alumni.Common.Sdk/
COPY src/ReservEase.Alumni.PostgresDb.Sdk/ReservEase.Alumni.PostgresDb.Sdk.csproj src/ReservEase.Alumni.PostgresDb.Sdk/
COPY src/ReservEase.Alumni.Redis.Sdk/ReservEase.Alumni.Redis.Sdk.csproj src/ReservEase.Alumni.Redis.Sdk/
COPY src/ReservEase.Alumni.Platform.Api/ReservEase.Alumni.Platform.Api.csproj src/ReservEase.Alumni.Platform.Api/

RUN dotnet restore src/ReservEase.Alumni.Platform.Api/ReservEase.Alumni.Platform.Api.csproj

COPY . .

RUN dotnet publish src/ReservEase.Alumni.Platform.Api/ReservEase.Alumni.Platform.Api.csproj \
    -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ReservEase.Alumni.Platform.Api.dll"]
