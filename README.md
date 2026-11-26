# TicketReserver.IO

Plataforma de alta concorrência para venda e reserva de ingressos construída com Clean Architecture, Event-Driven Architecture (EDA) e Sagas Coreografadas sobre o ecossistema .NET 10, Apache Kafka, PostgreSQL, Redis e ElasticSearch.

---

## 🏛 Arquitetura e Visão Geral

- **Microsserviços de Negócio:**
  - `TR.API.Events`: Catálogo de eventos, venues e setores com cache Redis L1.
  - `TR.API.Reservation`: Orquestração de reservas temporárias com lock distribuído no Redis (`SET NX EX 600`).
  - `TR.API.Search`: Motor de busca full-text e autocomplete no ElasticSearch 8 com Edge N-Grams e boosts semânticos.
- **Workers e Sagas:**
  - `TR.HW.ReservationOutboxRelay`: Transational Outbox Relay para publicação garantida no Kafka.
  - `TR.HW.ResTimeout`: Reconciliação e liberação de locks de reservas expiradas.
  - `TR.HW.Payment`: Processamento assíncrono de pagamentos.
  - `TR.HW.Ticketing`: Emissão segura de ingressos com QR Code criptográfico (HMAC-SHA256).
  - `TR.HW.Refund`: Compensação automática (Auto-Refund) em cenários de conflito de assentos.
  - `TR.HW.ResCompensator`: Compensação e estorno de estado de reserva.
  - `TR.HW.Notification`: Notificações de confirmação de compra e falhas aos clientes.
  - `TR.HW.Repl`: Replicação de dados CDC (Debezium + PostgreSQL) para o ElasticSearch.

---

## 📊 Stack de Observabilidade Full-Stack

A plataforma inclui uma stack de telemetria completa e unificada:

| Componente | Porta Host | Endpoint / UI | Função Principal |
| :--- | :--- | :--- | :--- |
| **OpenTelemetry Collector** | `4317` (gRPC), `4318` (HTTP) | `http://localhost:4318` | Ingestão unificada e roteamento de traces, métricas e logs |
| **Prometheus** | `9090` | [http://localhost:9090](http://localhost:9090) | Coleta e armazenamento de métricas time-series a cada 5s |
| **Grafana Tempo** | `3200` | [http://localhost:3200](http://localhost:3200) | Armazenamento e consulta de traces distribuídos |
| **Grafana Loki** | `3100` | [http://localhost:3100](http://localhost:3100) | Agregação centralizada de logs estruturados via LogQL |
| **Grafana** | `3000` | [http://localhost:3000](http://localhost:3000) | Painéis de monitoramento, APM e correlação de telemetria |

### Acesso ao Grafana

- **URL:** [http://localhost:3000](http://localhost:3000)
- **Usuário padrão:** `admin`
- **Senha padrão:** `admin`
- **Datasources pré-configurados:**
  - `Prometheus` (default)
  - `Tempo` (com correlação nativa para logs no Loki via `tracesToLogsV2`)
  - `Loki` (com correlação reversa para traces no Tempo via `derivedFields` extraindo o `TraceID`)
- **Dashboards provisionados:**
  - `TicketReserver - Saga & Business Metrics` (Concorrência `/hold` e `/checkout`, latência p95/p99 de Lock no Redis, saúde da Saga e taxa de auto-refund, backlog de outbox e consumer lag no Kafka).

---

## 🚀 Como Executar

### 1. Iniciar toda a infraestrutura e microsserviços via Docker Compose
```bash
docker compose up -d
```

### 2. Validar a configuração e integridade da observabilidade
```bash
./docker/observability/verify-observability.sh --dry-run
```

Ou com os containers rodando:
```bash
./docker/observability/verify-observability.sh
```

---

## 📚 Documentação Técnica

- [Decisões Arquiteturais e Sagas](file:///.agents/specification/architecture/arch-decisions.md)
- [Especificação de Infraestrutura e Redes](file:///.agents/specification/infra/infra-spec.md)
- [Definição de Pronto e Projeto](file:///PROJECT.md)
