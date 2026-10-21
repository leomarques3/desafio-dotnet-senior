#!/usr/bin/env bash
set -euo pipefail

LOTES="${1:-4}"
RPS="${2:-500}"
IM="${3:-10000027}"
SERIE="F$(date +%M%S)"
DIR="$(cd "$(dirname "$0")" && pwd)"
SAIDA="$DIR/saida/carga-$SERIE"
mkdir -p "$SAIDA"

for i in $(seq 1 "$LOTES"); do
  inicio=$(( (i - 1) * RPS / 2 + 1 ))
  dotnet run --project "$DIR/GeradorLote" -- "$RPS" --im "$IM" --serie "$SERIE" --inicio "$inicio" --lote "$i" \
    --saida "$SAIDA/lote$i.xml" > /dev/null
done

for i in $(seq 1 "$LOTES"); do
  "$DIR/enviar-lote.sh" "$SAIDA/lote$i.xml" > "$SAIDA/resposta$i.xml" &
done
wait

echo "serie $SERIE, $LOTES lotes de $RPS RPS, faixas sobrepostas pela metade, prestador $IM"
grep -ho 'NC[0-9]\{14\}' "$SAIDA"/resposta*.xml
