FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY shared/eTicketing.Contracts/*.csproj shared/eTicketing.Contracts/
COPY shared/eTicketing.Shared.Storage/*.csproj shared/eTicketing.Shared.Storage/
COPY services/pdfgeneration/eTicketing.PdfGeneration/*.csproj services/pdfgeneration/eTicketing.PdfGeneration/
RUN dotnet restore services/pdfgeneration/eTicketing.PdfGeneration/eTicketing.PdfGeneration.csproj

COPY shared/eTicketing.Contracts/ shared/eTicketing.Contracts/
COPY shared/eTicketing.Shared.Storage/ shared/eTicketing.Shared.Storage/
COPY services/pdfgeneration/ services/pdfgeneration/
RUN dotnet publish services/pdfgeneration/eTicketing.PdfGeneration/eTicketing.PdfGeneration.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
# QuestPDF renders text through SkiaSharp, which needs real font files and libfontconfig — the
# aspnet image ships neither, so every generated PDF would come out with empty glyph boxes where
# the Bosnian text should be. fonts-liberation specifically (not DejaVu) because fontconfig aliases
# Arial -> Liberation Sans and Courier New -> Liberation Mono, which is exactly what TicketDocument
# asks for: those two names then resolve identically here and on a Windows dev machine, with full
# coverage of the Latin Extended-A diacritics the Bosnian copy needs.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libfontconfig1 fontconfig fonts-liberation \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "eTicketing.PdfGeneration.dll"]
