FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY shared/eTicketing.Contracts/*.csproj shared/eTicketing.Contracts/
COPY services/pdfgeneration/eTicketing.PdfGeneration/*.csproj services/pdfgeneration/eTicketing.PdfGeneration/
RUN dotnet restore services/pdfgeneration/eTicketing.PdfGeneration/eTicketing.PdfGeneration.csproj

COPY shared/eTicketing.Contracts/ shared/eTicketing.Contracts/
COPY services/pdfgeneration/ services/pdfgeneration/
RUN dotnet publish services/pdfgeneration/eTicketing.PdfGeneration/eTicketing.PdfGeneration.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "eTicketing.PdfGeneration.dll"]
