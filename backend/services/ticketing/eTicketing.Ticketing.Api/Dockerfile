FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY shared/eTicketing.Contracts/*.csproj shared/eTicketing.Contracts/
COPY shared/eTicketing.Shared.Auth/*.csproj shared/eTicketing.Shared.Auth/
COPY shared/eTicketing.Shared.Storage/*.csproj shared/eTicketing.Shared.Storage/
COPY services/ticketing/eTicketing.Ticketing.Data/*.csproj services/ticketing/eTicketing.Ticketing.Data/
COPY services/ticketing/eTicketing.Ticketing.Business/*.csproj services/ticketing/eTicketing.Ticketing.Business/
COPY services/ticketing/eTicketing.Ticketing.Api/*.csproj services/ticketing/eTicketing.Ticketing.Api/
RUN dotnet restore services/ticketing/eTicketing.Ticketing.Api/eTicketing.Ticketing.Api.csproj

COPY shared/eTicketing.Contracts/ shared/eTicketing.Contracts/
COPY shared/eTicketing.Shared.Auth/ shared/eTicketing.Shared.Auth/
COPY shared/eTicketing.Shared.Storage/ shared/eTicketing.Shared.Storage/
COPY services/ticketing/ services/ticketing/
RUN dotnet publish services/ticketing/eTicketing.Ticketing.Api/eTicketing.Ticketing.Api.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "eTicketing.Ticketing.Api.dll"]
