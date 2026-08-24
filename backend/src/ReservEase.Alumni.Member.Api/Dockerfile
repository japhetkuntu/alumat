FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ReservEase.Alumni.sln .
COPY src/ReservEase.Alumni.Common.Sdk/ReservEase.Alumni.Common.Sdk.csproj src/ReservEase.Alumni.Common.Sdk/
COPY src/ReservEase.Alumni.PostgresDb.Sdk/ReservEase.Alumni.PostgresDb.Sdk.csproj src/ReservEase.Alumni.PostgresDb.Sdk/
COPY src/ReservEase.Alumni.Redis.Sdk/ReservEase.Alumni.Redis.Sdk.csproj src/ReservEase.Alumni.Redis.Sdk/
COPY src/ReservEase.Alumni.Storage.Sdk/ReservEase.Alumni.Storage.Sdk.csproj src/ReservEase.Alumni.Storage.Sdk/
COPY src/ReservEase.Alumni.Mailtrap.Sdk/ReservEase.Alumni.Mailtrap.Sdk.csproj src/ReservEase.Alumni.Mailtrap.Sdk/
COPY src/ReservEase.Alumni.Mailhog.Sdk/ReservEase.Alumni.Mailhog.Sdk.csproj src/ReservEase.Alumni.Mailhog.Sdk/
COPY src/ReservEase.Alumni.Paystack.Sdk/ReservEase.Alumni.Paystack.Sdk.csproj src/ReservEase.Alumni.Paystack.Sdk/
COPY src/ReservEase.Alumni.Sms.Sdk/ReservEase.Alumni.Sms.Sdk.csproj src/ReservEase.Alumni.Sms.Sdk/
COPY src/ReservEase.Alumni.Whatsapp.Sdk/ReservEase.Alumni.Whatsapp.Sdk.csproj src/ReservEase.Alumni.Whatsapp.Sdk/
COPY src/ReservEase.Alumni.Admin.Api/ReservEase.Alumni.Admin.Api.csproj src/ReservEase.Alumni.Admin.Api/
COPY src/ReservEase.Alumni.Member.Api/ReservEase.Alumni.Member.Api.csproj src/ReservEase.Alumni.Member.Api/

RUN dotnet restore src/ReservEase.Alumni.Member.Api/ReservEase.Alumni.Member.Api.csproj

COPY . .

RUN dotnet publish src/ReservEase.Alumni.Member.Api/ReservEase.Alumni.Member.Api.csproj \
    -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ReservEase.Alumni.Member.Api.dll"]
