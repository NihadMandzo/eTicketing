FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY shared/eTicketing.Contracts/*.csproj shared/eTicketing.Contracts/
COPY shared/eTicketing.Shared.Messaging/*.csproj shared/eTicketing.Shared.Messaging/
COPY shared/eTicketing.Shared.TicketPdf/*.csproj shared/eTicketing.Shared.TicketPdf/
COPY services/pdfgeneration/eTicketing.PdfGeneration/*.csproj services/pdfgeneration/eTicketing.PdfGeneration/
RUN dotnet restore services/pdfgeneration/eTicketing.PdfGeneration/eTicketing.PdfGeneration.csproj

COPY shared/eTicketing.Contracts/ shared/eTicketing.Contracts/
COPY shared/eTicketing.Shared.Messaging/ shared/eTicketing.Shared.Messaging/
COPY shared/eTicketing.Shared.TicketPdf/ shared/eTicketing.Shared.TicketPdf/
COPY services/pdfgeneration/ services/pdfgeneration/
RUN dotnet publish services/pdfgeneration/eTicketing.PdfGeneration/eTicketing.PdfGeneration.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
# QuestPDF renders through SkiaSharp, which needs libfontconfig present even though every face the
# ticket draws with is embedded in the assembly (Manrope and IBM Plex Mono — see
# eTicketing.Shared.TicketPdf.TicketTheme). Without it the renderer fails outright rather than
# substituting. fonts-liberation is kept only as a last-resort fallback for any glyph the embedded
# subsets don't cover.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libfontconfig1 fontconfig fonts-liberation \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "eTicketing.PdfGeneration.dll"]
