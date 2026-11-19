# Tasks

## 1. Scaffolding da Solução e Projetos do Worker

- [x] 1.1 Criar a solução `TR.HW.ReservationOutboxRelay.sln` com o projeto Worker Service em .NET 10 (`net10.0`) e projeto de testes unitários `TR.HW.ReservationOutboxRelay.UnitTests`, configurando pacotes essenciais (`Confluent.Kafka`, `Npgsql`, `Dapper`, `Microsoft.Extensions.Hosting`, `Serilog`), verificando a compilação com `dotnet build`.
- [x] 1.2 Configurar `appsettings.json`, classes fortemente tipadas de configuração (`KafkaProducerOptions`, `OutboxRelayOptions`) e injeção de dependências, verificando a resolução via testes de infraestrutura.

## 2. Acesso a Dados e Leitura Segura de Outbox

- [x] 2.1 Implementar `IOutboxRepository` e `OutboxRepository` com Dapper executando queries parametrizadas com `SELECT ... FOR UPDATE SKIP LOCKED` e filtro `deliver_after <= NOW()`, verificando com testes unitários e de consulta.
- [x] 2.2 Implementar métodos de atualização transacional de status (`MarkAsPublishedAsync`, `MarkAsFailedAsync` e `IncrementRetryAsync`), validando a conformidade de transições e backoff exponencial com testes unitários.

## 3. Produtor Kafka e Roteamento de Mensagens

- [x] 3.1 Implementar `IKafkaProducer` e `KafkaProducer` com `Confluent.Kafka` configurado com `Acks = All`, serialização JSON de envelope padronizado, partition key por `AggregateId` e injeção de headers W3C `traceparent`, verificando com testes unitários mockados.
- [x] 3.2 Implementar a lógica de roteamento por `EventType` (`reservation.checkout_req` -> `reservation.v1.checkout_req` e `reservation.hold_created` -> `reservation.v1.hold_created.delayed`) com fallback para DLQ (`reservation.v1.checkout_req.DLQ`), verificando com testes de roteamento.

## 4. Orquestração do Worker, Dockerfile e Validação Integrada

- [x] 4.1 Implementar o `ReservationOutboxRelayWorker` herdando de `BackgroundService` com loop de polling adaptativo (drenagem contínua quando há dados e backoff suave quando vazio), verificando com testes unitários de ciclo de vida.
- [x] 4.2 Criar Dockerfile multi-stage .NET 10, fragmento `docker-compose.yml` conectado à rede `ticketreserver-internal-net` e executar validação estrita de estilo com `dotnet format --verify-no-changes` e suíte completa de testes com `dotnet test`.
