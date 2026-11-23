# Design

## Context

In the **TicketReserver.IO** architecture (`arch-decisions.md` and `infra-spec.md`), `TR.HW.Notification` (`SVC_NOTIFICACAO`) is the final downstream consumer in both the happy-path ticketing saga and the compensation/auto-refund saga. While `TR.HW.ResCompensator` reconciles the internal reservation state in `reservation_db`, `TR.HW.Notification` operates in an independent consumer group (`cg-notification-worker`) listening to the same terminal topics (`ticketing.v1.issued`, `payment.v1.refunded`, `payment.v1.declined`) to communicate the outcome to the customer via Email and Push notifications. See `proposal.md` and `specs/customer-notification-worker/spec.md`.

## Goals / Non-Goals

**Goals:**
- Subscribe to `ticketing.v1.issued`, `payment.v1.refunded`, and `payment.v1.declined` under consumer group `cg-notification-worker` (`EnableAutoCommit = false`, `EnableAutoOffsetStore = false`).
- Enforce distributed idempotency using Redis (`notif:inbox:{consumerGroup}:{messageId}`) so that consumer rebalances or at-least-once Kafka redeliveries never spam customers with duplicate emails or push alerts.
- Model `CustomerNotification` as a rich domain entity validating recipient, reservation context, channel (`Email`, `Push`), and delivery state transitions (`Pending` → `Sent` / `Failed`).
- Execute external provider deliveries through a Polly resilience pipeline (Exponential Backoff Retry + Circuit Breaker).
- Route malformed/poison pill messages to `<topic>.DLQ` with diagnostic headers and W3C `traceparent` propagation.
- Maintain strict Clean Architecture (.NET 10) across 5 projects with native CQRS Lite (zero MediatR / zero AutoMapper).

**Non-Goals:**
- Creating a fifth PostgreSQL logical database (`infra-spec.md` explicitly fixes PostgreSQL to 4 logical databases: `catalog_events_db`, `reservation_db`, `payment_db`, and `ticketing_db`).
- Building a user-facing notification preferences REST API (out of scope for the background worker).

## Decisions

### Decision 1: Redis-Backed Distributed Inbox Store (`RedisNotificationInboxStore`)
- **Choice**: Store processed message markers in Redis under key `notif:inbox:{consumerGroup}:{messageId}` using `StringSetAsync(..., When.NotExists)` with a configurable retention TTL (default 7 days / `604800` seconds).
- **Rationale**: `infra-spec.md` Section 3.1 defines exactly 4 logical PostgreSQL databases (`catalog_events_db`, `reservation_db`, `payment_db`, `ticketing_db`) and places `TR.HW.Notification` as a stateless worker connected to `ticketreserver-internal-net`. Redis is already available on `ticketreserver-internal-net` with `maxmemory-policy noeviction` and AOF persistence, making it ideal for fast, atomic, distributed inbox deduplication across worker instances.
- **Alternatives Considered**:
  - *In-memory HashSet*: Rejected because it loses state on container restart and fails across horizontally scaled worker replicas.
  - *Adding a 5th PostgreSQL database*: Rejected to maintain strict compliance with `infra-spec.md` Section 3.1.

### Decision 2: Polly Retry + Circuit Breaker on External Notification Gateway
- **Choice**: Wrap `INotificationProviderGateway` calls inside `ResilientNotificationDispatcher` using Polly (`AsyncRetryPolicy` with 3 retries and exponential backoff + jitter, composed with `AsyncCircuitBreakerPolicy` breaking after 5 consecutive failures for 30 seconds).
- **Rationale**: External SMTP/Email (SendGrid/SES) and Push (FCM/APNs) gateways frequently exhibit transient network latency or rate-limiting spikes. Retrying transient failures prevents unnecessary crashes while the circuit breaker protects the worker thread pool during prolonged provider outages.

### Decision 3: Multi-Channel Dispatch per Outcome (`Email` + `Push`)
- **Choice**: For each terminal event (`TicketIssuedConfirmation`, `PaymentRefundedNotice`, `PaymentDeclinedNotice`), the command handler builds and dispatches rich `CustomerNotification` domain entities for both `NotificationChannel.Email` and `NotificationChannel.Push`, recording the inbox marker in Redis only after delivery succeeds.
- **Rationale**: Directly fulfills `PROJECT.md` Section 3.1.6 and `arch-decisions.md` (`Worker: Notificação E-mail/Push`). If one channel fails transiently, the retry policy handles it before the message is marked processed in the inbox store.

## Risks / Trade-offs

- **[Risk: Duplicate notification if worker crashes after provider dispatch but before Redis inbox marker write]** → Mitigation: Write the Redis inbox marker immediately upon successful dispatch before committing the Kafka offset, and pass a deterministic idempotency key (`{messageId}:{channel}`) to the notification provider gateway.
- **[Risk: Poison pill message blocking partition consumption]** → Mitigation: Catch `JsonException` and validation failures in `NotificationWorker`, forward the raw payload to `<topic>.DLQ` with diagnostic headers (`x-original-topic`, `x-exception-message`, `x-failed-at`, `traceparent`), and commit the offset.
