# Pinned to a specific Cumulative Update for reproducible builds. The floating
# "2022-latest" tag silently re-resolves to a newer CU on every pull, which can
# trigger unexpected "script upgrade mode" boots on existing volumes. Bump
# deliberately by editing this tag and re-testing the entrypoint wait loop.
FROM mcr.microsoft.com/mssql/server:2022-CU15-ubuntu-22.04

USER root

COPY entrypoint.sh /entrypoint.sh
COPY init.sql /scripts/init.sql
COPY init_dw.sql /scripts/init_dw.sql
COPY copilot_readonly.sql /scripts/copilot_readonly.sql

RUN chmod +x /entrypoint.sh && sed -i 's/\r//' /entrypoint.sh

USER mssql

ENTRYPOINT ["/bin/bash", "/entrypoint.sh"]
