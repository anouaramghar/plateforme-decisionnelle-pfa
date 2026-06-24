#!/usr/bin/env bash
# Generate a self-signed TLS certificate for local / demo use.
#
# DO NOT use this in production — browsers will warn about the self-signed
# cert and there is no chain of trust. For production, use a real cert from
# Let's Encrypt (certbot) or your institutional CA, and place it at
# nginx/certs/fullchain.pem + nginx/certs/privkey.pem.
#
# Usage:
#   ./nginx/generate-dev-certs.sh [hostname]
#   ./nginx/generate-dev-certs.sh plateforme.eniad.ma
#
# Then set ENABLE_TLS=true in .env and `docker compose up --build`.
set -euo pipefail

HOST="${1:-localhost}"
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
CERT_DIR="$SCRIPT_DIR/certs"

mkdir -p "$CERT_DIR"

if [ -f "$CERT_DIR/fullchain.pem" ] && [ -f "$CERT_DIR/privkey.pem" ]; then
  echo "Existing certs found in $CERT_DIR — leaving them in place."
  echo "Delete them first to regenerate."
  exit 0
fi

openssl req -x509 -nodes -newkey rsa:2048 -days 825 \
  -keyout "$CERT_DIR/privkey.pem" \
  -out    "$CERT_DIR/fullchain.pem" \
  -subj   "/CN=${HOST}" \
  -addext "subjectAltName=DNS:${HOST},DNS:*.${HOST},IP:127.0.0.1"

echo ""
echo "Self-signed cert written to $CERT_DIR (CN=${HOST})."
echo "Next steps:"
echo "  1. Set ENABLE_TLS=true in .env"
echo "  2. docker compose up --build"
echo "  3. Visit https://${HOST} (accept the self-signed warning in your browser)."
