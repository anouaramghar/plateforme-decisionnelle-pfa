# Tiny wrapper around the official nginx image that bakes in the HTTP and TLS
# configs plus the selector entrypoint. Compose then only needs to mount certs.
FROM nginx:1.27-alpine

COPY nginx.http.conf /etc/nginx/nginx.http.conf
COPY nginx.tls.conf  /etc/nginx/nginx.tls.conf
COPY locations.conf  /etc/nginx/locations.conf
COPY 05-tls-select.sh /docker-entrypoint.d/05-tls-select.sh

RUN chmod +x /docker-entrypoint.d/05-tls-select.sh
