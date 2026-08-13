#!/usr/bin/env bash
# Emits nimbus_cert_expiry_seconds{domain=...} for every certificate Caddy manages,
# read by node-exporter's textfile collector. Alerted on in infra/observability/alert.rules.yml
# (NimbusCertificateExpiringSoon). Install as a systemd timer — see infra/VPS-SETUP.md#e3-install-the-host-side-observability-scripts-and-timers.
set -euo pipefail

OUT=/var/lib/node_exporter/textfile_collector/nimbus_cert_expiry.prom
TMP="${OUT}.$$"
CERT_ROOT=/srv/nimbus/data/caddy/data/caddy/certificates

{
  echo '# HELP nimbus_cert_expiry_seconds Unix timestamp when a Caddy-managed certificate expires.'
  echo '# TYPE nimbus_cert_expiry_seconds gauge'
  if [ -d "$CERT_ROOT" ]; then
    while IFS= read -r -d '' crt; do
      domain=$(basename "$crt" .crt)
      expiry=$(openssl x509 -enddate -noout -in "$crt" | cut -d= -f2)
      expiry_epoch=$(date -d "$expiry" +%s 2>/dev/null || date -j -f "%b %d %T %Y %Z" "$expiry" +%s)
      echo "nimbus_cert_expiry_seconds{domain=\"${domain}\"} ${expiry_epoch}"
    done < <(find "$CERT_ROOT" -type f -name "*.crt" -print0)
  fi
} > "$TMP"
mv "$TMP" "$OUT"
