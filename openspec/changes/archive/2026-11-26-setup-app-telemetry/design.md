# Design

## Context

TicketReserver.IO consists of 3 Web APIs (`TR.API.Events`, `TR.API.Reservation`, `TR.API.Search`) and 8 Background Workers (`TR.HW.*`) running on .NET 10. The telemetry backends (OpenTelemetry Collector, Prometheus, Tempo, Loki, Grafana) have been provisioned in `setup-observability-infra`. Currently, microservices format logs to standard console without distributed trace headers, and domain metrics visualized in the Grafana dashboards (`saga-and-business-metrics.json`) are not yet emitted by the applications.

## Goals / Non-Goals

**Goals:**
- Provide a standardized OpenTelemetry bootstrapping pattern (`AddTicketReserverTelemetry`) across all Web APIs and Workers.
- Implement W3C trace propagation (`traceparent`, `tracestate`) across Apache Kafka message headers using standard .NET `ActivitySource` and `ActivityContext`.
- Instrument HTTP endpoints (ASP.NET Core / HttpClient), PostgreSQL queries (Npgsql), Redis commands (StackExchange.Redis), and background message loops.
- Expose domain meters (`System.Diagnostics.Metrics.Meter`) matching the pre-provisioned Grafana dashboard queries:
  - `ticketreserver_redis_lock_acquire_duration_seconds` (histogram)
  - `ticketreserver_reservations_active_total` (gauge)
  - `ticketreserver_saga_events_total` (counter)
  - `ticketreserver_seat_collision_refunds_total` (counter)
  - `ticketreserver_outbox_pending_messages` (gauge)
- Ensure all structured logs emitted via Serilog / ILogger carry `TraceId` and `SpanId` for instant Tempo <-> Loki drill-down.
- Configure `OTEL_EXPORTER_OTLP_ENDPOINT` and `OTEL_SERVICE_NAME` across Docker Compose files.

**Non-Goals:**
- Modifying core domain models, business validation rules, or database schemas.
- Adding third-party proprietary APM vendor agents (Datadog, New Relic) — rely exclusively on standard OpenTelemetry .NET SDK and .NET BCL `System.Diagnostics`.

## Decisions

### 1. Unified Telemetry Configuration Pattern
- **Decision**: Implement a reusable telemetry extension method pattern (`AddTicketReserverTelemetry`) registering OpenTelemetry Tracing, Metrics, and Logging.
- **Rationale**: Keeps configuration DRY and consistent across all 11 services while respecting the Clean Architecture boundaries (infrastructure/presentation layer).
- **Alternatives Considered**: Duplicating 40 lines of OTel builder configuration in every `Program.cs`. Rejected to prevent configuration drift.

### 2. Kafka Distributed Tracing via W3C TraceContext
- **Decision**: Use a custom `ActivitySource("TicketReserver.Kafka")` helper with standard W3C text map propagation:
  - On Producer: Inject `traceparent` (and `tracestate` if present) into Kafka `Headers`.
  - On Consumer: Extract `traceparent` from Kafka `Headers`, parse the `ActivityContext`, and start a consumer span as a child of the producer span.
- **Rationale**: `Confluent.Kafka` lacks built-in OpenTelemetry automatic instrumentation. Manual W3C header injection ensures traces span seamlessly from the checkout API through the saga workers (`TR.HW.ReservationOutboxRelay` -> `TR.HW.Payment` -> `TR.HW.Ticketing` / `TR.HW.Refund`).
- **Alternatives Considered**: Trace propagation via Kafka payload message body. Rejected because it mutates domain event contracts and breaks schema independence.

### 3. Metric Instruments via `System.Diagnostics.Metrics`
- **Decision**: Define a static `TicketReserverDiagnostics` class providing `Meter("TicketReserver")` instruments matching the exact metric names expected by Prometheus and the Grafana dashboard.
- **Rationale**: Uses native .NET BCL `System.Diagnostics.Metrics` API without tight coupling to OpenTelemetry exporter implementations.
- **Alternatives Considered**: Prometheus-net client library. Rejected in favor of modern, standard OpenTelemetry metrics.

### 4. Structured Logging Enrichment
- **Decision**: Configure Serilog and Microsoft.Extensions.Logging with trace enrichment (`TraceId`, `SpanId`) and OTLP log exporter targeting the OTel Collector.
- **Rationale**: Fulfills the bidirectional correlation requirement configured in Loki (`derivedFields` regex matching `traceparent`) and Tempo (`tracesToLogsV2`).

## Risks / Trade-offs

- **[Risk] Unreachable OTel Collector in unit tests or local dev** → [Mitigation] OTel SDK by design does not throw unhandled exceptions when exporters fail; export operations buffer and time out asynchronously without crashing the application.
- **[Risk] Performance overhead from high-frequency Kafka spans** → [Mitigation] Use batching processors with 1-second timeout and lightweight activity creation.
