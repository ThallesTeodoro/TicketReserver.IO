# Sprint 03 Tasks - Outbox Relay (sprint03_tasks.md)

## Task 01: Setup do Worker
- [x] **1.1:** Criar projeto `TR.HW.OutboxRelay` na pasta `source/`.
- [x] **1.2:** Instalar pacotes: `Hangfire.AspNetCore`, `Hangfire.PostgreSql`, `Confluent.Kafka`, `Dapper`, `Npgsql`.
- [x] **1.3:** Configurar injeção de dependência e leitura de `appsettings.json`.

## Task 02: Infraestrutura de Jobs (Hangfire)
- [x] **2.1:** Configurar `GlobalConfiguration` do Hangfire para usar Postgres.
- [x] **2.2:** Registrar o `BackgroundJobServer` no ciclo de vida da aplicação.
- [x] **2.3:** Criar o job recorrente com intervalo de 15 a 30 segundos (ou configurável).

## Task 03: Implementação do Relay Logic
- [x] **3.1:** Criar serviço `KafkaProducerService` para abstrair o `IProducer`.
- [x] **3.2:** Implementar lógica de leitura da tabela `OutboxMessages` via Dapper.
- [x] **3.3:** Implementar loop de processamento com propagação do `TraceId` (W3C Headers).
- [x] **3.4:** Implementar remoção segura das mensagens processadas.

## Task 04: Observabilidade e Logs
- [ ] **4.1:** Configurar Exportador OTLP para Traces e Logs.
- [ ] **4.2:** Adicionar logs estruturados informando volume de mensagens processadas e falhas.

## Task 05: Validação Integrada
- [ ] **5.1:** Teste de ponta a ponta: Criar reserva via API -> Verificar persistência -> Aguardar Relay -> Verificar mensagem no Kafka via AKHQ.
