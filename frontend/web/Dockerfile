# --- Stage 1: Build the application ---
FROM node:22-alpine AS build
WORKDIR /app

COPY package.json package-lock.json ./
RUN npm ci

COPY . .

# NG_APP_API_BASE_URL is gone: the browser now calls a same-origin, relative /api that this image's
# own Node server proxies to the Gateway, so there is no longer an API address to bake in at build
# time. Its replacement, GATEWAY_INTERNAL_URL, is a *runtime* variable read by server.ts — set it on
# the container, not here.
ARG NG_APP_GOOGLE_MAPS_API_KEY=
ENV NG_APP_GOOGLE_MAPS_API_KEY=${NG_APP_GOOGLE_MAPS_API_KEY}

RUN npm run build

# --- Stage 2: Serve the application using Node ---
FROM node:22-alpine AS runner
WORKDIR /app

# Copy the entire server and browser build output distribution
COPY --from=build /app/dist ./dist

EXPOSE 4000
ENV PORT=4000

# Where server.ts proxies /api. Overridden per environment (compose sets the service name, Azure
# Container Apps the Gateway's internal FQDN); the default here keeps `docker run` of this image
# alone working against a sibling container called `gateway`.
ENV GATEWAY_INTERNAL_URL=http://gateway:8080

# Start the compiled Node server instead of looking for static index.html files
CMD ["node", "dist/web/server/server.mjs"]
