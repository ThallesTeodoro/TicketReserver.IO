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
