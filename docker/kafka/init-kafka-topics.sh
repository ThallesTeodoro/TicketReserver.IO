#!/usr/bin/env bash
set -e

BOOTSTRAP_SERVER="${KAFKA_BOOTSTRAP_SERVER:-kafka:9092}"

echo "==> Aguardando Kafka estar pronto em $BOOTSTRAP_SERVER..."

max_retries=30
count=0
until kafka-topics --bootstrap-server "$BOOTSTRAP_SERVER" --list > /dev/null 2>&1; do
    count=$((count + 1))
    if [ "$count" -ge "$max_retries" ]; then
        echo "ERRO: Kafka não respondeu após $max_retries tentativas."
        exit 1
    fi
    echo "Kafka ainda não está pronto ($count/$max_retries). Aguardando 2s..."
    sleep 2
done

echo "==> Kafka está pronto. Iniciando provisionamento determinístico de tópicos..."

# Tópicos de Negócio e Reconciliação (3 partições, replication-factor 1)
DOM_TOPICS=(
    "reservation.v1.checkout_req"
    "payment.v1.approved"
    "payment.v1.refunded"
    "ticketing.v1.issued"
    "ticketing.v1.failed"
    "reservation.v1.hold_created.delayed"
)

for topic in "${DOM_TOPICS[@]}"; do
    echo "Criando/verificando tópico de domínio: $topic (partições: 3)"
    kafka-topics --create --if-not-exists \
        --bootstrap-server "$BOOTSTRAP_SERVER" \
        --partitions 3 \
        --replication-factor 1 \
        --topic "$topic"
done

# Dead Letter Queues (DLQs) Obrigatórias (1 partição, replication-factor 1)
DLQ_TOPICS=(
    "reservation.v1.checkout_req.DLQ"
    "payment.v1.approved.DLQ"
    "payment.v1.refunded.DLQ"
    "ticketing.v1.issued.DLQ"
    "ticketing.v1.failed.DLQ"
)

for topic in "${DLQ_TOPICS[@]}"; do
    echo "Criando/verificando tópico de DLQ: $topic (partições: 1)"
    kafka-topics --create --if-not-exists \
        --bootstrap-server "$BOOTSTRAP_SERVER" \
        --partitions 1 \
        --replication-factor 1 \
        --topic "$topic"
done

echo "==> Provisionamento de tópicos concluído com sucesso!"
echo "==> Tópicos existentes no cluster:"
kafka-topics --bootstrap-server "$BOOTSTRAP_SERVER" --list
