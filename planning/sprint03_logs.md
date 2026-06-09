# Sprint 03 Logs - TicketReserver.IO

Este arquivo documenta todas as ações, decisões técnicas e execuções realizadas durante a Sprint 03.

## [04/06/2026] - Planejamento da Sprint
*   **Status:** Sprint 03 iniciada.
*   **Decisões Técnicas:**
    *   **Hangfire:** Escolhido como orquestrador por oferecer persistência robusta, retries automáticos e uma dashboard nativa para monitoramento de falhas.
    *   **Estratégia de Relay:** Optou-se por Polling (Pull) em vez de Push direto da API para garantir desacoplamento total e resiliência caso o Kafka esteja indisponível no momento da transação da API.
    *   **Headers Kafka:** Obrigatória a injeção do Header `traceparent` para manter o rastreio distribuído iniciado na API.

## [05/06/2026] - Reestruturação para Outbox Robusto
*   **Decisão Técnica:** Evolução do Outbox de um modelo "delete-after-process" para uma "Máquina de Estados" persistente.
*   **Motivação:** Necessidade de auditoria, rastreabilidade de falhas e suporte a diferentes tipos de eventos tipados.
*   **Mudança de Schema:** Status migrado para SMALLINT (Enum) e inclusão de campos de controle de tentativas (`Attempts`, `LastAttemptAt`, `ErrorReason`).
