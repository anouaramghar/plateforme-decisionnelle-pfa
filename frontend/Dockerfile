# ─── Build stage ──────────────────────────────────────────────
# Node 20 LTS — Node 18 went into maintenance-only in 2025.
FROM node:20-alpine AS build
WORKDIR /app

# Install deps from lockfile for reproducible builds.
# Falls back to `npm install` if no lockfile is present yet (early dev).
COPY package*.json ./
RUN if [ -f package-lock.json ]; then npm ci --legacy-peer-deps; else npm install --legacy-peer-deps; fi

COPY . .
RUN npm run build

# ─── Runtime stage ────────────────────────────────────────────
# nginx-unprivileged runs as non-root (UID 101) and listens on 8080 by default.
FROM nginxinc/nginx-unprivileged:alpine AS final

COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html

EXPOSE 8080
CMD ["nginx", "-g", "daemon off;"]
