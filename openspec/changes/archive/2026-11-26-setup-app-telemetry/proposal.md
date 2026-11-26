# Proposal

## Why

With the centralized telemetry infrastructure (OpenTelemetry Collector, Prometheus, Tempo, Loki, Grafana) provisioned in `setup-observability-infra`, the TicketReserver.IO platform now requires application-level instrumentation. Microservices and background workers currently log to standard console output without distributed trace context, and custom domain metrics (such as Redis lock acquisition latency, Saga completion/failure counters, and outbox lag) are not yet exposed. Instrumenting the services with OpenTelemetry SDK enables full-stack observability, end-to-end W3C trace propagation across Kafka and HTTP, and automatic dashboard feeding in Grafana.

## What Changes

- Introduce a shared telemetry bootstrapping module or reusable extension for Web APIs and Workers providing standardized OpenTelemetry registration.
- Configure OTLP exporters for traces, metrics, and logs targeting the OpenTelemetry Collector via environment variables (`OTEL_EXPORTER_OTLP_ENDPOINT`, `OTEL_SERVICE_NAME`).
- Implement distributed trace context propagation over Apache Kafka message headers (`traceparent`, `tracestate`) matching W3C Trace Context specifications, ensuring continuous traces from Web APIs through asynchronous workers (`TR.HW.*`).
- Instrument HTTP requests (ASP.NET Core / HttpClient), PostgreSQL queries (Npgsql), Redis commands (StackExchange.Redis), and background worker loops.
- Expose standardized domain and technical metrics meters:
  - `ticketreserver_redis_lock_acquire_duration_seconds` (histogram for `SET NX EX` latency).
  - `ticketreserver_reservations_active_total` (gauge for reservations awaiting payment).
  - `ticketreserver_saga_events_total` (counter for `payment.v1.approved`, `ticketing.v1.issued`, `ticketing.v1.failed`).
  - `ticketreserver_seat_collision_refunds_total` (counter for auto-refund triggers).
  - `ticketreserver_outbox_pending_messages` (gauge for pending PostgreSQL outbox backlog).
- Configure Serilog / OpenTelemetry logging to inject `TraceId` and `SpanId` into structured log events for bidirectional drill-down in Grafana Loki and Tempo.
- Update Docker Compose service definitions with required OpenTelemetry environment variables.

## Capabilities

### New Capabilities
- `application-telemetry`: Standardized application-level distributed tracing, metrics emission, and structured log correlation for .NET microservices and workers.

### Modified Capabilities
<!-- None -->

## Impact

- **Affected Code**: `TR.API.*` Web APIs and `TR.HW.*` Background Workers will register OpenTelemetry tracing, metrics, and logging.
- **Dependencies**: Adds OpenTelemetry NuGet packages (`OpenTelemetry.Extensions.Hosting`, `OpenTelemetry.Instrumentation.AspNetCore`, `OpenTelemetry.Instrumentation.Http`, `OpenTelemetry.Exporter.OpenTelemetryProtocol`, `OpenTelemetry.Instrumentation.Runtime`, `Serilog.Sinks.OpenTelemetry` or native OTel logging).
- **Docker Compose**: Sets `OTEL_EXPORTER_OTLP_ENDPOINT` and `OTEL_SERVICE_NAME` for all microservice containers in compose files.
