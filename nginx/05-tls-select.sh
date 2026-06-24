#!/bin/sh
# Docker entrypoint script (runs before nginx starts, via the official image's
# /docker-entrypoint.d/*.sh mechanism). Picks the TLS or HTTP-only config based
# on the ENABLE_TLS env var and writes it to /etc/nginx/nginx.conf.
#
# Runs as 05 so the stock 10-listen-on-ipv6-by-default.sh sees the chosen
# config and can add `listen [::]:80;` to it (a no-op for nginx.tls.conf since
# the redirect server already declares it).
set -e

if [ "${ENABLE_TLS}" = "true" ]; then
  echo "[nginx] ENABLE_TLS=true — using TLS config (443 + 80→443 redirect)."
  cp /etc/nginx/nginx.tls.conf /etc/nginx/nginx.conf
else
  echo "[nginx] ENABLE_TLS not set — using HTTP-only config (port 80)."
  cp /etc/nginx/nginx.http.conf /etc/nginx/nginx.conf
fi
