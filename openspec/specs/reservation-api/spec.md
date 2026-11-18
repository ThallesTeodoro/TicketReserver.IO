# reservation-api Specification

## Purpose

Prover o scaffolding arquitetural e a fundação técnica do microsserviço de Reservas (TR.API.Reservation) em C# .NET 10, com Clean Architecture, CQRS Lite nativo, Minimal APIs com Carter, envelope JsonResponse, integração inicial de persistência e health checks conteinerizados.

## Requirements

### Requirement: Estrutura da Solução e Clean Architecture
A solução SHALL implementar isolamento estrito entre quatro camadas (Domain, Application, Infrastructure, WebApi) e uma suíte de testes unitários, adotando CQRS Lite nativo e rejeitando o uso de bibliotecas de terceiros como MediatR e AutoMapper.

#### Scenario: Compilação e isolamento entre camadas
- **WHEN** a solução `TR.API.Reservation.sln` é compilada via `dotnet build`
- **THEN** o projeto Domain não referencia pacotes externos e a compilação é concluída com zero erros

#### Scenario: Ausência de pacotes proibidos
- **WHEN** a árvore de dependências dos projetos é inspecionada
- **THEN** nenhum pacote contendo `MediatR` ou `AutoMapper` está presente nos arquivos `.csproj`

### Requirement: Envelopamento Padronizado e Tratamento Global de Erros
A camada WebApi SHALL envelopar todas as respostas HTTP no contrato `JsonResponse<TData, TError>` e implementar middleware global para capturar exceções de domínio e validação.

#### Scenario: Retorno de resposta HTTP envelopada
- **WHEN** uma requisição bem-sucedida é processada por um endpoint Carter
- **THEN** o corpo da resposta contém as propriedades `statusCode`, `message`, `data` e `errors`

#### Scenario: Interceptação de exceção de domínio
- **WHEN** uma operação dispara uma exceção do tipo `DomainException`
- **THEN** o `ExceptionHandlerMiddleware` captura o erro e retorna status HTTP 400 com envelope `JsonResponse` estruturado

### Requirement: Verificação de Saúde e Monitoramento
A aplicação WebApi SHALL expor o endpoint de sondagem de integridade em `/healthz` para verificação de liveness e readiness do serviço.

#### Scenario: Consulta de saúde do serviço
- **WHEN** uma requisição HTTP GET é enviada para `/healthz`
- **THEN** a aplicação responde com status HTTP 200 OK indicando estado operacional saudável

### Requirement: Empacotamento Conteinerizado e Execução Modular
A aplicação SHALL disponibilizar build multi-stage em Dockerfile otimizado para .NET 10 e arquivo `docker-compose.yml` fragmentado, conectando-se aos serviços `postgres` e `redis` na rede interna `ticketreserver-internal-net`.

#### Scenario: Execução conteinerizada do microsserviço
- **WHEN** a stack Docker Compose é inicializada
- **THEN** o container `ticketreserver-reservation-api` inicia na porta interna 8080 (mapeada para 5003 no host) e atinge status saudável

### Requirement: Retenção Temporária de Assentos e Lotes (Seat Hold)
A API de Reserva SHALL disponibilizar a operação de retenção temporária de assentos numerados e cotas de lotes gerais via endpoint `POST /reserva/hold`, garantindo exclusão mútua na memória RAM com TTL de 10 minutos (600s), persistência atômica no PostgreSQL e publicação transacional diferida via Outbox.

#### Scenario: Retenção bem-sucedida de assentos numerados
- **WHEN** uma requisição válida para reter assentos numerados disponíveis é processada
- **THEN** o sistema adquire o lock atômico no Redis, persiste a reserva com status `AWAITING_PAYMENT`, registra o evento `reservation.hold_created` com delay de 600 segundos no outbox e retorna status HTTP 201 Created

#### Scenario: Colisão de assento com reversão imediata (Fast-Fail)
- **WHEN** uma requisição tenta reter um ou mais assentos que já estão em posse de outro usuário
- **THEN** o sistema falha na aquisição do lock no Redis, reverte imediatamente quaisquer locks adquiridos na mesma operação, não efetua gravações de reserva no banco e retorna status HTTP 409 Conflict

#### Scenario: Retenção atômica de cota para lotes não-numerados
- **WHEN** uma requisição solicita ingressos de pista comum sem assento específico vinculado
- **THEN** o sistema deduz a cota solicitada atomicamente no Redis via script Lua e persiste os itens na reserva mantendo consistência com o inventário disponível

### Requirement: Início de Checkout e Transição de Pagamento
A API de Reserva SHALL processar solicitações de checkout através do endpoint `POST /reserva/checkout`, validando o prazo de retenção (hold), alterando o estado para `PROCESSING_PAYMENT` sob controle de concorrência otimista e gerando o evento imediato de checkout no Outbox.

#### Scenario: Checkout bem-sucedido dentro do prazo de retenção
- **WHEN** o cliente proprietário da reserva submete o checkout com dados de pagamento antes do término do tempo limite de 10 minutos
- **THEN** o sistema atualiza o status da reserva para `PROCESSING_PAYMENT`, incrementa a versão de concorrência, grava o evento `reservation.checkout_req` com delay zero no outbox e responde com status HTTP 202 Accepted

#### Scenario: Rejeição de checkout por expiração do hold
- **WHEN** uma requisição de checkout é recebida após o término do prazo do hold temporário
- **THEN** o sistema atualiza a reserva para `EXPIRED`, libera as retenções correspondentes no Redis e rejeita a operação com status HTTP 409 Conflict

#### Scenario: Rejeição de concorrência concorrente sob OCC
- **WHEN** duas requisições concorrentes tentam iniciar o checkout para a mesma reserva simultaneamente
- **THEN** o controle de concorrência otimista (OCC) assegura que apenas uma transição ocorra e a requisição conflitante seja rejeitada

### Requirement: Consulta de Reserva com Contagem Regressiva
A API de Reserva SHALL fornecer a consulta detalhada do estado atual da reserva através do endpoint `GET /reserva/{id}`, informando status, itens, tempo restante de hold e indicador de expiração.

#### Scenario: Consulta de reserva ativa com tempo restante
- **WHEN** uma requisição consulta uma reserva existente com status `AWAITING_PAYMENT`
- **THEN** a resposta HTTP 200 OK retorna os dados da reserva juntamente com `remainingHoldSeconds` e o booleano `isExpired = false`

#### Scenario: Consulta de reserva inexistente
- **WHEN** uma requisição consulta um identificador de reserva que não existe no banco de dados
- **THEN** a aplicação retorna status HTTP 404 Not Found com envelope padronizado de erro

### Requirement: Cancelamento Voluntário Pré-Checkout
A API de Reserva SHALL permitir que o cliente cancele voluntariamente sua reserva pendente através do endpoint `DELETE /reserva/{id}`, liberando antecipadamente as retenções em memória no Redis.

#### Scenario: Cancelamento com liberação antecipada no Redis
- **WHEN** o cliente solicita o cancelamento de uma reserva com status `AWAITING_PAYMENT`
- **THEN** o sistema libera as chaves e cotas no Redis, transiciona o status da reserva para `CANCELLED` com motivo `USER_CANCELLED` e retorna status HTTP 200 OK

#### Scenario: Rejeição de cancelamento direto para reserva em processamento ou confirmada
- **WHEN** uma requisição tenta cancelar diretamente uma reserva que já está em `PROCESSING_PAYMENT` ou `CONFIRMED`
- **THEN** o sistema rejeita a operação com status HTTP 409 Conflict, indicando que o fluxo deve seguir as regras de compensação/estorno da saga
