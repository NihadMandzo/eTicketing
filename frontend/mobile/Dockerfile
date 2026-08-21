# =============================================================================
# eTicketing mobile (Flutter) — builds an Android release .apk inside Docker.
#
# NOTE: iOS builds require a macOS host/toolchain and are NOT possible from a
# Linux container. This image only produces the Android artifact.
#
# The final stage is not a running app — it just holds the built .apk so
# `docker compose run --rm mobile-build` can copy it out to ./dist/mobile.
# =============================================================================

FROM ghcr.io/cirruslabs/flutter:stable AS build
WORKDIR /app

COPY pubspec.yaml pubspec.lock ./
RUN flutter pub get

COPY . .

ARG MOBILE_API_BASE_URL=http://10.0.2.2:5000/api/
ARG MOBILE_BUILD_MODE=release

# Read by android/app/build.gradle.kts (System.getenv("MAPS_API_KEY")) to fill the
# ${mapsApiKey} manifest placeholder google_maps_flutter needs — see AndroidManifest.xml.
ARG GOOGLE_MAPS_API_KEY=YOUR_GOOGLE_MAPS_API_KEY
ENV MAPS_API_KEY=${GOOGLE_MAPS_API_KEY}

RUN flutter build apk --${MOBILE_BUILD_MODE} \
      --dart-define=API_BASE_URL=${MOBILE_API_BASE_URL}

# ---- Export stage -------------------------------------------------------------
FROM busybox:stable AS export
COPY --from=build /app/build/app/outputs/flutter-apk /artifacts
CMD ["sh", "-c", "cp -v /artifacts/*.apk /out/ && echo 'APK copied to ./dist/mobile'"]
