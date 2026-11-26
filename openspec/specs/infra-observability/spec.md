# infra-observability Specification

## Purpose

Provides a centralized, production-grade telemetry pipeline for TicketReserver.IO using OpenTelemetry Collector, Prometheus, Grafana Tempo, Grafana Loki, and Grafana.

## Requirements

### Requirement: Unified OTLP Telemetry Ingestion via OpenTelemetry Collector
The system SHALL provide an OpenTelemetry Collector service that accepts OpenTelemetry Protocol (OTLP) telemetry over gRPC (port 4317) and HTTP (port 4318), applies memory limiter and batch processors, and routes traces to Tempo, metrics to Prometheus, and structured logs to Loki.

#### Scenario: Telemetry Ingestion and Multi-Backend Routing
- **WHEN** applications export OTLP traces, metrics, or logs to the OpenTelemetry Collector on ports 4317 or 4318
- **THEN** the collector accepts the payload, applies memory limits and batching, and forwards traces to Tempo (tempo:4317), exposes metrics on prometheus endpoint (:8889), and pushes logs to Loki (http://loki:3100/loki/api/v1/push)

### Requirement: Time-Series Metrics Scraping via Prometheus
The system SHALL provide a Prometheus service configured to scrape time-series metrics from the OpenTelemetry Collector prometheus endpoint at a 5-second interval and expose the query UI and API on port 9090.

#### Scenario: Periodic Metrics Scraping
- **WHEN** the Prometheus container is running and healthy
- **THEN** it scrapes http://otel-collector:8889 every 5 seconds and retains system and application metrics for dashboard querying

### Requirement: Distributed Trace Storage and Querying via Grafana Tempo
The system SHALL provide a Grafana Tempo service that receives distributed traces via OTLP from the OpenTelemetry Collector and exposes query endpoints on port 3200.

#### Scenario: Distributed Trace Ingestion and Query Interface
- **WHEN** the OpenTelemetry Collector exports trace batches to tempo:4317
- **THEN** Tempo stores the trace spans locally and makes them queryable by TraceID on HTTP port 3200

### Requirement: Structured Log Ingestion via Grafana Loki
The system SHALL provide a Grafana Loki service that receives structured application and platform logs pushed by the OpenTelemetry Collector on HTTP port 3100 and exposes LogQL query capabilities.

#### Scenario: Structured Log Push and Query
- **WHEN** the OpenTelemetry Collector pushes structured logs to http://loki:3100/loki/api/v1/push
- **THEN** Loki indexes log labels (service_name, job, environment) and makes log streams queryable via LogQL on port 3100

### Requirement: Correlated Telemetry Visualization and Dashboards via Grafana
The system SHALL provide a Grafana service on port 3000 with pre-configured datasource provisioning for Prometheus, Tempo, and Loki, establishing bidirectional trace-to-logs correlation, and pre-configured dashboards for Reservation APM, Saga health, Redis lock contention, and Kafka/Outbox lag.

#### Scenario: Automated Datasource Provisioning with Trace-to-Log Correlation
- **WHEN** Grafana starts up
- **THEN** it automatically provisions Prometheus as default datasource, Tempo with tracesToLogsV2 linking to Loki, and Loki with derivedFields linking traceparent regex to Tempo TraceID

#### Scenario: Out-of-the-Box Domain and Technical Dashboards
- **WHEN** an engineer accesses Grafana on port 3000
- **THEN** pre-loaded dashboards visualize seat lock p95/p99 latency (Redis SET NX EX), reservation checkout throughput, Saga completion vs failure rates (ticketing.v1.issued vs ticketing.v1.failed), and Outbox pending message backlog

### Requirement: Telemetry Network Isolation and Compose Orchestration
The system SHALL define the observability stack within a modular Docker Compose configuration under docker/observability/docker-compose.yml attached to ticketreserver-telemetry-net and ticketreserver-internal-net, and registered in the root docker-compose.yml via the include directive.

#### Scenario: Modular Compose Inclusion and Network Connectivity
- **WHEN** docker compose up is executed from the repository root
- **THEN** the observability services start and communicate over ticketreserver-telemetry-net while allowing microservices on ticketreserver-internal-net to send OTLP data to otel-collector:4317 and otel-collector:4318
