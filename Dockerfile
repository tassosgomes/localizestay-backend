# syntax=docker/dockerfile:1

## ---- Build stage -----------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

# Restore first, from project files only, to keep the Docker layer cache warm
# across source-only changes.
COPY LocalizeStay.sln Directory.Build.props Directory.Packages.props NuGet.Config ./
COPY src/LocalizeStay.Api/LocalizeStay.Api.csproj src/LocalizeStay.Api/
COPY src/BuildingBlocks/LocalizeStay.SharedKernel/LocalizeStay.SharedKernel.csproj src/BuildingBlocks/LocalizeStay.SharedKernel/
COPY src/Modules/Discovery/LocalizeStay.Modules.Discovery/LocalizeStay.Modules.Discovery.csproj src/Modules/Discovery/LocalizeStay.Modules.Discovery/
COPY src/Modules/Discovery/LocalizeStay.Modules.Discovery.Contracts/LocalizeStay.Modules.Discovery.Contracts.csproj src/Modules/Discovery/LocalizeStay.Modules.Discovery.Contracts/
COPY src/Modules/Inventory/LocalizeStay.Modules.Inventory/LocalizeStay.Modules.Inventory.csproj src/Modules/Inventory/LocalizeStay.Modules.Inventory/
COPY src/Modules/Inventory/LocalizeStay.Modules.Inventory.Contracts/LocalizeStay.Modules.Inventory.Contracts.csproj src/Modules/Inventory/LocalizeStay.Modules.Inventory.Contracts/
COPY src/Modules/Booking/LocalizeStay.Modules.Booking/LocalizeStay.Modules.Booking.csproj src/Modules/Booking/LocalizeStay.Modules.Booking/
COPY src/Modules/Booking/LocalizeStay.Modules.Booking.Contracts/LocalizeStay.Modules.Booking.Contracts.csproj src/Modules/Booking/LocalizeStay.Modules.Booking.Contracts/
COPY src/Modules/Payments/LocalizeStay.Modules.Payments/LocalizeStay.Modules.Payments.csproj src/Modules/Payments/LocalizeStay.Modules.Payments/
COPY src/Modules/Payments/LocalizeStay.Modules.Payments.Contracts/LocalizeStay.Modules.Payments.Contracts.csproj src/Modules/Payments/LocalizeStay.Modules.Payments.Contracts/
COPY src/Modules/CustomerCare/LocalizeStay.Modules.CustomerCare/LocalizeStay.Modules.CustomerCare.csproj src/Modules/CustomerCare/LocalizeStay.Modules.CustomerCare/
COPY src/Modules/CustomerCare/LocalizeStay.Modules.CustomerCare.Contracts/LocalizeStay.Modules.CustomerCare.Contracts.csproj src/Modules/CustomerCare/LocalizeStay.Modules.CustomerCare.Contracts/
COPY src/Modules/Curation/LocalizeStay.Modules.Curation/LocalizeStay.Modules.Curation.csproj src/Modules/Curation/LocalizeStay.Modules.Curation/
COPY src/Modules/Curation/LocalizeStay.Modules.Curation.Contracts/LocalizeStay.Modules.Curation.Contracts.csproj src/Modules/Curation/LocalizeStay.Modules.Curation.Contracts/
COPY src/Modules/Operations/LocalizeStay.Modules.Operations/LocalizeStay.Modules.Operations.csproj src/Modules/Operations/LocalizeStay.Modules.Operations/
COPY src/Modules/Operations/LocalizeStay.Modules.Operations.Contracts/LocalizeStay.Modules.Operations.Contracts.csproj src/Modules/Operations/LocalizeStay.Modules.Operations.Contracts/
COPY src/Modules/IdentityAccess/LocalizeStay.Modules.IdentityAccess/LocalizeStay.Modules.IdentityAccess.csproj src/Modules/IdentityAccess/LocalizeStay.Modules.IdentityAccess/
COPY src/Modules/IdentityAccess/LocalizeStay.Modules.IdentityAccess.Contracts/LocalizeStay.Modules.IdentityAccess.Contracts.csproj src/Modules/IdentityAccess/LocalizeStay.Modules.IdentityAccess.Contracts/
COPY src/Modules/Insights/LocalizeStay.Modules.Insights/LocalizeStay.Modules.Insights.csproj src/Modules/Insights/LocalizeStay.Modules.Insights/
COPY src/Modules/Insights/LocalizeStay.Modules.Insights.Contracts/LocalizeStay.Modules.Insights.Contracts.csproj src/Modules/Insights/LocalizeStay.Modules.Insights.Contracts/

RUN dotnet restore src/LocalizeStay.Api/LocalizeStay.Api.csproj

COPY src/ src/
RUN dotnet publish src/LocalizeStay.Api/LocalizeStay.Api.csproj \
    --no-restore \
    -c Release \
    -o /app/publish

## ---- Runtime stage ----------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app

RUN addgroup -S localizestay && adduser -S localizestay -G localizestay
RUN apk add --no-cache curl

COPY --from=build /app/publish .

USER localizestay

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 \
    CMD curl -f http://localhost:8080/health/live || exit 1

ENTRYPOINT ["dotnet", "LocalizeStay.Api.dll"]
