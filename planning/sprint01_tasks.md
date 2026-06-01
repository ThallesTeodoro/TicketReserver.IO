# Sprint 01 Tasks - Infraestrutura Base (sprint01_tasks.md)

## Task 01: Infra-as-Code Local (Docker)
- [ ] **1.1:** Criar diretório `infrastructure/docker` para arquivos de configuração (Grafana, OTel, etc).
- [ ] **1.2:** Criar script `init-db.sql` para inicialização automática das bases no Postgres.
- [ ] **1.3:** Escrever o `docker-compose.yml` com as imagens oficiais e redes isoladas.
- [ ] **1.4:** Configurar variáveis de ambiente e volumes persistentes.

## Task 02: Pipeline de Observabilidade
- [ ] **2.1:** Configurar `otel-collector-config.yaml` com receivers (otlp), processors (batch) e exporters (loki, tempo, prometheus).
- [ ] **2.2:** Configurar `loki-config.yaml` para persistência de logs estruturados.
- [ ] **2.3:** Configurar `tempo-config.yaml` para rastreio distribuído.

## Task 03: Provisioning do Grafana
- [ ] **3.1:** Criar `grafana/provisioning/datasources/all.yaml` para conexão automática com Loki, Tempo e Prometheus.
- [ ] **3.2:** Importar dashboards básicos de monitoramento de containers (Docker Stats).
- [ ] **3.3:** Configurar o link "Trace-to-Logs" no Grafana para correlação automática via TraceId.

## Task 04: Validação Final
- [ ] **4.1:** Executar `docker compose up -d`.
- [ ] **4.2:** Validar acesso: AKHQ (:8085), Grafana (:3000), Redis Insight (:8001).
- [ ] **4.3:** Simular envio de um log via curl para o OTel Collector e validar presença no Loki.
