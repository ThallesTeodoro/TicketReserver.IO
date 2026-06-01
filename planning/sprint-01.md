# Sprint 01 - Fundação de Infraestrutura e Observabilidade (sprint-01.md)

## Objetivo da Sprint
Prover todo o ecossistema de ferramentas necessárias para o desenvolvimento, garantindo que a comunicação entre bancos, mensageria e o pipeline de observabilidade esteja validado e monitorado antes de iniciarmos qualquer código de negócio.

## Tarefas Detalhadas

### 1. Provisionamento do Ecossistema Docker
*   **Descrição:** Criar o `docker-compose.yml` contendo toda a stack tecnológica.
*   **Componentes:**
    *   Postgres (Script de init para bases `reservation_db`, `payment_db`, `ticketing_db`).
    *   Redis Stack (Cache e interface Insight).
    *   Kafka (KRaft) + AKHQ (UI).
    *   OpenTelemetry Collector.
    *   Grafana Stack (Loki, Tempo, Prometheus).
*   **DoD:** Todos os contêineres saudáveis e interfaces (AKHQ, Grafana, Redis Insight) acessíveis.

### 2. Configuração do Pipeline de Telemetria
*   **Descrição:** Configurar o OTel Collector para receber dados via OTLP e rotear Logs para o Loki e Traces para o Tempo.
*   **DoD:** Realizar um teste de ingestão manual (via curl ou script simples) e visualizar o dado no Grafana.

### 3. Setup de Dashboards e Provisionamento
*   **Descrição:** Configurar o Grafana via scripts de provisioning para carregar automaticamente os Data Sources e Dashboards básicos de monitoramento de infraestrutura (CPU/Memória dos containers e volume de mensagens no Kafka).
*   **DoD:** Ao subir o docker-compose, o Grafana deve estar pronto para uso, sem necessidade de configuração manual de fontes de dados.
