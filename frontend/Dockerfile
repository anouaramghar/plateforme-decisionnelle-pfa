# ─── Build stage ──────────────────────────────────────────────
# Node 20 LTS — pinned to a specific minor for reproducible builds.
# Node 18 went into maintenance-only in 2025.
FROM node:20.18-alpine AS build
WORKDIR /app

# Install deps from lockfile for reproducible builds.
# Falls back to `npm install` if no lockfile is present yet (early dev).
COPY package*.json ./
RUN if [ -f package-lock.json ]; then npm ci --legacy-peer-deps; else npm install --legacy-peer-deps; fi

COPY . .
RUN npm run build

# ─── Runtime stage ────────────────────────────────────────────
# nginx-unprivileged runs as non-root (UID 101) and listens on 8080 by default.
# Pinned to a specific minor to match the outer nginx (nginx:1.27-alpine).
FROM nginxinc/nginx-unprivileged:1.27-alpine AS final

COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html

EXPOSE 8080
CMD ["nginx", "-g", "daemon off;"]
