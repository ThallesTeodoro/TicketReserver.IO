# Especificação de Infraestrutura e Observabilidade

Este documento define a especificação técnica completa para a infraestrutura conteinerizada (Docker Compose / Kubernetes), topologia de rede, particionamento de bancos de dados, streaming de CDC com Debezium, mensageria Apache Kafka (modo KRaft) e o pipeline completo de observabilidade distribuída (**OpenTelemetry**, **Grafana**, **Loki**, **Tempo** e **Prometheus**) para o ecossistema **TicketReserver.IO**.

---

## 1. Visão Geral da Topologia de Infraestrutura

A infraestrutura é projetada para alta disponibilidade local e paridade estrita com o ambiente de produção em nuvem (K8s).

```mermaid
flowchart TB
    %% ================= INGRESS & REDES =================
    Client(["Clientes / Mobile App / Web"])
    
    subgraph DMZ ["Rede Pública / Ingress (ticketreserver-public-net)"]
        Ingress["API Gateway / Ingress \n(Traefik / Nginx :80/:443)"]
    end
    Client --> Ingress

    %% ================= COMPONENTES DA APLICAÇÃO =================
    subgraph APP_NET ["Rede Interna de Aplicação (ticketreserver-internal-net)"]
        %% APIs Públicas
        subgraph APIS ["Camada de APIs (Acessíveis pelo Gateway)"]
            APIEv["TR.API.Events \n(:5001)"]
            APISr["TR.API.Search \n(:5002)"]
            APIRes["TR.API.Reservation \n(:5003)"]
        end

        %% Workers Privados
        subgraph WORKERS ["Camada de Workers (Background Services sem portas externas)"]
            HWRepl["TR.HW.Repl (Replicação CDC)"]
            HWOutbox["TR.HW.OutboxRelay (Outbox Dispatcher)"]
            HWTimeout["TR.HW.ResTimeout (Reconciliação 10 min)"]
            HWPay["TR.HW.Payment (Processador de Pagamento)"]
            HWRefund["TR.HW.Refund (Estorno Automático)"]
            HWTick["TR.HW.Ticketing (Emissor de Ingressos)"]
            HWNotif["TR.HW.Notification (Notificações E-mail/Push)"]
        end

        %% Dados & Cache
        subgraph DATA_STORES ["Armazenamento e Cache"]
            PG[("PostgreSQL 16 \n(wal_level=logical) \n:5432")]
            REDIS[("Redis Stack \n(Cache & Distributed Hold) \n:6379")]
            ELASTIC[("ElasticSearch 8.x \n(Search Index) \n:9200")]
        end

        %% Mensageria & CDC
        subgraph STREAMING ["Mensageria Central & CDC"]
            KAFKA[["Apache Kafka (KRaft) \n:9092"]]
            DEBEZIUM["Debezium Kafka Connect \n:8083"]
            AKHQ["AKHQ (Web Console) \n:8085"]
        end
    end

    %% ================= OBSERVABILIDADE =================
    subgraph TELEMETRY_NET ["Rede de Telemetria (ticketreserver-telemetry-net)"]
        OTEL["OpenTelemetry Collector \n(:4317 gRPC / :4318 HTTP)"]
        PROMETHEUS[("Prometheus \n(:9090)")]
        TEMPO[("Grafana Tempo \n(:3200)")]
        LOKI[("Grafana Loki \n(:3100)")]
        GRAFANA["Grafana Dashboard \n(:3000)"]
    end

    %% Roteamento Ingress
    Ingress -- "/eventos" --> APIEv
    Ingress -- "/search" --> APISr
    Ingress -- "/reserva" --> APIRes

    %% Conexões de Dados
    APIEv <--> PG
    APIEv <--> REDIS
    APISr <--> ELASTIC
    APIRes <--> PG
    APIRes <--> REDIS
    HWRepl --> ELASTIC
    HWRepl <-- KAFKA
    HWOutbox <--> PG
    HWOutbox --> KAFKA
    HWTimeout <-- KAFKA
    HWTimeout <--> PG
    HWTimeout <--> REDIS
    HWPay <-- KAFKA
    HWPay <--> PG
    HWRefund <-- KAFKA
    HWRefund <--> PG
    HWTick <-- KAFKA
    HWTick <--> PG
    HWNotif <-- KAFKA

    PG -. "WAL Streaming" .-> DEBEZIUM
    DEBEZIUM -- "Tópicos CDC" --> KAFKA
    AKHQ <--> KAFKA

    %% Telemetria
    APIS -. "OTLP Traces / Metrics / Logs" .-> OTEL
    WORKERS -. "OTLP Traces / Metrics / Logs" .-> OTEL
    OTEL --> PROMETHEUS
    OTEL --> TEMPO
    OTEL --> LOKI
    GRAFANA --> PROMETHEUS
    GRAFANA --> TEMPO
    GRAFANA --> LOKI
```

