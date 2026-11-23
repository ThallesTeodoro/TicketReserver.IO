#!/usr/bin/env bash
set -e

CONNECT_URL="${CONNECT_URL:-http://localhost:8083}"
CONNECTOR_CONFIG="${CONNECTOR_CONFIG:-/debezium-postgres-catalog.json}"

echo "==> Aguardando Debezium Connect estar pronto em $CONNECT_URL..."

max_retries=30
count=0
until curl -s "$CONNECT_URL/connectors" > /dev/null 2>&1; do
    count=$((count + 1))
    if [ "$count" -ge "$max_retries" ]; then
        echo "ERRO: Debezium Connect não respondeu após $max_retries tentativas."
        exit 1
    fi
    echo "Debezium Connect ainda não está pronto ($count/$max_retries). Aguardando 3s..."
    sleep 3
done

echo "==> Debezium Connect pronto. Verificando conector catalog-postgres-connector..."

STATUS_CODE=$(curl -s -o /dev/null -w "%{http_code}" "$CONNECT_URL/connectors/catalog-postgres-connector")

if [ "$STATUS_CODE" -eq 200 ]; then
    echo "==> Conector catalog-postgres-connector já registrado. Pulando registro."
else
    echo "==> Registrando conector catalog-postgres-connector..."
    curl -s -X POST "$CONNECT_URL/connectors" -H 'Content-Type: application/json' -d @"$CONNECTOR_CONFIG"
    echo ""
    echo "==> Conector catalog-postgres-connector registrado com sucesso!"
fi
