# Proposal

## Why

When a customer's checkout saga reaches a terminal state — whether tickets are issued (`ticketing.v1.issued`), a payment is automatically refunded due to a seat collision (`payment.v1.refunded`), or a payment is declined by the gateway (`payment.v1.declined`) — the customer must receive an immediate, idempotent notification via Email and Push (`SVC_NOTIFICACAO`). Without `TR.HW.Notification`, the event-driven choreography completes internal database state transitions silently without informing the end user of their issued tickets or refund confirmation.

## What Changes

- Scaffold `TR.HW.Notification` solution with clean separation across 5 projects (`Domain`, `Application`, `Infrastructure`, `Worker`, `UnitTests`) following .NET 10 and CQRS Lite guidelines (zero MediatR and zero AutoMapper).
- Implement `CustomerNotification` aggregate root, value objects/enums (`NotificationType`, `NotificationChannel`, `NotificationStatus`), and domain exceptions in `TR.HW.Notification.Domain`.
- Implement CQRS Lite `SendCustomerNotificationCommand`, `SendCustomerNotificationCommandHandler`, `SendCustomerNotificationCommandValidator`, and static mapping extensions in `TR.HW.Notification.Application`.
- Implement `RedisNotificationInboxStore` using `StackExchange.Redis` (`SET notif:inbox:{consumerGroup}:{messageId} NX EX`) to guarantee distributed inbox deduplication across worker replicas without modifying the 4-database PostgreSQL topology.
- Implement `ResilientNotificationDispatcher` in `TR.HW.Notification.Infrastructure` using Polly (Exponential Backoff Retry and Circuit Breaker) to dispatch transactional Email and Push notifications.
- Implement `NotificationWorker` (`BackgroundService`) subscribing to `ticketing.v1.issued`, `payment.v1.refunded`, and `payment.v1.declined` under consumer group `cg-notification-worker` with manual offset commits, W3C `traceparent` extraction, and DLQ routing for poison pill payloads.
- Configure multi-stage `Dockerfile` and `docker-compose.yml` for `TR.HW.Notification`, registering it in the root `docker-compose.yml`.

## Capabilities

### New Capabilities
- `customer-notification-worker`: Consumes terminal saga events (`ticketing.v1.issued`, `payment.v1.refunded`, `payment.v1.declined`), enforces distributed inbox deduplication in Redis, and dispatches resilient Email and Push notifications to customers.

### Modified Capabilities
<!-- None -->

## Impact

- **New Projects**: `TR.HW.Notification` (`Domain`, `Application`, `Infrastructure`, `Worker`, `UnitTests`).
- **Cache / Distributed Inbox**: Uses Redis (`notif:inbox:{consumerGroup}:{messageId}`) for idempotent message deduplication.
- **Kafka**: Multi-topic consumer for `ticketing.v1.issued`, `payment.v1.refunded`, and `payment.v1.declined` (`cg-notification-worker`) with DLQ fallback.
- **Root Docker Compose**: Registration of `ticketreserver-notification-worker` via `include`.
