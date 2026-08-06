# =============================================================================
# eTicketing web (Angular SSR) — built and served entirely inside Docker.
# =============================================================================

# ---- Build stage ------------------------------------------------------------
FROM node:22-alpine AS build
WORKDIR /app

COPY package.json package-lock.json ./
RUN npm ci

COPY . .

# Reserved for once the app reads it via src/environments/*.ts — harmless no-op today.
ARG WEB_API_BASE_URL=http://localhost:5000/api
ENV WEB_API_BASE_URL=${WEB_API_BASE_URL}

RUN npm run build

# ---- Runtime stage -----------------------------------------------------------
FROM node:22-alpine AS runtime
WORKDIR /app
ENV NODE_ENV=production

COPY --from=build /app/dist/web ./dist/web

EXPOSE 4000
CMD ["node", "dist/web/server/server.mjs"]
