# Backlog Geral do PDI (backlog.md)

## Epic 01: Infraestrutura de Desenvolvimento & Observabilidade
- [ ] Configuração do `docker-compose` unificado (Kafka, Postgres, Redis, OTel Collector, Loki, Grafana, AKHQ).
- [ ] Configuração inicial do Dashboard do Grafana e conexões de Data Source.

## Epic 02: Core de Reservas & Outbox Pattern
- [ ] Implementação da Minimal API de Reservas com Middleware de Idempotência no Redis.
- [ ] Implementação da transação atômica EF Core (Reservation + Outbox Table).
- [ ] Desenvolvimento do componente isolado `Outbox-Relay` em .NET.

## Epic 03: Motor Financeiro & Resiliência Assíncrona
- [ ] Criação do Worker de Pagamentos com integração `Confluent.Kafka`.
- [ ] Implementação da Idempotência Transacional via Constraint no Postgres.
- [ ] Implementação da lógica de tratamento de erro com desvio para Dead Letter Queue (DLQ).

## Epic 04: Fechamento da SAGA e Emissão
- [ ] Criação do Worker de Ticketing.
- [ ] Implementação do fluxo completo de reversão/compensação da SAGA (Cancelamento automático de assento).

## Epic 05: Orquestração Enterprise (Kubernetes)
- [ ] Geração dos manifestos YAML de K8s para todos os microsserviços.
- [ ] Configuração do HPA (Autoscaling) baseado em estresse de concorrência.
- [ ] Integração das Health Probes do .NET com o ciclo de vida do K8s.