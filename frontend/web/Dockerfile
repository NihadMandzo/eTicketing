# =============================================================================
# eTicketing web (Angular SSR) — built and served entirely inside Docker.
# =============================================================================

# ---- Build stage ------------------------------------------------------------
FROM node:22-alpine AS build
WORKDIR /app

COPY package.json package-lock.json ./
RUN npm ci

COPY . .

# Read by @ngx-env/builder during `npm run build` and baked into the bundle. The repo-root .env is
# outside this build context, so process.env is the only way the values can arrive here — hence the
# ARG -> ENV pass-through. docker-compose supplies both from that same .env.
ARG NG_APP_API_BASE_URL=http://localhost:5000/api
ENV NG_APP_API_BASE_URL=${NG_APP_API_BASE_URL}

# Empty by default: no key means the product-details map renders a labelled placeholder rather than
# Google's grey "for development purposes only" tile. Pass a real key to get a real map.
ARG NG_APP_GOOGLE_MAPS_API_KEY=
ENV NG_APP_GOOGLE_MAPS_API_KEY=${NG_APP_GOOGLE_MAPS_API_KEY}

RUN npm run build

# ---- Runtime stage -----------------------------------------------------------
FROM node:22-alpine AS runtime
WORKDIR /app
ENV NODE_ENV=production

COPY --from=build /app/dist/web ./dist/web

EXPOSE 4000
CMD ["node", "dist/web/server/server.mjs"]
