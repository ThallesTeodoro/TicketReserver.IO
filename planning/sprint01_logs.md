# Sprint 01 Logs - TicketReserver.IO

Este arquivo documenta todas as ações, decisões técnicas e execuções realizadas durante a Sprint 01.

## [22/05/2026] - Inicialização da Sprint
*   **Status:** Documentação consolidada e ambiente pronto para o setup de infraestrutura.
*   **Ações:**
    *   Criada estrutura de diretórios em `infrastructure/docker/`.
    *   Criado script `init-db.sql` com as bases `reservation_db`, `payment_db` e `ticketing_db`.
    *   Configurado `docker-compose.yml` com a stack completa: Postgres, Redis, Kafka, OTel, Loki, Tempo e Grafana.
    *   Configurados Data Sources do Grafana com integração Trace-to-Logs.
    *   Configurado OTel Collector para roteamento de Traces, Logs e Metrics.
    *   **Execução:** `docker compose up -d` executado. Inicialmente houve falha na imagem `bitnami/kafka:latest`.
    *   **Correção:** Substituída pela imagem `confluentinc/cp-kafka:7.6.1` com configuração KRaft estável.
    *   **Ajustes Técnicos:** Corrigidas configurações de Loki 3.0, Tempo 3.0 e OTel Collector (exporters atualizados para `otlphttp` e `debug`).
    *   **Validação:** Todos os containers em status `Up`. Teste de ingestão manual realizado com sucesso (`infra-test` log persistido no Loki).


