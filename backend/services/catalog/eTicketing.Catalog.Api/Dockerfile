FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY shared/eTicketing.Contracts/*.csproj shared/eTicketing.Contracts/
COPY shared/eTicketing.Shared.Auth/*.csproj shared/eTicketing.Shared.Auth/
COPY shared/eTicketing.Shared.Messaging/*.csproj shared/eTicketing.Shared.Messaging/
COPY shared/eTicketing.Shared.Storage/*.csproj shared/eTicketing.Shared.Storage/
COPY services/catalog/eTicketing.Catalog.Data/*.csproj services/catalog/eTicketing.Catalog.Data/
COPY services/catalog/eTicketing.Catalog.Business/*.csproj services/catalog/eTicketing.Catalog.Business/
COPY services/catalog/eTicketing.Catalog.Api/*.csproj services/catalog/eTicketing.Catalog.Api/
RUN dotnet restore services/catalog/eTicketing.Catalog.Api/eTicketing.Catalog.Api.csproj

COPY shared/eTicketing.Contracts/ shared/eTicketing.Contracts/
COPY shared/eTicketing.Shared.Auth/ shared/eTicketing.Shared.Auth/
COPY shared/eTicketing.Shared.Messaging/ shared/eTicketing.Shared.Messaging/
COPY shared/eTicketing.Shared.Storage/ shared/eTicketing.Shared.Storage/
COPY services/catalog/ services/catalog/
RUN dotnet publish services/catalog/eTicketing.Catalog.Api/eTicketing.Catalog.Api.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "eTicketing.Catalog.Api.dll"]
