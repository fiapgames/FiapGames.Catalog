# FiapGames.Catalog

ASP.NET Core Web API responsável pelo catálogo de jogos do FiapGames (CatalogAPI). Faz o CRUD de jogos, inicia o fluxo de compra publicando `OrderPlacedEvent` e consome `PaymentProcessedEvent` para adicionar o jogo à biblioteca do usuário quando o pagamento é aprovado.

## Arquitetura

Solução dividida em 4 projetos:

```
FiapGames.Core/      Models (Game, Order, OrderStatus, UserGameLibrary), Dtos e interfaces de serviço
                      (IGameService, IPurchaseService, PurchaseResult) — sem dependências externas
FiapGames.Data/       CatalogDbContext + Migrations do EF Core
FiapGames.Services/   Implementações (GameService, PurchaseService) e Consumers MassTransit
                      (PaymentProcessedConsumer)
FiapGames.Catalog/    Projeto Web: Controllers (Games, Orders, Library, Health), Validators
                      (FluentValidation) e Program.cs (composição/DI)
```

Dependências entre projetos: `Catalog -> Services -> Data -> Core` (Catalog também referencia Core e Data diretamente).

Endpoints:

| Método | Rota | Descrição |
|---|---|---|
| GET | `/games` | Lista os jogos |
| GET | `/search?q=minecrfat&page=1&pageSize=20` | Busca fuzzy por relevância no OpenSearch (JWT obrigatório) |
| GET | `/games/{id}` | Detalhe de um jogo |
| POST | `/games` | Cria um jogo |
| PUT | `/games/{id}` | Atualiza um jogo |
| DELETE | `/games/{id}` | Inativa um jogo (`Active=false`) |
| POST | `/games/{id}/purchase` | Inicia a compra (cria `Order` e publica `OrderPlacedEvent`) |
| GET | `/orders` | Lista os pedidos |
| GET | `/orders/{id}` | Consulta o status de um pedido |
| GET | `/library/{userId}` | Biblioteca do usuário (busca o usuário via request/response no RabbitMQ, depois consulta os jogos comprados) |
| GET | `/health` | Health check |

Fluxo de compra:

```
CatalogAPI -> Publish(OrderPlacedEvent) -> RabbitMQ -> PaymentsAPI
PaymentsAPI -> Publish(PaymentProcessedEvent) -> RabbitMQ -> CatalogAPI (e NotificationsAPI)
CatalogAPI consome PaymentProcessedEvent -> se Approved, adiciona o jogo à biblioteca do usuário
```

Fluxo de consulta da biblioteca (request/response síncrono sobre mensageria):

```
CatalogAPI -> Request(UserLookupRequested) -> RabbitMQ -> UsersAPI
UsersAPI -> Response(UserLookupResponded) -> RabbitMQ -> CatalogAPI
CatalogAPI consulta a própria base e retorna os jogos do usuário + dados básicos (nome/e-mail)
```

`GET /library/{userId}` retorna `404` se o usuário não existir (`Found=false` na resposta do UsersAPI), ou `503` se o UsersAPI não responder em 5s (serviço fora do ar). Verificado de ponta a ponta contra uma instância real do `Fiap.Games.Users` — os contratos (`FiapGames.Contracts.Requests.User.UserLookupRequested`/`Responded` e `FiapGames.Contracts.IntegrationEvents.UserCreatedEvent`) são compartilhados entre os dois repositórios.

## Stack

- .NET 10 / ASP.NET Core Web API
- Entity Framework Core + SQL Server
- Swagger (Swashbuckle)
- MassTransit + RabbitMQ
- FluentValidation (validação automática dos DTOs via action filter)
- OpenSearch.Client + Amazon OpenSearch Service (busca fuzzy e indexação com outbox SQL)

## Variáveis de ambiente

| Seção | Variável | Descrição |
|---|---|---|
| `ConnectionStrings` | `SqlServer` | Connection string do SQL Server |
| `RabbitMq` | `Host`, `VirtualHost`, `UserName`, `Password` | Conexão com o RabbitMQ |
| — | `ASPNETCORE_ENVIRONMENT` | Ambiente de execução (Development/Production) |
| `OpenSearch` | `Url`, `IndexName`, `RetryIntervalSeconds` | Endpoint, alias do índice e intervalo de retentativa |

## Executando localmente

```bash
docker compose up -d --build
```

Isso inicia SQL Server, MongoDB, Redis, RabbitMQ, OpenSearch e a API (porta `8080`). Gere as variáveis obrigatórias do `.env` antes da primeira execução. As migrations do EF Core são aplicadas automaticamente na inicialização. Não execute esse Compose junto com o de Orchestration, pois usam as mesmas portas e nomes de containers.

- Health check: `http://localhost:8080/health`
- Swagger: `http://localhost:8080/swagger`

## Deploy (Kubernetes)

Manifests em `k8s/`:

- `namespace.yaml`
- `configmap.yaml` — configuração não sigilosa; o Secret `catalog-api-secrets` é criado pelo `ci/secrets.sh` de Orchestration
- `deployment.yaml` — Deployment + Service da imagem `brendhom/fiapgames-catalog-api:latest`

O Secret assume um SQL Server já disponível no cluster em `sqlserver.fiapgames.svc.cluster.local`.

O Deployment também exige o ConfigMap `catalog-search-config`, criado por `FiapGames.Orchestration` com o endpoint correto do Kind ou da AWS. Veja [busca, consistência, recuperação e testes](SEARCH.md).

```bash
kubectl apply -f k8s/
kubectl get pods -n fiapgames
```

## Build e push da imagem Docker

```bash
docker build -t brendhom/fiapgames-catalog-api:latest .
docker push brendhom/fiapgames-catalog-api:latest
```
