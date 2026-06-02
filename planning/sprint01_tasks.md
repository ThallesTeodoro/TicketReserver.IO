# Sprint 01 Tasks - Infraestrutura Base (sprint01_tasks.md)

## Task 01: Infra-as-Code Local (Docker)
- [x] **1.1:** Criar diretório `infrastructure/docker` para arquivos de configuração (Grafana, OTel, etc).
- [x] **1.2:** Criar script `init-db.sql` para inicialização automática das bases no Postgres.
- [x] **1.3:** Escrever o `docker-compose.yml` com as imagens oficiais e redes isoladas.
- [x] **1.4:** Configurar variáveis de ambiente e volumes persistentes.

## Task 02: Pipeline de Observabilidade
- [x] **2.1:** Configurar `otel-collector-config.yaml` com receivers (otlp), processors (batch) e exporters (loki, tempo, prometheus).
- [x] **2.2:** Configurar `loki-config.yaml` para persistência de logs estruturados.
- [x] **2.3:** Configurar `tempo-config.yaml` para rastreio distribuído.

## Task 03: Provisioning do Grafana
- [x] **3.1:** Criar `grafana/provisioning/datasources/all.yaml` para conexão automática com Loki, Tempo e Prometheus.
- [x] **3.2:** Importar dashboards básicos de monitoramento de containers (Docker Stats).
- [x] **3.3:** Configurar o link "Trace-to-Logs" no Grafana para correlação automática via TraceId.

## Task 04: Validação Final
- [x] **4.1:** Executar `docker compose up -d`.
- [x] **4.2:** Validar acesso: AKHQ (:8085), Grafana (:3000), Redis Insight (:8001).
- [x] **4.3:** Simular envio de um log via curl para o OTel Collector e validar presença no Loki.
