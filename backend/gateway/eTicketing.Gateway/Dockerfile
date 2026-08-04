FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY gateway/eTicketing.Gateway/*.csproj gateway/eTicketing.Gateway/
RUN dotnet restore gateway/eTicketing.Gateway/eTicketing.Gateway.csproj

COPY gateway/eTicketing.Gateway/ gateway/eTicketing.Gateway/
RUN dotnet publish gateway/eTicketing.Gateway/eTicketing.Gateway.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "eTicketing.Gateway.dll"]
