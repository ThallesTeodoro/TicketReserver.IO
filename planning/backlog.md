# Backlog Geral Detalhado - TicketReserver.IO (backlog.md)

## Epic 01: Fundação de Infraestrutura & Observabilidade (Sprint 01)
- [ ] **Task 01.1:** Setup do `docker-compose` com Postgres (databases: `reservation`, `payment`, `ticketing`).
- [ ] **Task 01.2:** Configuração do Apache Kafka (KRaft mode) e AKHQ para gestão de tópicos.
- [ ] **Task 01.3:** Setup do Pipeline de Telemetria (OTel Collector -> Loki, Tempo, Prometheus).
- [ ] **Task 01.4:** Provisionamento automático de Dashboards do Grafana (Estado da Infra e Logs).

## Epic 02: Core de Reservas & Idempotência de Borda (Sprint 02)
- [ ] **Task 02.1:** Criação da Solução .NET e Projeto `TR.API.Reservation` (Minimal API).
- [ ] **Task 02.2:** Implementação do Middleware de Idempotência com Redis (Padrão: `idempotency:reservation:{key}`).
- [ ] **Task 02.3:** [Fluxo Hexagonal] Domínio: Entidade `Reservation` rica e Interface `IReservationRepository`.
- [ ] **Task 02.4:** [Fluxo Hexagonal] Infra: Repositório com Dapper e Transação Atômica (Reservation + OutboxTable).
- [ ] **Task 02.5:** [Fluxo Hexagonal] Delivery: Use Case de Criação de Reserva e Endpoint.
- [ ] **Task 02.6:** Testes Unitários: Regras de negócio, duplicidade e falhas de persistência.

## Epic 03: Resiliência Assíncrona & Outbox Pattern (Sprint 03)
- [ ] **Task 03.1:** Implementação do Worker `TR.HW.OutboxRelay`.
- [ ] **Task 03.2:** Lógica de Polling/Publishing com propagação de `TraceId` nos Headers do Kafka.
- [ ] **Task 03.3:** Implementação do Worker `TR.HW.Payment` (Consumer).
- [ ] **Task 03.4:** Gestão Manual de Offsets e Idempotência Transacional no Banco de Pagamentos.
- [ ] **Task 03.5:** Configuração de Dead Letter Queues (DLQ) e Estratégia de Retry (Polly).

## Epic 04: SAGA Coreografada & Emissão (Sprint 04)
- [ ] **Task 04.1:** Implementação do evento `payment.approved` e consumo pelo `TR.HW.Ticketing`.
- [ ] **Task 04.2:** Geração de Ingressos (PDF/QR Code fake) e persistência.
- [ ] **Task 04.3:** Implementação do Fluxo de Compensação: `payment.refused` -> `TR.API.Reservation` (Liberação de Assento).
- [ ] **Task 04.4:** Teste de Integração de Ponta-a-Ponta com rastreio via Trace ID unificado.

## Epic 05: Cloud Native & Orquestração Kubernetes (Sprint 05)
- [ ] **Task 05.1:** Dockerização final de todos os componentes (Multi-stage build).
- [ ] **Task 05.2:** Criação dos manifestos K8s (Deployment, Service, ConfigMap, Secret).
- [ ] **Task 05.3:** Configuração de Liveness/Readiness Probes vinculadas ao Health Checks do .NET.
- [ ] **Task 05.4:** Configuração de HPA (Horizontal Pod Autoscaler) baseado em métricas de concorrência.