---

## 2. Topologia de Redes e Guardrails de Segurança

O ambiente local e conteinerizado impõe estrita segregação por redes Docker:

| Rede Docker | Escopo | Componentes Conectados | Exposição Host / Externa |
| :--- | :--- | :--- | :--- |
| `ticketreserver-public-net` | DMZ / Gateway | Ingress (Traefik/Nginx), Clientes | `80:80`, `443:443` |
| `ticketreserver-internal-net` | Core Business | Todas as APIs, Workers, PostgreSQL, Redis, Kafka, Debezium, ElasticSearch | Sem exposição pública direta (apenas via Ingress ou ferramentas de dev locais) |
| `ticketreserver-telemetry-net` | Observabilidade | OTel Collector, Prometheus, Tempo, Loki, Grafana, todas as aplicações .NET | Grafana (`3000:3000`), AKHQ (`8085:8085`) |

### Guardrails de Segurança
1. **Nenhum Worker expõe portas TCP/HTTP externas:** Os workers (`TR.HW.*`) são exclusivamente *Background Services* que operam como consumidores Kafka e clientes de banco. Nenhuma porta de worker pode ser mapeada para o host.
2. **PostgreSQL com Autenticação Forte e Segregação Lógica:** Apenas portas necessárias para depuração em ambiente `dev` podem ser abertas localmente.
3. **Propagação Segura de Headers:** O Ingress sanitiza cabeçalhos HTTP de entrada e injeta/propaga o padrão **W3C Trace Context (`traceparent`, `tracestate`)**.

---

## 3. Armazenamento e Bancos de Dados

### 3.1. PostgreSQL 16 (Database-per-Service Lógico)
Um único cluster PostgreSQL é provisionado no Docker Compose para economia de recursos em desenvolvimento, porém isolando estritamente **4 bancos de dados lógicos** correspondentes a cada microsserviço:

* **Bancos Lógicos Criados:**
  1. `catalog_events_db`: Domínio de Catálogo de Eventos (`SVC_EVENTOS`).
  2. `reservation_db`: Domínio de Reserva (`SVC_RESERVA`).
  3. `payment_db`: Domínio de Pagamento (`SVC_PAGAMENTO`).
  4. `ticketing_db`: Domínio de Emissão e Inventário de Ingressos (`SVC_TICKETING`).
* **Configuração Mandatória para CDC (`postgresql.conf`):**
  * `wal_level = logical`: Exigido pelo Debezium para captura de eventos do WAL.
  * `max_wal_senders = 10`
  * `max_replication_slots = 10`

#### Script de Inicialização Automatizada (`init-databases.sql`):
```sql
-- Criação de Usuários e Bancos
CREATE DATABASE catalog_events_db;
CREATE DATABASE reservation_db;
CREATE DATABASE payment_db;
CREATE DATABASE ticketing_db;

-- Habilitação de extensões em cada banco
\c catalog_events_db;
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

\c reservation_db;
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

\c payment_db;
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

\c ticketing_db;
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
```

### 3.2. Redis Stack (Porta 6379 / Web UI 8001)
* **Objetivo:** 
  * Cache L1 do catálogo de eventos.
  * Mecanismo de **Distributed Lock (Seat Hold)** com comando atômico `SET NX EX 600` e scripts Lua de liberação segura.
