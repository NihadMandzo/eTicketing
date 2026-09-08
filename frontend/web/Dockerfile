# --- Stage 1: Build the application ---
FROM node:22-alpine AS build
WORKDIR /app

COPY package.json package-lock.json ./
RUN npm ci

COPY . .

ARG NG_APP_API_BASE_URL=http://localhost:5000/api
ENV NG_APP_API_BASE_URL=${NG_APP_API_BASE_URL}
ARG NG_APP_GOOGLE_MAPS_API_KEY=
ENV NG_APP_GOOGLE_MAPS_API_KEY=${NG_APP_GOOGLE_MAPS_API_KEY}

# Diagnostic only — makes the value actually baked into this image visible in the build log,
# since a wrong/missing --build-arg silently falls back to the ARG default above with no other
# signal. Remove once the CORS-to-localhost issue this was added to chase is confirmed fixed.
RUN echo "Building with NG_APP_API_BASE_URL=${NG_APP_API_BASE_URL}"

RUN npm run build

# --- Stage 2: Serve the application using Node ---
FROM node:22-alpine AS runner
WORKDIR /app

# Copy the entire server and browser build output distribution
COPY --from=build /app/dist ./dist

EXPOSE 4000
ENV PORT=4000

# Start the compiled Node server instead of looking for static index.html files
CMD ["node", "dist/web/server/main.mjs"]
