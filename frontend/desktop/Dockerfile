# =============================================================================
# eTicketing desktop (Flutter) — builds a Linux release bundle inside Docker.
#
# NOTE: Windows (.exe) and macOS (.app) builds require their native toolchain
# (Visual Studio Build Tools / Xcode) and are NOT possible from a Linux
# container. Build those natively on Windows/macOS with `flutter build
# windows` / `flutter build macos`. This image only produces the Linux
# artifact.
#
# The final stage is not a running app — it just holds the built bundle so
# `docker compose run --rm desktop-build` can copy it out to ./dist/desktop.
# =============================================================================

FROM ghcr.io/cirruslabs/flutter:stable AS build
WORKDIR /app

RUN apt-get update && apt-get install -y --no-install-recommends \
      clang cmake ninja-build pkg-config libgtk-3-dev liblzma-dev \
    && rm -rf /var/lib/apt/lists/*
RUN flutter config --enable-linux-desktop

COPY pubspec.yaml pubspec.lock ./
RUN flutter pub get

COPY . .

ARG DESKTOP_API_BASE_URL=http://localhost:5000/api/
ARG DESKTOP_BUILD_MODE=release

RUN flutter build linux --${DESKTOP_BUILD_MODE} \
      --dart-define=API_BASE_URL=${DESKTOP_API_BASE_URL} \
    && mkdir -p /out_bundle \
    && cp -r build/linux/x64/${DESKTOP_BUILD_MODE}/bundle/* /out_bundle/

# ---- Export stage -------------------------------------------------------------
FROM busybox:stable AS export
COPY --from=build /out_bundle /artifacts
CMD ["sh", "-c", "cp -rv /artifacts/* /out/ && echo 'Linux desktop bundle copied to ./dist/desktop'"]
