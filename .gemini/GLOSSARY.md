# Glossário do Projeto (Ubiquitous Language)

Este documento define os termos de domínio e técnicos do sistema **TicketReserver.IO**. A IA deve consultar e utilizar exclusivamente estes termos na geração de código, entidades e tabelas.

## Domínio (Negócio)

*   **Evento (Event):** O show, palestra ou concerto para o qual os ingressos estão sendo vendidos.
*   **Assento (Seat):** A unidade física ou virtual que o usuário deseja comprar. Pode estar nos estados: `Disponivel`, `Bloqueado` (durante a SAGA) ou `Vendido`.
*   **Reserva (Reservation):** A intenção de compra de um Assento. Possui ciclo de vida curto (ex: 10 minutos). *Não confundir com Ingresso.*
*   **Pagamento (Payment):** A transação financeira. Pode ser `Aprovado`, `Recusado` ou `Falho` (erro técnico).
*   **Ingresso (Ticket):** O artefato final (QR Code/PDF) que garante a entrada do usuário no Evento. Só existe após a conclusão com sucesso do Pagamento.

## Infraestrutura e Arquitetura

*   **Idempotency-Key (Chave de Idempotência):** Um GUID enviado pelo client no header HTTP (`X-Idempotency-Key`) para garantir que uma requisição seja processada apenas uma vez.
*   **Outbox Relay:** O worker segregado responsável por ler a tabela `OutboxMessages` no Postgres e publicar os eventos no Kafka. *Nunca chame de BackgroundJob da API.*
*   **Integration Event (Evento de Integração):** Mensagens publicadas no Kafka para comunicação entre serviços (ex: `ReservationCreatedEvent`).
*   **SAGA Pivot:** O ponto sem volta do nosso fluxo distribuído. Neste projeto, a transação na operadora de Pagamento é o Pivot.
*   **Compensação (Compensation):** Ação de desfazer uma transação local anterior caso a SAGA falhe (ex: liberar o Assento se o Pagamento for recusado).
*   **TraceId:** O identificador único do OpenTelemetry que acompanha toda a jornada do usuário (da API até o último Worker).
*   **Dead Letter Queue (DLQ):** Tópico do Kafka destinado a armazenar mensagens que não puderam ser processadas após X retries (Poison Pills).