FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY shared/eTicketing.Contracts/*.csproj shared/eTicketing.Contracts/
COPY shared/eTicketing.Shared.Storage/*.csproj shared/eTicketing.Shared.Storage/
COPY services/notifications/eTicketing.Notifications/*.csproj services/notifications/eTicketing.Notifications/
RUN dotnet restore services/notifications/eTicketing.Notifications/eTicketing.Notifications.csproj

COPY shared/eTicketing.Contracts/ shared/eTicketing.Contracts/
COPY shared/eTicketing.Shared.Storage/ shared/eTicketing.Shared.Storage/
COPY services/notifications/ services/notifications/
RUN dotnet publish services/notifications/eTicketing.Notifications/eTicketing.Notifications.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "eTicketing.Notifications.dll"]
