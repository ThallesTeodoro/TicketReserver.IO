# Tasks

## 1. Core Telemetry Foundations and W3C Propagation

- [x] 1.1 Define shared diagnostic definitions (`TicketReserverDiagnostics`) including ActivitySources (`TicketReserver.Kafka`, `TicketReserver.Http`) and Meter instruments (`ticketreserver_redis_lock_acquire_duration_seconds`, `ticketreserver_reservations_active_total`, `ticketreserver_saga_events_total`, `ticketreserver_seat_collision_refunds_total`, `ticketreserver_outbox_pending_messages`), and verify compilation
- [x] 1.2 Implement W3C TraceContext propagation helpers for Kafka message headers (injecting and extracting `traceparent` and `tracestate`) and verify with dedicated unit tests

## 2. Web APIs Telemetry Instrumentation

- [x] 2.1 Add OpenTelemetry packages and register tracing, metrics, and Serilog OTLP/trace enrichment in `TR.API.Reservation`, instrumenting Redis lock duration and active reservations metrics, and verify with `dotnet test TR.API.Reservation/tests/TR.API.Reservation.UnitTests`
- [x] 2.2 Register OpenTelemetry tracing, metrics, and log enrichment in `TR.API.Events` and `TR.API.Search`, and verify with `dotnet test TR.API.Events/tests/TR.API.Events.UnitTests` and `dotnet test TR.API.Search/tests/TR.API.Search.UnitTests`

## 3. Background Workers and Saga Trace Propagation

- [x] 3.1 Instrument `TR.HW.ReservationOutboxRelay` and `TR.HW.Payment` with Kafka W3C trace propagation, outbox backlog gauge, and payment approved/refunded saga metrics, and verify with their unit test suites
- [x] 3.2 Instrument `TR.HW.Ticketing`, `TR.HW.Refund`, and `TR.HW.ResCompensator` with Kafka trace propagation, auto-refund compensation counters, and ticketing success/failure metrics, and verify with their unit test suites
- [x] 3.3 Instrument `TR.HW.ResTimeout`, `TR.HW.Notification`, and `TR.HW.Repl` with OpenTelemetry and trace propagation, and verify with their unit test suites

## 4. Compose Environment and Global Validation

- [x] 4.1 Update service definitions in `docker-compose.yml` and included compose files with `OTEL_EXPORTER_OTLP_ENDPOINT` (`http://otel-collector:4317`) and service-specific `OTEL_SERVICE_NAME`, and verify whole orchestration using `docker compose config`
- [x] 4.2 Run complete unit test suite across all 11 repository solutions ensuring 100% test pass rate with invariant globalization
- [x] 4.3 Update telemetry and APM documentation in `README.md` and `.agents/specification/infra/` detailing exported metrics and W3C tracing architecture, and verify markdown links

## Workflow follow-up

- Run `openspec validate setup-app-telemetry --strict` to verify spec compliance.
- Apply the change via `/openspec-apply-change setup-app-telemetry`.
- Archive the change after implementation and validation are complete.
