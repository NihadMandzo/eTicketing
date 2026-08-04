FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY shared/eTicketing.Contracts/*.csproj shared/eTicketing.Contracts/
COPY services/payment/eTicketing.Payment.Data/*.csproj services/payment/eTicketing.Payment.Data/
COPY services/payment/eTicketing.Payment.Business/*.csproj services/payment/eTicketing.Payment.Business/
COPY services/payment/eTicketing.Payment.Api/*.csproj services/payment/eTicketing.Payment.Api/
RUN dotnet restore services/payment/eTicketing.Payment.Api/eTicketing.Payment.Api.csproj

COPY shared/eTicketing.Contracts/ shared/eTicketing.Contracts/
COPY services/payment/ services/payment/
RUN dotnet publish services/payment/eTicketing.Payment.Api/eTicketing.Payment.Api.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "eTicketing.Payment.Api.dll"]
