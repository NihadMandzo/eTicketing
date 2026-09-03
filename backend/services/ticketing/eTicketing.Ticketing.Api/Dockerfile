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
#
# libomp5 is for the AI Uvidi tab. ML.NET's SSA forecaster and spike detector go through Intel
# MKL, and Microsoft.ML.Mkl.Redist ships the Intel OpenMP runtime that its libMklImports.so links
# against (libiomp5) for **Windows only** — the linux-x64 asset in that package has no OpenMP
# runtime beside it at all. Without this, libMklImports.so fails to dlopen with
# "libiomp5.so: cannot open shared object file", every SSA fit throws DllNotFoundException, and the
# forecaster silently spends its life on the heuristic rung — the feature appears to work while
# never once using a model, which is the worst way for this to fail.
#
# LLVM's libomp is ABI-compatible with Intel's libiomp5, so the symlink is the supported way to
# satisfy that link. Written as an explicit symlink rather than an LD_PRELOAD or an env var so it
# is visible in the image and survives any entrypoint change.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libfontconfig1 fontconfig fonts-liberation libomp5-18 \
    && ln -sf /usr/lib/x86_64-linux-gnu/libomp.so.5 /usr/lib/x86_64-linux-gnu/libiomp5.so \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "eTicketing.Ticketing.Api.dll"]
