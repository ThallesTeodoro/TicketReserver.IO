# Proposal

## Why

The TicketReserver.IO platform consists of 11 distributed microservices communicating asynchronously over Apache Kafka, PostgreSQL, and Redis. Without a centralized telemetry pipeline, diagnosing latency bottlenecks, monitoring seat-hold lock contention, and tracing distributed sagas across services is difficult and error-prone. Provisioning the full-stack observability infrastructure (OpenTelemetry Collector, Prometheus, Grafana Tempo, Grafana Loki, and Grafana) as specified in `infra-spec.md` provides the unified telemetry foundation required for production-readiness and end-to-end distributed tracing.

## What Changes

- Provision OpenTelemetry Collector service in `docker/otel-collector/` configured with OTLP receivers (gRPC `:4317` and HTTP `:4318`), batch/resource processors, and exporters routing traces to Grafana Tempo, metrics to Prometheus, and structured logs to Grafana Loki.
- Provision Prometheus service in `docker/prometheus/` configured with a 5-second scrape interval pulling metrics from the OTel Collector prometheus exporter endpoint (`:8889`).
- Provision Grafana Tempo service in `docker/tempo/` configured for local retention and fast trace retrieval.
- Provision Grafana Loki service in `docker/loki/` configured for structured log ingestion.
- Provision Grafana service in `docker/grafana/` on port `:3000` with automated datasource provisioning (`datasources.yaml`) establishing bidirectional trace-to-logs correlation (`Tempo` <-> `Loki`) and pre-configured dashboards for Reservation Saga monitoring, seat lock latency (Redis `SET NX EX`), and Kafka consumer/outbox lag.
- Configure dedicated Docker network `ticketreserver-telemetry-net` and integrate the modular compose files into the root `docker-compose.yml` via the `include:` directive.

## Capabilities

### New Capabilities
- `infra-observability`: Distributed observability infrastructure providing unified ingestion of traces, metrics, and logs via OpenTelemetry Collector, Prometheus, Grafana Tempo, Grafana Loki, and Grafana dashboards with trace-to-log correlation.

### Modified Capabilities
<!-- None -->

## Impact

- **Infrastructure**: Adds 5 containerized services (`otel-collector`, `prometheus`, `tempo`, `loki`, `grafana`) under `docker/` connected to `ticketreserver-telemetry-net` and `ticketreserver-internal-net`.
- **Host Ports**: Exposes Grafana (`:3000`), Prometheus (`:9090`), Tempo (`:3200`), Loki (`:3100`), and OTel Collector (`:4317` / `:4318`) for local development and application telemetry ingestion.
- **Root Compose**: Registers the new modular compose files in root `docker-compose.yml`.
