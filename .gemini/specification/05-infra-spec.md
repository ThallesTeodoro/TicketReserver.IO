# Especificação de Infraestrutura e Observabilidade (05-infra-spec.md)

Este documento define os requisitos técnicos para a criação do ambiente Docker Compose, Kubernetes e Dashboards de monitoramento.

## 1. Topologia de Redes e Containers (Docker Compose)
O ambiente de desenvolvimento local deve isolar os serviços na rede interna `ticketreserver-net`.

*   **Bancos de Dados:**
    *   **Postgres (Porta 5432):** Deve inicializar com três databases lógicas distintas via scripts de init: `reservation_db`, `payment_db` e `ticketing_db`.
    *   **Redis Stack (Portas 6379 / 8001):** Modulo RedisJSON ativado para cache de idempotência.
*   **Mensageria:**
    *   **Kafka (Porta 9092):** Rodando em modo KRaft (sem Zookeeper). Deve auto-criar os tópicos especificados em `02-contracts.md`.
    *   **AKHQ (Porta 8085):** Conectado ao cluster local para visualização de filas e DLQs.

## 2. Pipeline de Telemetria (OpenTelemetry)
O fluxo de dados de observabilidade deve seguir rigorosamente a arquitetura de coleta distribuída:

```text
[Aplicações .NET] --(OTLP / gRPC)--> [OTel Collector]
                                          ├──> [Grafana Loki] (Logs)
                                          ├──> [Grafana Tempo] (Traces)
                                          └──> [Prometheus] (Metrics)
```

Configurações do OTel Collector:
*   **Receivers**: otlp ativo para protocolos gRPC (porta 4317) e HTTP (porta 4318).
*   **Exporters**:
    *   `loki` enviando para `http://loki:3100/loki/api/v1/push`.
    *   `otlp/tempo` enviando traces via gRPC para a porta 4117 do Tempo.
    *   `prometheus` expondo a porta 8889 para scraping de métricas.

## 3. Requisitos de Dashboards (Grafana)
O container do Grafana deve ser provisionado via provisioning scripts automáticos para incluir:

*   **Data Sources Pré-configurados**: Loki, Tempo e Prometheus conectados por padrão.
*   **Trace-to-Logs Integration**: Configurar o Grafana para que, ao clicar em um TraceID no Tempo, ele abra automaticamente os logs correspondentes no Loki filtrados por aquele exato ID.