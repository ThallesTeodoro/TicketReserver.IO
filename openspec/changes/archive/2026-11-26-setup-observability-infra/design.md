# Design

## Context

TicketReserver.IO is an asynchronous, event-driven distributed system consisting of 11 microservices and background workers communicating over PostgreSQL, Redis, and Apache Kafka. See `proposal.md` for motivation. Currently, individual services write logs to the console and metrics/traces are not centralized. Section 5 of `.agents/specification/infra/infra-spec.md` specifies an end-to-end telemetry architecture using OpenTelemetry Collector as the unified gateway, routing metrics to Prometheus, traces to Grafana Tempo, and logs to Grafana Loki, visualized via Grafana with bidirectional correlation.

## Goals / Non-Goals

**Goals:**
- Provide a unified OTLP receiver (gRPC `:4317` and HTTP `:4318`) for all services via OpenTelemetry Collector.
- Store time-series metrics in Prometheus (`:9090`) scraped from OTel Collector (`:8889`) at a 5-second interval.
- Store distributed traces in Grafana Tempo (`:3200`) and structured logs in Grafana Loki (`:3100`).
- Automate Grafana (`:3000`) configuration with file-based provisioning for datasources (with bidirectional Tempo <-> Loki trace-to-log drill-down) and dashboards for Saga monitoring, seat lock latency, and outbox backlog.
- Encapsulate the infrastructure inside `docker/observability/docker-compose.yml` and register it in root `docker-compose.yml`.

**Non-Goals:**
- Modifying application code or .NET project dependencies to emit telemetry (this is handled in Phase 2: `setup-app-telemetry`).
- Production cloud deployment (e.g. AWS EKS / Managed Prometheus / Grafana Cloud) — configuration is tuned for deterministic local development in Docker.
- Long-term multi-terabyte log retention or remote S3/GCS object storage backends for Tempo/Loki (local filesystem storage is sufficient for local development).

## Decisions

### 1. Unified Gateway via OpenTelemetry Collector vs Direct Service Backends
- **Decision**: All microservices will exclusively export telemetry via standard OTLP (`otel-collector:4317` / `4318`). The Collector fans out to Tempo, Prometheus, and Loki.
- **Rationale**: Decouples application code from specific storage backends. If Tempo is swapped for Jaeger or Loki for OpenSearch in production, zero application code changes are required.
- **Alternatives Considered**: Direct exporter libraries in each service (e.g., Prometheus C# client, Serilog Loki sink, Jaeger exporter). Rejected because it bloats application dependencies and duplicates batching/filtering logic.

### 2. Prometheus Pull Architecture over OTel Collector Exporter
- **Decision**: Configure OpenTelemetry Collector with the `prometheus` exporter on `0.0.0.0:8889` under the `ticketreserver` namespace, and configure Prometheus to scrape this target every 5 seconds.
- **Rationale**: Standard Prometheus pull model ensures robust scrape status checks, targets introspection, and reliable time-series aggregation adhering to `infra-spec.md` Section 5.2.
- **Alternatives Considered**: OTLP push directly into Prometheus (PRW). Scrape model is simpler to diagnose and debug locally via Prometheus target status UI.

### 3. Bidirectional Trace-to-Log Correlation in Grafana
- **Decision**: Configure Grafana datasources via `datasources.yaml` with:
  1. Tempo `tracesToLogsV2` pointing to Loki with `{service_name="$${__service.name}"} |= "$${__trace.id}"`.
  2. Loki `derivedFields` with regex `traceparent=00-([0-9a-fA-F]{32})-` extracting the `TraceID` and linking directly to Tempo.
- **Rationale**: Allows an engineer inspecting a failed checkout span to jump with a single click to the exact application log entries, and conversely jump from an error log to the distributed waterfall trace.

### 4. Modular Compose Layout
- **Decision**: Place all observability configuration in `docker/observability/`:
  - `docker/observability/docker-compose.yml`
  - `docker/observability/otel-collector/otel-collector-config.yaml`
  - `docker/observability/prometheus/prometheus.yml`
  - `docker/observability/tempo/tempo.yaml`
  - `docker/observability/loki/loki-config.yaml`
  - `docker/observability/grafana/provisioning/datasources/datasources.yaml`
  - `docker/observability/grafana/provisioning/dashboards/dashboards.yaml`
  - `docker/observability/grafana/dashboards/saga-and-business-metrics.json`
- **Rationale**: Keeps the observability stack cohesive, cleanly separated from transactional databases and brokers, while adhering to Docker Compose 2.20+ `include:` mechanism already used in root `docker-compose.yml`.

## Risks / Trade-offs

- **Memory overhead on development machines** → Mitigation: Enable `memory_limiter` processor in OTel Collector (75% limit, 20% spike limit) and tune local container resource limits so the entire telemetry stack stays under ~800MB RAM.
- **High disk growth from local trace and log files** → Mitigation: Configure Loki and Tempo with local retention window (24h) and volume mounts that can be pruned with standard `docker compose down -v`.
- **Grafana provisioning syntax strictness** → Mitigation: Use exact YAML specifications matching Grafana 10.x/11.x schema and validate with automated smoke tests during verification.
