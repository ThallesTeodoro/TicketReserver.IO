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

## 🚪 API Gateway & Ponto Único de Entrada (NGINX Alpine)

A plataforma disponibiliza um **API Gateway / Ingress** centralizado em `http://localhost` (Porta `80` HTTP / `443` HTTPS), isolando a rede interna (`ticketreserver-internal-net`) e unificando o acesso a todas as APIs:

| Rota Externa (Porta 80) | Serviço Upstream | Função Principal |
| :--- | :--- | :--- |
| `GET /healthz` | `ticketreserver-ingress` | Healthcheck do Gateway (`{"status":"UP"}`) |
| `GET /events`, `POST /events` | `events-api:8080` | Catálogo e cadastro de eventos |
| `GET /venues`, `GET /venues/{id}` | `events-api:8080` | Locais, estádios e arenas |
| `POST /reserva/hold` | `reservation-api:8080` | Bloqueio temporário de assento (`SET NX EX 600`) |
| `POST /reserva/checkout` | `reservation-api:8080` | Envio de reserva para a saga de pagamento |
| `GET /reserva/{id}` | `reservation-api:8080` | Consulta de status e tempo restante do hold |
| `GET /search?q=rock` | `search-api:8080` | Busca full-text facetada no ElasticSearch (rewrite para `/api/v1/search/events`) |
| `GET /search/autocomplete?q=roc` | `search-api:8080` | Autocomplete com Edge N-Grams |
| `GET /api/v1/search/*` | `search-api:8080` | Rotas versionadas diretas da API de busca |

- **W3C Distributed Tracing**: O Ingress preserva e propaga transparentemente os headers `traceparent` e `tracestate` para os serviços internos, garantindo a visualização de traces ponta a ponta no Grafana Tempo.
- **CORS Centralizado**: Tratamento unificado de preflight `OPTIONS` com headers `Access-Control-Allow-*`.

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

### 🔍 Instrumentação de Aplicação (.NET 10 OpenTelemetry)

Todos os microsserviços Web API e background workers utilizam a biblioteca compartilhada `TR.Common.Diagnostics` para telemetria nativa unificada:

1. **Rastreamento Distribuído (W3C TraceContext over Kafka):**
   - Propagação transparente dos headers W3C (`traceparent` e `tracestate`) nos registros de mensagem do Apache Kafka via `KafkaTraceContextPropagator`.
   - `ActivitySource` dedicados: `TicketReserver.Kafka` e `TicketReserver.Http`.
   - Spans de consumo de mensagens (`Kafka Consumer Process`) são linkados diretamente ao span de publicação da API ou outbox relay originário, permitindo visualização de traces ponta a ponta no Grafana Tempo através de toda a saga coreografada.

2. **Métricas de Domínio e Resiliência (System.Diagnostics.Metrics):**
   - `ticketreserver_redis_lock_acquire_duration_seconds` (Histogram): Latência de aquisição de lock distribuído no Redis (`SET NX EX`).
   - `ticketreserver_reservations_active_total` (UpDownCounter): Contagem de reservas ativas em retenção temporária.
   - `ticketreserver_saga_events_total` (Counter): Volume de eventos de saga particionado por `event_type` (`payment.v1.approved`, `payment.v1.refunded`, `ticketing.v1.issued`, `ticketing.v1.failed`, `reservation.compensated`) e `status`.
   - `ticketreserver_seat_collision_refunds_total` (Counter): Taxa de colisões de assento disparando compensação (auto-refund).
   - `ticketreserver_outbox_pending_messages` (ObservableGauge): Monitoramento em tempo real do backlog de mensagens outbox pendentes no PostgreSQL.

3. **Logs Estruturados Correlacionados:**
   - Logs Serilog enriquecidos automaticamente com `TraceId`, `SpanId` e atributos de serviço (`service.name`, `service.version`), exportados via OTLP gRPC para o OTel Collector e agregados no Grafana Loki.

---

## 🚀 Como Executar

### 1. Iniciar toda a infraestrutura e microsserviços via Docker Compose
```bash
docker compose up -d
```

### 2. Validar o Gateway e a Observabilidade
```bash
# Validação da configuração do Ingress / API Gateway
./docker/ingress/verify-ingress.sh --dry-run

# Validação da configuração da Observabilidade
./docker/observability/verify-observability.sh --dry-run
```

Ou com os containers rodando:
```bash
./docker/ingress/verify-ingress.sh
./docker/observability/verify-observability.sh
```

---

## 📚 Documentação Técnica

- [Decisões Arquiteturais e Sagas](file:///home/thallesteodoro/development/ticket-reservation-pdi/.agents/specification/architecture/arch-decisions.md)
- [Especificação de Infraestrutura e Redes](file:///home/thallesteodoro/development/ticket-reservation-pdi/.agents/specification/infra/infra-spec.md)
- [Definição de Pronto e Projeto](file:///home/thallesteodoro/development/ticket-reservation-pdi/PROJECT.md)
