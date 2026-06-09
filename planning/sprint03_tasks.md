# Sprint 03 Tasks - Outbox Relay Robusto (sprint03_tasks.md)

## Task 01: Setup do Worker [CONCLUÍDA]
- [x] **1.1:** Criar projeto `TR.HW.OutboxRelay` na pasta `source/`.
- [x] **1.2:** Instalar pacotes: `Hangfire.AspNetCore`, `Hangfire.PostgreSql`, `Confluent.Kafka`, `Dapper`, `Npgsql`.
- [x] **1.3:** Configurar injeção de dependência e leitura de `appsettings.json`.

## Task 02: Infraestrutura de Jobs (Hangfire) [CONCLUÍDA]
- [x] **2.1:** Configurar `GlobalConfiguration` do Hangfire para usar Postgres.
- [x] **2.2:** Registrar o `BackgroundJobServer` no ciclo de vida da aplicação.
- [x] **2.3:** Criar o job recorrente com intervalo configurável via appsettings.

## Task 03: Implementação do Relay Logic (Simplificado) [CONCLUÍDA - SERÁ REFATORADA]
- [x] **3.1:** Criar serviço `KafkaProducerService`.
- [x] **3.2:** Implementar lógica de leitura e deleção direta.

## Task 04: Observabilidade e Logs [CONCLUÍDA]
- [x] **4.1:** Configurar Exportador OTLP para Traces e Logs.
- [x] **4.2:** Adicionar instrumentação nos Use Cases.

## Task 05: Contratos de Eventos e Status [CONCLUÍDA]
- [x] **5.1:** Criar enum `OutboxStatus` (Pendente=1, EmProcessamento=2, Processado=3, ErroPermanente=4).
- [x] **5.2:** Criar interface `IEvent` e evento `ReservationCreated`.
- [x] **5.3:** Garantir que o domínio da API e do Worker compartilhem ou repliquem estes contratos.

## Task 06: Evolução do Schema e Modelos [CONCLUÍDA]
- [x] **6.1:** Atualizar `03-data-models.md` com as novas colunas (`Status` as SMALLINT, `EventName`, `Attempts`, etc).
- [x] **6.2:** Criar script de migração/ajuste para a tabela `OutboxMessages`.
- [x] **6.3:** Atualizar records de `OutboxMessage` no C# para refletir o novo banco.

## Task 07: Refatoração da API (Publisher) [CONCLUÍDA]
- [x] **7.1:** Atualizar `ReservationRepository` para preencher os novos campos no `INSERT` do Outbox.
- [x] **7.2:** Garantir que o `EventName` seja persistido corretamente.

## Task 08: Refatoração do Worker (State Machine Relay) [CONCLUÍDA]
- [x] **8.1:** Alterar polling para buscar apenas mensagens elegíveis (`Pendente` ou timeout em `EmProcessamento`).
- [x] **8.2:** Implementar transição para `EmProcessamento` com concorrência segura.
- [x] **8.3:** Implementar conclusão para `Processado` após confirmação do Kafka.
- [x] **8.4:** Implementar lógica de retry incremental e marcação de `ErroPermanente`.

## Task 09: Validação Integrada e Resiliência
- [ ] **9.1:** Validar fluxo de sucesso: Mensagem fica como `Processado` no banco.
- [ ] **9.2:** Validar fluxo de erro: Simular queda do Kafka e verificar incremento de `Attempts`.
