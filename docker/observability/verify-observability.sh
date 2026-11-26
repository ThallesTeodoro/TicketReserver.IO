#!/usr/bin/env bash
set -euo pipefail

# Script de validação e smoke test da stack de observabilidade do TicketReserver.IO
# Pode ser executado em modo estático (--dry-run / --config-check) ou contra containers em execução.

MODE="${1:-}"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"

echo "=== [TicketReserver.IO] Validando Arquivos de Configuração de Observabilidade ==="

# 1. Validação YAML/JSON dos arquivos de provisionamento
python3 -c "import yaml; yaml.safe_load(open('${SCRIPT_DIR}/otel-collector/otel-collector-config.yaml'))"
echo "✓ OTel Collector Config: YAML válido"

python3 -c "import yaml; yaml.safe_load(open('${SCRIPT_DIR}/prometheus/prometheus.yml'))"
echo "✓ Prometheus Config: YAML válido"

python3 -c "import yaml; yaml.safe_load(open('${SCRIPT_DIR}/tempo/tempo.yaml'))"
echo "✓ Tempo Config: YAML válido"

python3 -c "import yaml; yaml.safe_load(open('${SCRIPT_DIR}/loki/loki-config.yaml'))"
echo "✓ Loki Config: YAML válido"

python3 -c "import yaml; yaml.safe_load(open('${SCRIPT_DIR}/grafana/provisioning/datasources/datasources.yaml'))"
echo "✓ Grafana Datasources: YAML válido"

python3 -c "import yaml; yaml.safe_load(open('${SCRIPT_DIR}/grafana/provisioning/dashboards/dashboards.yaml'))"
echo "✓ Grafana Dashboards Provisioning: YAML válido"

python3 -m json.tool "${SCRIPT_DIR}/grafana/dashboards/saga-and-business-metrics.json" > /dev/null
echo "✓ Grafana Dashboard JSON: JSON válido"

# 2. Validação Docker Compose
cd "${REPO_ROOT}"
docker compose -f docker-compose.yml config > /dev/null
echo "✓ Docker Compose: Validação de orquestração bem-sucedida"

if [[ "${MODE}" == "--dry-run" || "${MODE}" == "--config-check" ]]; then
  echo "=== Modo verificação de configuração concluído com sucesso! ==="
  exit 0
fi

# 3. Verificação de Saúde dos Endpoints (quando os containers estiverem em execução)
check_endpoint() {
  local service_name="$1"
  local url="$2"
  local expected_code="${3:-200}"

  echo -n "Testando ${service_name} (${url})... "
  if curl -s -o /dev/null -w "%{http_code}" --connect-timeout 2 "${url}" | grep -q "${expected_code}"; then
    echo "ONLINE (HTTP ${expected_code})"
    return 0
  else
    echo "OFFLINE (Container não está rodando ou indisponível)"
    return 1
  fi
}

echo "=== [TicketReserver.IO] Verificando Endpoints de Observabilidade (Runtime) ==="
check_endpoint "Grafana Health" "http://localhost:3000/api/health" "200" || true
check_endpoint "Prometheus Ready" "http://localhost:9090/-/ready" "200" || true
check_endpoint "Tempo Ready" "http://localhost:3200/ready" "200" || true
check_endpoint "Loki Ready" "http://localhost:3100/ready" "200" || true

echo "=== Verificação concluída ==="
