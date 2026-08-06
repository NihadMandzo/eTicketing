# eTicketing

## Running the stack (Docker)

Everything — backend microservices, infrastructure (SQL Server, RabbitMQ, Redis),
and the Angular web app — is built and run from one root [docker-compose.yml](docker-compose.yml),
configured by one root `.env` file.

1. Copy the env template and fill in the placeholders (backend secrets, at minimum):
   ```
   copy .env.example .env
   ```
2. Build and start everything:
   ```
   docker compose up -d --build
   ```
   - Gateway: http://localhost:5000
   - Web app: http://localhost:4200
   - RabbitMQ management UI: http://localhost:15672

### Mobile (Android APK) and desktop (Linux bundle)

Flutter apps can't run as long-lived Docker services, but they **are** built by
Docker — as one-shot jobs gated behind the `build` compose profile:

```
docker compose --profile build run --rm mobile-build     # -> ./dist/mobile/app-release.apk
docker compose --profile build run --rm desktop-build    # -> ./dist/desktop/
```

Limitations (see `.env.example` for details): Docker can only produce an
**Android APK** for mobile (no macOS host for iOS) and a **Linux bundle** for
desktop (Windows/macOS builds need their native toolchain and must be built
natively with `flutter build windows` / `flutter build macos`).

All configuration for every part of the stack (backend / web / mobile / desktop)
lives in the single root `.env` file — see `.env.example` for the full list of
variables and what each one feeds.