* **Diretiva de Memória Crítica:**
  * `maxmemory 512mb`
  * `maxmemory-policy noeviction`: **Regra de Ouro:** Sob nenhuma hipótese o Redis pode descartar chaves com TTL ativas por LRU/LFU, pois isso destruiria as garantias atômicas de assento reservado durante picos de concorrência.
  * Persistência híbrida ativada: `appendonly yes` (AOF) com fsync `everysec` para tolerância a quedas abruptas.

### 3.3. ElasticSearch 8.x + Kibana (Portas 9200 / 5601)
* **Configuração de Ambiente Dev:**
  * `discovery.type=single-node`
  * `xpack.security.enabled=false` (simplificado para ambiente de desenvolvimento local)
  * `ES_JAVA_OPTS=-Xms512m -Xmx512m`
* **Índice e Alias Inicial:**
  * Criação automática do índice `events_search_v1` com os analisadores em português (`pt_text_analyzer`) e `autocomplete_analyzer` (Edge N-Gram) e o alias `events_search`.

---

## 4. Mensageria e Streaming (Apache Kafka KRaft & Debezium CDC)

### 4.1. Apache Kafka em Modo KRaft (Sem ZooKeeper)
* **Imagem:** `confluentinc/cp-kafka:7.6.0` ou imagem Bitnami compatível.
* **Portas:**
  * `9092`: Comunicação interna na rede `ticketreserver-internal-net`.
  * `29092`: Listener interno do broker.
  * `9094`: Listener mapeado para o host local (ferramentas de debug).
* **Parâmetros de Resiliência do Cluster:**
  * `KAFKA_AUTO_CREATE_TOPICS_ENABLE: "false"` (tópicos devem ser criados explicitamente para garantir particionamento correto).
  * `KAFKA_DEFAULT_REPLICATION_FACTOR: 1` (para ambiente local de nó único).

### 4.2. Topologia Formal de Tópicos e Partições
Script determinístico de provisionamento de tópicos (`init-kafka-topics.sh`):

```bash
# Tópicos de Negócio e Sagas
kafka-topics --create --if-not-exists --bootstrap-server kafka:9092 --partitions 3 --replication-factor 1 --topic reservation.v1.checkout_req
kafka-topics --create --if-not-exists --bootstrap-server kafka:9092 --partitions 3 --replication-factor 1 --topic payment.v1.approved
kafka-topics --create --if-not-exists --bootstrap-server kafka:9092 --partitions 3 --replication-factor 1 --topic payment.v1.refunded
kafka-topics --create --if-not-exists --bootstrap-server kafka:9092 --partitions 3 --replication-factor 1 --topic ticketing.v1.issued
kafka-topics --create --if-not-exists --bootstrap-server kafka:9092 --partitions 3 --replication-factor 1 --topic ticketing.v1.failed

# Tópico Especial com Delay / Reconciliação (10 minutos TTL)
kafka-topics --create --if-not-exists --bootstrap-server kafka:9092 --partitions 3 --replication-factor 1 --topic reservation.v1.hold_created.delayed

# Dead Letter Queues (DLQs) Obrigatórias (kafka-resilience-audit)
kafka-topics --create --if-not-exists --bootstrap-server kafka:9092 --partitions 1 --replication-factor 1 --topic reservation.v1.checkout_req.DLQ
kafka-topics --create --if-not-exists --bootstrap-server kafka:9092 --partitions 1 --replication-factor 1 --topic payment.v1.approved.DLQ
kafka-topics --create --if-not-exists --bootstrap-server kafka:9092 --partitions 1 --replication-factor 1 --topic payment.v1.refunded.DLQ
kafka-topics --create --if-not-exists --bootstrap-server kafka:9092 --partitions 1 --replication-factor 1 --topic ticketing.v1.issued.DLQ
kafka-topics --create --if-not-exists --bootstrap-server kafka:9092 --partitions 1 --replication-factor 1 --topic ticketing.v1.failed.DLQ
```

### 4.3. Debezium Kafka Connect (Porta 8083)
Monitora as tabelas do `catalog_events_db` e envia mutações via CDC para o Kafka.

