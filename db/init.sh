#!/bin/bash
# Executado pelo entrypoint da imagem gvenzl/oracle-free (initdb.d) na primeira subida.
set -e
export NLS_LANG=.AL32UTF8
CONN="NFSE_OWNER/${APP_USER_PASSWORD}@//localhost:1521/FREEPDB1"
INICIO=$(date +%s)
for f in 01_schema 02_pkg_guia 03_pkg_nfse 04_triggers 05_seed_base 06_seed_volume; do
    if [ "$f" = "06_seed_volume" ] && [ "${NFSE_SEED_VOLUME:-1}" = "0" ]; then
        echo "[init] $f.sql ignorado (NFSE_SEED_VOLUME=0)"
        continue
    fi
    echo "[init] $f.sql"
    sqlplus -s -L "$CONN" @/opt/nfse/db/$f.sql
done
for m in $(ls /opt/nfse/db/migrations/V*.sql 2>/dev/null | grep -v '_rollback\.sql$' | sort -V); do
    echo "[init] migrations/$(basename "$m")"
    sqlplus -s -L "$CONN" @"$m"
done
echo "[init] concluido em $(( $(date +%s) - INICIO ))s"
