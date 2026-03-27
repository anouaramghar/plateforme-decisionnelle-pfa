FROM mcr.microsoft.com/mssql/server:2022-latest

USER root

COPY entrypoint.sh /entrypoint.sh
COPY init.sql /scripts/init.sql
COPY init_dw.sql /scripts/init_dw.sql

RUN chmod +x /entrypoint.sh && sed -i 's/\r//' /entrypoint.sh

USER mssql

ENTRYPOINT ["/bin/bash", "/entrypoint.sh"]
