# ─── Build stage ──────────────────────────────────────────────
FROM node:18-alpine AS build
WORKDIR /app

# Install deps from lockfile for reproducible builds.
# Falls back to `npm install` if no lockfile is present yet (early dev).
COPY package*.json ./
RUN if [ -f package-lock.json ]; then npm ci; else npm install; fi

COPY . .
RUN npm run build

# ─── Runtime stage ────────────────────────────────────────────
# nginx-unprivileged runs as non-root (UID 101) and listens on 8080 by default.
FROM nginxinc/nginx-unprivileged:alpine AS final

COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html

EXPOSE 8080
CMD ["nginx", "-g", "daemon off;"]
