# TicketReserver.IO 🎫

O **TicketReserver.IO** é um sistema distribuído de alta concorrência para reserva e venda de ingressos, desenvolvido como parte de um Plano de Desenvolvimento Individual (PDI) focado na stack .NET e arquiteturas modernas de microsserviços.

## 🚀 Visão Geral
O objetivo principal é garantir a reserva de assentos por tempo limitado, processar pagamentos de forma resiliente e garantir a consistência eventual através de uma SAGA coreografada.

## 🛠️ Stack Tecnológica
- **Linguagem:** C# .NET 9
- **Bancos de Dados:** PostgreSQL (Persistência) e Redis (Idempotência)
- **Mensageria:** Apache Kafka (Confluent Platform)
- **Observabilidade:** OpenTelemetry, Grafana Loki, Grafana Tempo e Prometheus
- **Infraestrutura:** Docker & Kubernetes

## 🏗️ Arquitetura e Padrões
- **Arquitetura Hexagonal (Ports & Adapters)**
- **CQRS Lite**
- **SAGA Coreografada** (Coordenação de transações distribuídas)
- **Outbox Pattern** (Garantia de entrega de mensagens)
- **Idempotência de Borda e Transacional**

## 🔧 Como Executar (Local)

### Pré-requisitos
- Docker & Docker Compose

### Subindo a Infraestrutura
```bash
docker compose up -d
```

### Endpoints Locais
- **Grafana (Dashboards & Traces):** `http://localhost:3000`
- **AKHQ (Gestão Kafka):** `http://localhost:8085`
- **Redis Insight:** `http://localhost:8001`
- **Postgres:** `localhost:5432`

## 📂 Estrutura do Projeto
- `.gemini/`: Regras, guias de estilo e especificações técnicas.
- `infrastructure/`: Configurações de Docker, OTel, Grafana, etc.
- `planning/`: Backlog e logs das sprints.
- `source/`: Código-fonte da solução (em desenvolvimento).

---
*Este projeto é estritamente para fins de estudo e desenvolvimento de hard skills nível sênior.*