#### Payload de Registro do Conector (`debezium-postgres-catalog.json`):
```json
{
  "name": "catalog-postgres-connector",
  "config": {
    "connector.class": "io.debezium.connector.postgresql.PostgresConnector",
    "tasks.max": "1",
    "plugin.name": "pgoutput",
    "database.hostname": "postgres",
    "database.port": "5432",
    "database.user": "postgres",
    "database.password": "postgres",
    "database.dbname": "catalog_events_db",
    "database.server.name": "catalog",
    "table.include.list": "public.venues,public.events,public.seat_sections,public.ticket_tiers,public.seats",
    "topic.prefix": "catalog.events",
    "tombstones.on.delete": "true"
  }
}
```

---

## 5. Pipeline de Observabilidade Distribuída (OpenTelemetry Full-Stack)

A observabilidade adota a tríade **Logs, Métricas e Traces** unificada pelo **OpenTelemetry Collector**.

```text
[Microsserviços .NET 8] 
         │ (OTLP gRPC / Porta 4317)
         ▼
 ┌───────────────────────────────────────────────┐
 │            OpenTelemetry Collector            │
 │  (Receivers: OTLP / Processors: Batch, Limit) │
 └───────┬──────────────┬──────────────┬─────────┘
         │              │              │
         ▼ (Traces)     ▼ (Metrics)    ▼ (Logs)
  ┌─────────────┐┌─────────────┐┌─────────────┐
  │Grafana Tempo││ Prometheus  ││Grafana Loki │
  │ (:3200)     ││ (:9090)     ││ (:3100)     │
  └──────┬──────┘└──────┬──────┘└──────┬──────┘
         │              │              │
         └──────────────┼──────────────┘
                        ▼
             ┌─────────────────────┐
             │       Grafana       │
             │ (:3000 Dashboard)   │
             └─────────────────────┘
```

### 5.1. Configuração do OTel Collector (`otel-collector-config.yaml`)

```yaml
receivers:
  otlp:
    protocols:
      grpc:
        endpoint: 0.0.0.0:4317
      http:
        endpoint: 0.0.0.0:4318

processors:
  batch:
    timeout: 1s
    send_batch_size: 256
  memory_limiter:
    check_interval: 1s
    limit_percentage: 75
    spike_limit_percentage: 20
  resource:
    attributes:
      - key: deployment.environment
        value: "development"
        action: upsert

exporters:
  # Exportador de Traces para o Grafana Tempo
  otlp/tempo:
    endpoint: "tempo:4317"
    tls:
      insecure: true

  # Exportador de Métricas para Scraping do Prometheus
  prometheus:
    endpoint: "0.0.0.0:8889"
    namespace: "ticketreserver"

  # Exportador de Logs para o Grafana Loki
  loki:
    endpoint: "http://loki:3100/loki/api/v1/push"
    default_labels_enabled:
      exporter: false
      job: true

service:
  pipelines:
    traces:
      receivers: [otlp]
      processors: [memory_limiter, batch, resource]
      exporters: [otlp/tempo]
    metrics:
      receivers: [otlp]
      processors: [memory_limiter, batch, resource]
      exporters: [prometheus]
    logs:
      receivers: [otlp]
      processors: [memory_limiter, batch, resource]
      exporters: [loki]
```

### 5.2. Configuração do Prometheus (`prometheus.yml`)
Configurado para raspar as métricas consolidadas pelo OTel Collector:

```yaml
global:
  scrape_interval: 5s
  evaluation_interval: 5s

scrape_configs:
  - job_name: "otel-collector"
    static_configs:
      - targets: ["otel-collector:8889"]
```

### 5.3. Configuração do Grafana Provisioning
No diretório `grafana/provisioning/datasources/datasources.yaml`, os data sources são registrados com correlação nativa:

