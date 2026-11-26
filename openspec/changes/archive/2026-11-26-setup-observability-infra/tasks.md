# Tasks

## 1. OpenTelemetry Collector Setup

- [x] 1.1 Create `docker/observability/otel-collector/otel-collector-config.yaml` with OTLP gRPC (:4317) and HTTP (:4318) receivers, memory limiter, batch, and resource processors, and Tempo, Prometheus, and Loki exporters, and verify file format and syntax against OpenTelemetry Collector schema
- [x] 1.2 Define `otel-collector` service in `docker/observability/docker-compose.yml` with ports 4317 and 4318 exposed and verify compose syntax using `docker compose config`

## 2. Metrics and Traces Backends (Prometheus & Tempo)

- [x] 2.1 Create `docker/observability/prometheus/prometheus.yml` configured to scrape `otel-collector:8889` every 5 seconds under job `otel-collector` and verify Prometheus configuration file syntax
- [x] 2.2 Create `docker/observability/tempo/tempo.yaml` configured for local trace retention and OTLP gRPC ingestion, and verify configuration syntax
- [x] 2.3 Define `prometheus` (port 9090) and `tempo` (port 3200) services and persistent volumes in `docker/observability/docker-compose.yml`, and verify compose configuration with `docker compose -f docker/observability/docker-compose.yml config`

## 3. Structured Logging and Grafana Provisioning

- [x] 3.1 Create `docker/observability/loki/loki-config.yaml` configured for single-node filesystem storage and structured log ingestion on port 3100, and verify configuration syntax
- [x] 3.2 Create `docker/observability/grafana/provisioning/datasources/datasources.yaml` with Prometheus, Tempo, and Loki datasources configured with bidirectional trace-to-log correlation (`tracesToLogsV2` and `derivedFields`), and verify YAML schema
- [x] 3.3 Create `docker/observability/grafana/provisioning/dashboards/dashboards.yaml` and dashboard definition `saga-and-business-metrics.json` visualizing seat lock latency (Redis `SET NX EX`), reservation checkout throughput, Saga auto-refund rates, and outbox lag, and verify JSON structure
- [x] 3.4 Define `loki` (port 3100) and `grafana` (port 3000) services in `docker/observability/docker-compose.yml` mounting the provisioning directories and volumes, and verify compose configuration

## 4. Root Orchestration and Validation

- [x] 4.1 Register `./docker/observability/docker-compose.yml` under `include:` in root `docker-compose.yml` and declare network `ticketreserver-telemetry-net`, and verify whole repository configuration with `docker compose config`
- [x] 4.2 Create smoke-test verification script `docker/observability/verify-observability.sh` that checks container readiness and health endpoints for OTel Collector, Prometheus, Tempo, Loki, and Grafana, and verify execution permissions and correctness
- [x] 4.3 Update infrastructure documentation in `README.md` and `.agents/specification/infra/` detailing telemetry ports, Grafana credentials, and dashboard access, and verify markdown links

## Workflow follow-up

- Run `openspec validate setup-observability-infra --strict` to verify spec compliance.
- Apply the change via `/openspec-apply-change setup-observability-infra`.
- Archive the change after implementation and validation are complete.
