FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY shared/eTicketing.Contracts/*.csproj shared/eTicketing.Contracts/
COPY shared/eTicketing.Shared.Auth/*.csproj shared/eTicketing.Shared.Auth/
COPY shared/eTicketing.Shared.TicketPdf/*.csproj shared/eTicketing.Shared.TicketPdf/
COPY services/ticketing/eTicketing.Ticketing.Data/*.csproj services/ticketing/eTicketing.Ticketing.Data/
COPY services/ticketing/eTicketing.Ticketing.Business/*.csproj services/ticketing/eTicketing.Ticketing.Business/
COPY services/ticketing/eTicketing.Ticketing.Api/*.csproj services/ticketing/eTicketing.Ticketing.Api/
RUN dotnet restore services/ticketing/eTicketing.Ticketing.Api/eTicketing.Ticketing.Api.csproj

COPY shared/eTicketing.Contracts/ shared/eTicketing.Contracts/
COPY shared/eTicketing.Shared.Auth/ shared/eTicketing.Shared.Auth/
COPY shared/eTicketing.Shared.TicketPdf/ shared/eTicketing.Shared.TicketPdf/
COPY services/ticketing/ services/ticketing/
RUN dotnet publish services/ticketing/eTicketing.Ticketing.Api/eTicketing.Ticketing.Api.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
# GET /tickets/{id}/pdf renders the buyer's ticket here rather than serving a stored file, so this
# image needs the same SkiaSharp prerequisites as eTicketing.PdfGeneration: libfontconfig must be
# present even though every face the sheet draws with is embedded in the assembly, or the renderer
# fails outright. fonts-liberation is a last-resort fallback for uncovered glyphs only.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libfontconfig1 fontconfig fonts-liberation \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "eTicketing.Ticketing.Api.dll"]