```yaml
apiVersion: 1

datasources:
  - name: Prometheus
    type: prometheus
    access: proxy
    url: http://prometheus:9090
    isDefault: true

  - name: Tempo
    type: tempo
    access: proxy
    url: http://tempo:3200
    jsonData:
      httpMethod: GET
      tracesToLogsV2:
        datasourceUid: "loki"
        spanStartTimeShift: "1h"
        spanEndTimeShift: "-1h"
        filterByTraceId: true
        filterBySpanId: false
        customQuery: true
        query: '{service_name="$${__service.name}"} |= "$${__trace.id}"'

  - name: Loki
    type: loki
    access: proxy
    url: http://loki:3100
    jsonData:
      derivedFields:
        - datasourceUid: "tempo"
          matcherRegex: "traceparent=00-([0-9a-fA-F]{32})-"
          name: TraceID
          url: "$${__value.raw}"
```

### 5.4. Instrumentação de Aplicação (.NET 10 OpenTelemetry & W3C TraceContext)

Todos os serviços (.NET 10) utilizam a biblioteca compartilhada `shared/TR.Common.Diagnostics` para telemetria de aplicação:

1. **Rastreamento Distribuído via Kafka (W3C TraceContext):**
   - Injeção e extração de headers W3C standard (`traceparent` e `tracestate`) nos registros de mensagem do Apache Kafka via `KafkaTraceContextPropagator`.
   - `ActivitySource` registrados:
     - `TicketReserver.Kafka`: Spans para publicação e consumo de mensagens com `ActivityKind.Producer` e `ActivityKind.Consumer`.
     - `TicketReserver.Http`: Instrumentação de requisições de entrada/saída HTTP.
   - Exportação direta para o OTel Collector via OTLP gRPC (`http://otel-collector:4317`).

2. **Métricas de Domínio e Resiliência (System.Diagnostics.Metrics):**
   - `ticketreserver_redis_lock_acquire_duration_seconds` (Histogram): Latência na aquisição de locks distribuídos no Redis.
   - `ticketreserver_reservations_active_total` (UpDownCounter): Volume de reservas ativas em retenção temporária.
   - `ticketreserver_saga_events_total` (Counter): Contagem de eventos de saga particionados por `event_type` (`payment.v1.approved`, `payment.v1.refunded`, `ticketing.v1.issued`, `ticketing.v1.failed`, `reservation.compensated`) e `status`.
   - `ticketreserver_seat_collision_refunds_total` (Counter): Taxa de colisões de assento disparando compensação (auto-refund).
   - `ticketreserver_outbox_pending_messages` (ObservableGauge): Backlog corrente de mensagens pendentes no outbox transacional.

3. **Logs Estruturados Correlacionados:**
   - Logs Serilog enriquecidos com `TraceId`, `SpanId`, `service.name` e `service.version`.

---

## 6. Dashboards e Métricas Críticas de Negócio

O Grafana é pré-configurado com dashboards cobrindo indicadores técnicos e métricas de domínio:

1. **APM & Concorrência de Reservas:**
   * Taxa de requisições por segundo em `/reserva/hold` e `/reserva/checkout`.
   * Tempo de resposta p95 e p99 da aquisição de Lock no Redis (`SET NX EX`).
   * Quantidade de reservas ativas em estado `AWAITING_PAYMENT`.
2. **Saúde da Saga e Concorrência de Ticketing (Auto-Refund):**
   * Gráfico comparativo: `payment.v1.approved` vs `ticketing.v1.issued` vs `ticketing.v1.failed`.
   * Taxa de colisões de assento detectadas (disparos de estorno automático no `TR.HW.Refund`).
3. **Resiliência do Kafka e Outbox:**
   * Backlog de mensagens pendentes na tabela `outbox_messages` do PostgreSQL.
   * Mensagens roteadas para tópicos `.DLQ` (Poison Pills).
   * Consumer Lag por grupo de consumidores nos tópicos críticos.

---

## 7. Relação de Portas e Serviços para Desenvolvimento Local

