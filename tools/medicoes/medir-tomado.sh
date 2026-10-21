#!/usr/bin/env bash
set -euo pipefail

CNPJ="${1:-01234567000195}"
INI="${2:-2026-10-01}"
FIM="${3:-2026-10-31}"
VEZES="${4:-20}"
URL="${5:-http://127.0.0.1:8080/NfseWS.asmx}"

PEDIDO="&lt;ConsultarNfseServicoTomadoEnvio&gt;&lt;Consulente&gt;&lt;CpfCnpj&gt;&lt;Cnpj&gt;$CNPJ&lt;/Cnpj&gt;&lt;/CpfCnpj&gt;&lt;/Consulente&gt;&lt;PeriodoEmissao&gt;&lt;DataInicial&gt;$INI&lt;/DataInicial&gt;&lt;DataFinal&gt;$FIM&lt;/DataFinal&gt;&lt;/PeriodoEmissao&gt;&lt;Pagina&gt;1&lt;/Pagina&gt;&lt;/ConsultarNfseServicoTomadoEnvio&gt;"
ENVELOPE="<?xml version=\"1.0\" encoding=\"utf-8\"?><soap:Envelope xmlns:soap=\"http://schemas.xmlsoap.org/soap/envelope/\"><soap:Body><ConsultarNfseServicoTomadoRequest xmlns=\"http://notacarioca.rio.gov.br/\"><inputXML>$PEDIDO</inputXML></ConsultarNfseServicoTomadoRequest></soap:Body></soap:Envelope>"

TEMPOS=()
for i in $(seq 1 "$VEZES"); do
  SAIDA=$(curl -sS -o /tmp/medir-tomado.$$ -w '%{http_code} %{time_total}' -X POST "$URL" \
    -H 'Content-Type: text/xml; charset=utf-8' \
    -H 'SOAPAction: "http://notacarioca.rio.gov.br/ConsultarNfseServicoTomado"' \
    --data-binary "$ENVELOPE")
  TEMPOS+=("${SAIDA#* }")
done
NOTAS=$(grep -o '&lt;CompNfse&gt;' /tmp/medir-tomado.$$ | wc -l | tr -d ' ')
rm -f /tmp/medir-tomado.$$

printf "%s\n" "${TEMPOS[@]}" | awk -v cnpj="$CNPJ" -v ini="$INI" -v fim="$FIM" -v notas="$NOTAS" -v http="${SAIDA%% *}" '
  { t = $1 * 1000; s += t; if (NR == 1 || t < min) min = t; if (t > max) max = t }
  END { printf "tomador %s, %s a %s: %d chamadas, HTTP %s, %s notas na pagina 1, media %.0f ms, min %.0f ms, max %.0f ms\n", cnpj, ini, fim, NR, http, notas, s / NR, min, max }'
