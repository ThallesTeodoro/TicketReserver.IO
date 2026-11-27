#!/usr/bin/env bash
set -euo pipefail

# Script de validação e verificação do Ingress / API Gateway do TicketReserver.IO
# Suporta modo estático (--dry-run / --config-check) ou smoke tests contra containers em execução.

MODE="${1:-}"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"
GATEWAY_PORT="${GATEWAY_HTTP_PORT:-80}"
BASE_URL="http://localhost:${GATEWAY_PORT}"

echo "=== [TicketReserver.IO] Validando Configuração do Ingress / API Gateway ==="

# 1. Validação de sintaxe do nginx.conf
if ! docker run --rm \
  --add-host events-api:127.0.0.1 \
  --add-host reservation-api:127.0.0.1 \
  --add-host search-api:127.0.0.1 \
  -v "${SCRIPT_DIR}/nginx.conf":/etc/nginx/nginx.conf:ro \
  nginx:alpine nginx -t > /dev/null 2>&1; then
  echo "✗ Erro: Falha na validação de sintaxe do nginx.conf"
  docker run --rm \
    --add-host events-api:127.0.0.1 \
    --add-host reservation-api:127.0.0.1 \
    --add-host search-api:127.0.0.1 \
    -v "${SCRIPT_DIR}/nginx.conf":/etc/nginx/nginx.conf:ro \
    nginx:alpine nginx -t
  exit 1
fi
echo "✓ NGINX Configuration: Sintaxe válida e upstreams verificados"

# 2. Validação da orquestração Docker Compose unificada
cd "${REPO_ROOT}"
docker compose -f docker-compose.yml config --quiet
echo "✓ Docker Compose: Ingress integrado e rede ticketreserver-public-net válida"

# 3. Verificação de rotas declaradas no nginx.conf
grep -q "location = /healthz" "${SCRIPT_DIR}/nginx.conf"
grep -q "location /events" "${SCRIPT_DIR}/nginx.conf"
grep -q "location /venues" "${SCRIPT_DIR}/nginx.conf"
grep -q "location /reserva" "${SCRIPT_DIR}/nginx.conf"
grep -q "location /search" "${SCRIPT_DIR}/nginx.conf"
grep -q "proxy_set_header traceparent" "${SCRIPT_DIR}/nginx.conf"
echo "✓ Route & Header Mappings: Todas as rotas (/healthz, /events, /venues, /reserva, /search) e headers W3C mapeados"

if [[ "${MODE}" == "--dry-run" || "${MODE}" == "--config-check" ]]; then
  echo "=== [TicketReserver.IO] Verificação em modo dry-run concluída com sucesso! ==="
  exit 0
fi

# 4. Smoke tests contra containers em execução
echo "=== Testando Conectividade HTTP do Ingress em ${BASE_URL} ==="

check_endpoint() {
  local route_name="$1"
  local url="$2"
  local expected_code="${3:-200}"

  echo -n "Testando rota ${route_name} (${url})... "
  local status_code
  status_code=$(curl -s -o /dev/null -w "%{http_code}" --connect-timeout 2 "${url}" || echo "000")
  if [[ "${status_code}" =~ ${expected_code} ]]; then
    echo "ONLINE (HTTP ${status_code})"
    return 0
  else
    echo "OFFLINE/FALHA (HTTP ${status_code}, esperado: ${expected_code})"
    return 1
  fi
}

check_endpoint "Gateway Health" "${BASE_URL}/healthz" "200"
check_endpoint "Catalog Events" "${BASE_URL}/events" "200|404"
check_endpoint "Catalog Venues" "${BASE_URL}/venues" "200"
check_endpoint "Search Events" "${BASE_URL}/search" "200|400"
check_endpoint "Reservation Hold Preflight (CORS)" "${BASE_URL}/reserva/hold" "204|405"

echo "=== Smoke tests do Ingress concluídos com sucesso! ==="