| Serviço | Container | Portas Internas | Porta Host (Dev) | Função Principal |
| :--- | :--- | :--- | :--- | :--- |
| **Ingress** | `ticketreserver-ingress` | `80`, `443` | `80:80`, `443:443` | Roteador e API Gateway local |
| **PostgreSQL** | `ticketreserver-postgres` | `5432` | `5432:5432` | 4 bases lógicas (`events`, `res`, `pay`, `tick`) |
| **Redis Stack** | `ticketreserver-redis` | `6379`, `8001` | `6379:6379`, `8001:8001` | Lock de 10 min e Web UI |
| **Kafka (KRaft)** | `ticketreserver-kafka` | `9092`, `29092` | `9094:9094` | Barramento principal de eventos e delayed |
| **Debezium** | `ticketreserver-debezium` | `8083` | `8083:8083` | CDC PostgreSQL -> Kafka |
| **AKHQ** | `ticketreserver-akhq` | `8080` | `8085:8080` | Interface visual para tópicos e DLQs |
| **ElasticSearch** | `ticketreserver-elastic` | `9200` | `9200:9200` | Motor de busca facetada |
| **Kibana** | `ticketreserver-kibana` | `5601` | `5601:5601` | Exploração de dados do ElasticSearch |
| **OTel Collector** | `ticketreserver-otel` | `4317`, `4318`, `8889` | `4317`, `4318` | Ingestão e roteamento de telemetria |
| **Prometheus** | `ticketreserver-prometheus` | `9090` | `9090:9090` | Time-series metrics engine |
| **Grafana Tempo** | `ticketreserver-tempo` | `3200`, `4317` | `3200:3200` | Distributed Tracing backend |
| **Grafana Loki** | `ticketreserver-loki` | `3100` | `3100:3100` | Agregação centralizada de logs |
| **Grafana** | `ticketreserver-grafana` | `3000` | `3000:3000` | Dashboards APM e negócio |

---

## 8. Ingress e API Gateway (NGINX Alpine)

O ecossistema implementa um ponto único de entrada baseado em **NGINX 1.27 Alpine** (`ticketreserver-ingress`), posicionado na DMZ com conexões simultâneas a `ticketreserver-public-net` (exposição das portas 80 e 443 no host) e `ticketreserver-internal-net` (resolução e encaminhamento para os microsserviços internos).

### 8.1. Tabela de Roteamento Unificado

| Rota Externa (Porta 80) | Upstream Interno | Método / Rewrite | Função Principal |
| :--- | :--- | :--- | :--- |
| `GET /healthz` | Local | N/A | Healthcheck do Ingress (`{"status":"UP"}`) |
| `GET /events`, `POST /events` | `http://events-api:8080/events` | Pass-through | Catálogo de eventos |
| `GET /venues`, `GET /venues/{id}` | `http://events-api:8080/venues` | Pass-through | Locais e setores físicos |
| `POST /reserva/hold` | `http://reservation-api:8080/reserva/hold` | Pass-through | Reserva temporária e lock no Redis |
| `POST /reserva/checkout` | `http://reservation-api:8080/reserva/checkout` | Pass-through | Checkout e envio para saga Kafka |
| `GET /reserva/{id}` | `http://reservation-api:8080/reserva/{id}` | Pass-through | Consulta de status e expiração |
| `GET /search?q=...` | `http://search-api:8080/api/v1/search/events?q=...` | Rewrite (`^/search` -> `/api/v1/search/events`) | Busca facetada no ElasticSearch |
| `GET /search/autocomplete` | `http://search-api:8080/api/v1/search/events/autocomplete` | Rewrite | Autocomplete rápido por prefixo |
| `GET /api/v1/search/*` | `http://search-api:8080/api/v1/search/*` | Pass-through | Rotas nativas da API de busca |

### 8.2. Observabilidade e Propagação de Trace W3C

O Ingress injeta e preserva cabeçalhos essenciais para rastreabilidade de ponta a ponta:
- `traceparent` e `tracestate`: Propagados transparentemente para Tempo e Loki.
- `X-Request-ID`: Injetado automaticamente via `$request_id` para correlação de logs caso não venha do cliente.
- `Host`, `X-Real-IP`, `X-Forwarded-For`, `X-Forwarded-Proto`: Cabeçalhos padrão de proxy reverso.

### 8.3. Script de Validação e Smoke Test

O script [docker/ingress/verify-ingress.sh](file:///home/thallesteodoro/development/ticket-reservation-pdi/docker/ingress/verify-ingress.sh) permite validar sintaxe e conectividade:
```bash
./docker/ingress/verify-ingress.sh --dry-run
./docker/ingress/verify-ingress.sh
```
