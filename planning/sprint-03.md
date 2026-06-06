# Sprint 03 - Resiliência Assíncrona & Outbox Relay (sprint-03.md)

## Objetivo da Sprint
Implementar o componente `TR.HW.OutboxRelay` para garantir a entrega garantida (At-Least-Once) de eventos do banco de dados para o Apache Kafka. O worker utilizará **Hangfire** para orquestração de jobs recorrentes e persistência no Postgres, assegurando que nenhum evento de reserva seja perdido mesmo em caso de falhas temporárias do broker.

## Arquitetura do Worker
*   **Tipo:** .NET Console Application / Worker Service.
*   **Orquestrador:** Hangfire (Job Recorrente).
*   **Storage:** Postgres (Schema compartilhado ou dedicado para o Hangfire).
*   **Confiabilidade:** O job lerá a tabela `OutboxMessages`, publicará no Kafka e, somente após a confirmação do Broker (Ack), removerá ou marcará a mensagem no banco.

## Tarefas Detalhadas

### 1. Setup do Projeto OutboxRelay
*   **Descrição:** Criar a solução/projeto `TR.HW.OutboxRelay`. Diferente da API, este worker pode ter uma estrutura mais simples, mas deve manter a separação de responsabilidades (Infrastructure para Kafka e DB).
*   **DoD:** Projeto criado, compilando e configurado para rodar como um Background Service.

### 2. Configuração do Hangfire
*   **Descrição:** Instalar e configurar o Hangfire com storage no Postgres. Configurar o `BackgroundJobServer` e a dashboard (opcional, apenas para debug local).
*   **DoD:** Tabelas do Hangfire criadas no Postgres e servidor de jobs ativo.

### 3. Integração com Kafka (Producer)
*   **Descrição:** Implementar o Client de produção do Kafka utilizando `Confluent.Kafka`. Deve suportar a propagação de metadados (Headers) como o `traceparent`.
*   **DoD:** Classe wrapper para o Kafka Producer validada.

### 4. Job de Relay (Lógica de Negócio)
*   **Descrição:** Implementar o método que será invocado pelo Hangfire. 
    *   Leitura de N mensagens pendentes.
    *   Loop de publicação com tratamento de erro por mensagem.
    *   Confirmação e Deleção atômica (ou marcação de `ProcessedAt`).
*   **DoD:** Mensagem sai da tabela `OutboxMessages` e aparece no tópico do Kafka (validado via AKHQ).

### 5. Observabilidade e Resiliência
*   **Descrição:** Adicionar instrumentação OpenTelemetry (Traces e Logs) para rastrear o tempo entre a criação da reserva na API e a publicação pelo Worker. Configurar Retries no Hangfire.
*   **DoD:** Traces visíveis no Tempo mostrando o fluxo completo: API -> DB -> Worker -> Kafka.
