#!/usr/bin/env bash
set -euo pipefail

QTD="${1:-50000}"
PARALELOS="${2:-1}"
IM="${3:-10000027}"
DIR="$(cd "$(dirname "$0")/.." && pwd)"
SERIE="${4:-M$(date +%M%S)}"
SAIDA="$DIR/saida/medicao-$SERIE"
SQL="docker compose exec -T oracle sqlplus -s NFSE_OWNER/nfse_dev_123@//localhost:1521/FREEPDB1"
mkdir -p "$SAIDA"

for i in $(seq 1 "$PARALELOS"); do
  dotnet run --project "$DIR/GeradorLote" -- "$QTD" --im "$IM" --serie "$SERIE" --inicio $(( (i - 1) * QTD + 1 )) --lote "$i" \
    --saida "$SAIDA/lote$i.xml" > /dev/null
done
TAMANHO=$(du -m "$SAIDA/lote1.xml" | cut -f1)

docker compose restart webservices > /dev/null 2>&1
until curl -sf http://127.0.0.1:8080/health > /dev/null; do sleep 1; done
CONTAINER=$(docker compose ps -q webservices)
LIMITE=$(docker inspect -f '{{.HostConfig.Memory}}' "$CONTAINER")

: > "$SAIDA/memoria.txt"
( while docker inspect -f '{{.State.Running}}' "$CONTAINER" 2>/dev/null | grep -q true; do
    docker stats --no-stream --format '{{.MemUsage}}' "$CONTAINER" | cut -d/ -f1 >> "$SAIDA/memoria.txt" || true
  done ) &
AMOSTRADOR=$!

INICIO=$(date +%s)
ENVIOS=()
for i in $(seq 1 "$PARALELOS"); do
  "$DIR/enviar-lote.sh" "$SAIDA/lote$i.xml" > "$SAIDA/resposta$i.xml" 2>&1 &
  ENVIOS+=($!)
done
wait "${ENVIOS[@]}" || true
RECEPCAO=$(( $(date +%s) - INICIO ))
PROTOCOLOS=$(grep -ho 'NC[0-9]\{14\}' "$SAIDA"/resposta*.xml | sed "s/.*/'&'/" | paste -sd, - || true)

RESULTADO="sem protocolo"
if [ -n "$PROTOCOLOS" ]; then
  while true; do
    if ! docker inspect -f '{{.State.Running}}' "$CONTAINER" | grep -q true; then
      RESULTADO="webservices caiu (OOMKilled=$(docker inspect -f '{{.State.OOMKilled}}' "$CONTAINER"))"
      break
    fi
    PENDENTES=$(printf "SET HEADING OFF FEEDBACK OFF\nSELECT COUNT(*) FROM LOTES_RPS WHERE PROTOCOLO IN (%s) AND SITUACAO < 3;\nEXIT\n" "$PROTOCOLOS" | $SQL | tr -d '[:space:]')
    if [ "$PENDENTES" = "0" ]; then
      RESULTADO="processado"
      break
    fi
    sleep 2
  done
fi
TOTAL=$(( $(date +%s) - INICIO ))
{ kill "$AMOSTRADOR"; wait "$AMOSTRADOR"; } 2> /dev/null || true

PICO=$(awk '{ v = $1 + 0; if ($1 ~ /GiB/) v *= 1024; else if ($1 ~ /KiB/) v /= 1024; if (v > m) m = v } END { printf "%.0f", m }' "$SAIDA/memoria.txt")
printf "lotes: %s x %s RPS (%s MB cada), limite: %s MiB\n" "$PARALELOS" "$QTD" "$TAMANHO" "$(( LIMITE / 1048576 ))"
printf "recepcao: %ss, total: %ss, pico de memoria: %s MiB\n" "$RECEPCAO" "$TOTAL" "$PICO"
printf "resultado: %s\n" "$RESULTADO"
if [ -n "$PROTOCOLOS" ]; then
  printf "SET LINES 200 FEEDBACK OFF\nSELECT PROTOCOLO, QTD_RPS, SITUACAO FROM LOTES_RPS WHERE PROTOCOLO IN (%s);\nEXIT\n" "$PROTOCOLOS" | $SQL
fi
docker compose logs --since "${TOTAL}s" webservices | grep -E "lote|falha" | sed 's/^[^|]*| //' | tail -10 || true
