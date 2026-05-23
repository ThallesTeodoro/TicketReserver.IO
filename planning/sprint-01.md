# Sprint 01 - Infraestrutura Base, Telemetria e API Inicial (sprint-01.md)

## Objetivo da Sprint
Montar todo o ecossistema de ferramentas em contêineres Docker, estruturar a injeção do OpenTelemetry na API de Reserva e validar o recebimento de logs estruturados e traces correlacionados no Grafana Loki.

## Tarefas Detalhadas

### 1. Infra-as-Code Local (`docker-compose.yml`)
*   **Descrição:** Montar o ambiente contendo: Postgres (3 bases lógicas segregadas), Redis Stack, Apache Kafka (KRaft mode), OpenTelemetry Collector (configurado para exportar em OTLP), Grafana Loki (Logs), Grafana Tempo (Traces) e AKHQ (Interface Kafka).
*   **DoD (Definition of Done):** Todos os contêineres de pé e a interface do Grafana e do AKHQ acessíveis via navegador.

### 2. Boilerplate .NET & OpenTelemetry Setup
*   **Descrição:** Criar a Minimal API `Service-Reservation`. Configurar o SDK do OpenTelemetry no `Program.cs` para capturar métricas, traces e logs do ASP.NET Core e do Entity Framework Core.
*   **DoD:** Rodar a API localmente, fazer uma chamada HTTP e visualizar o Trace gerado de forma gráfica dentro do painel do Grafana Tempo.

### 3. Implementação do Middleware de Idempotência (Redis)
*   **Descrição:** Criar um Action Filter ou Middleware customizado que intercepta o header `X-Idempotency-Key`. Implementar a validação atômica no Redis.
*   **DoD:** Executar duas chamadas HTTP idênticas em menos de 5 segundos via Postman/Insomnia; a primeira deve retornar `201 Created` e a segunda deve retornar `409 Conflict` (ou o retorno cacheado), exibindo a tag de idempotência no log do Loki.