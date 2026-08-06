FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY shared/eTicketing.Contracts/*.csproj shared/eTicketing.Contracts/
COPY shared/eTicketing.Shared.Auth/*.csproj shared/eTicketing.Shared.Auth/
COPY services/identity/eTicketing.Identity.Data/*.csproj services/identity/eTicketing.Identity.Data/
COPY services/identity/eTicketing.Identity.Business/*.csproj services/identity/eTicketing.Identity.Business/
COPY services/identity/eTicketing.Identity.Api/*.csproj services/identity/eTicketing.Identity.Api/
RUN dotnet restore services/identity/eTicketing.Identity.Api/eTicketing.Identity.Api.csproj

COPY shared/eTicketing.Contracts/ shared/eTicketing.Contracts/
COPY shared/eTicketing.Shared.Auth/ shared/eTicketing.Shared.Auth/
COPY services/identity/ services/identity/
RUN dotnet publish services/identity/eTicketing.Identity.Api/eTicketing.Identity.Api.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "eTicketing.Identity.Api.dll"]
