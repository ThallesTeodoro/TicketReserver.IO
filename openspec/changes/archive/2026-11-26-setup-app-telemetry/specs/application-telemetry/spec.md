# Spec Delta

## Purpose

Enables end-to-end distributed tracing, custom domain metrics, and trace-correlated structured logging across all TicketReserver.IO .NET microservices and background workers.

## ADDED Requirements

### Requirement: Standardized OTLP Telemetry Exporter Configuration
The system SHALL configure all Web APIs and background workers to export traces, metrics, and structured logs to the OpenTelemetry Collector via standard OpenTelemetry Protocol (OTLP) gRPC or HTTP endpoints configured through environment variables OTEL_EXPORTER_OTLP_ENDPOINT and OTEL_SERVICE_NAME.

#### Scenario: Telemetry Export via Environment Variables
- **WHEN** a service or worker starts with OTEL_EXPORTER_OTLP_ENDPOINT and OTEL_SERVICE_NAME defined
- **THEN** it automatically registers OpenTelemetry providers and streams telemetry to the specified collector endpoint

### Requirement: W3C Distributed Trace Context Propagation across HTTP and Kafka
The system SHALL inject and extract W3C Trace Context headers (traceparent and tracestate) across incoming/outgoing HTTP calls and Apache Kafka message headers, linking user-facing API operations to asynchronous worker transactions and compensation sagas within continuous distributed traces.

#### Scenario: End-to-End Trace Propagation from API to Worker
- **WHEN** a client submits a checkout request to the Reservation API and an event is published to Kafka
- **THEN** the published Kafka message includes traceparent headers, and consuming workers resume the active trace to maintain context continuity across the saga

### Requirement: Domain and Resilience Metrics Instrumentation
The system SHALL instrument custom domain and resilience meters that record Redis seat-hold lock acquisition duration (histogram), active reservations awaiting payment (gauge), Saga event throughput (counter), seat collision compensations (counter), and pending outbox backlog (gauge).

#### Scenario: Redis Lock Acquisition Duration Measurement
- **WHEN** the Reservation API acquires or fails to acquire a seat-hold lock in Redis using SET NX EX
- **THEN** it records the acquisition duration in the ticketreserver_redis_lock_acquire_duration_seconds metric histogram

#### Scenario: Saga Event and Refund Metric Recording
- **WHEN** saga workers process payment approval, ticket issuance, or ticketing failure events
- **THEN** they record the event count in ticketreserver_saga_events_total and increment ticketreserver_seat_collision_refunds_total when an auto-refund compensation occurs

### Requirement: Correlated Structured Logging with Distributed Trace Identifiers
The system SHALL enrich all structured application log events with active TraceId and SpanId attributes, enabling direct bidirectional drill-down between Grafana Tempo distributed traces and Grafana Loki log streams.

#### Scenario: Log Emission with Injected Trace Context
- **WHEN** an application log is emitted during an active request or message processing span
- **THEN** the log entry contains TraceId and SpanId formatted for W3C compatibility and Loki derivedFields extraction
