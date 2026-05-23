# Regras de Engenharia Inegociáveis (RULES.md)

Este documento dita os padrões arquiteturais absolutos do projeto TicketReserver.IO. Qualquer desvio dessas regras deve ser sumariamente rejeitado pelo modo `arch-guardrails`.

## 1. Restrições de Componentização
*   **Outbox Isolation:** O processamento da tabela de Outbox é expressamente proibido de rodar dentro do processo da `API-Reserva` (seja via `BackgroundService`, `IHostedService` ou bibliotecas como Coravel/Quartz internas). Ele deve ser implementado no componente independente `Outbox-Relay`.
*   **Database Isolation:** É expressamente proibido o compartilhamento de bases de dados (tabelas, esquemas ou instâncias físicas) entre os microsserviços. Cada domínio (`Reserva`, `Pagamento`, `Ticketing`) possui sua própria string de conexão e ciclo de vida de migração independentes.

## 2. Padrões de Mensageria (Kafka)
*   **Manual Offsets:** O uso de `EnableAutoCommit = true` no Kafka Consumer está proibido. Todos os consumidores .NET devem confirmar o processamento manualmente (`consumer.Commit(consumeResult)`) apenas após a persistência bem-sucedida no banco de dados local.
*   **Idempotency Over Memory:** Nenhum estado de mensagem processada deve ser mantido unicamente em memória RAM do Worker. A validação de mensagem já processada deve bater no banco de dados persistente correspondente.

## 3. Fluxo de Execução de AI SKILLS
Quando uma skill for invocada na CLI, execute rigorosamente o protocolo abaixo:
*   `arch-guardrails`: Escaneie o código proposto e aponte violações a este arquivo antes de gerar o código final.
*   `architectural-review`: Aplique a análise tripla (Tech Lead, QA, Security) gerando um relatório em formato Markdown estruturado.
*   `auto-fix`: Exija a stack trace completa do log estruturado e o código-fonte atual antes de inferir correções.
*   `commit-standardizer`: Formate saídas baseadas estritamente em Conventional Commits (`feat:`, `fix:`, `chore:`, `refactor:`, `docs:`).
*   `unit-test-generator`: Proíba testes que utilizem mocks para comportamento interno da SAGA. Exija testes focados em injeção de falhas e concorrência (ex: Race Conditions na reserva).