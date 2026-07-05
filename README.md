# FiapGames.Catalog

ASP.NET Core Web API responsável pelo catálogo de jogos do FiapGames (CatalogAPI). Faz o CRUD de jogos, inicia o fluxo de compra publicando `OrderPlacedEvent` e consome `PaymentProcessedEvent` para adicionar o jogo à biblioteca do usuário quando o pagamento é aprovado.

## Arquitetura

```
Controllers/     Endpoints HTTP (Games, Orders, Library, Health)
Data/            DbContext (CatalogDbContext)
Models/          Entidades persistidas (Game, Order, OrderStatus, UserGameLibrary)
Dtos/            Contratos de entrada/saída da API
Validators/      Regras de validação (FluentValidation) + filtro que as aplica automaticamente
Services/        Regras de negócio (IGameService, IPurchaseService)
Consumers/       Consumers MassTransit (PaymentProcessedConsumer)
Migrations/      Migrations do EF Core
```

Endpoints:

| Método | Rota | Descrição |
|---|---|---|
| GET | `/games` | Lista os jogos |
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

## Variáveis de ambiente

| Seção | Variável | Descrição |
|---|---|---|
| `ConnectionStrings` | `SqlServer` | Connection string do SQL Server |
| `RabbitMq` | `Host`, `VirtualHost`, `UserName`, `Password` | Conexão com o RabbitMQ |
| — | `ASPNETCORE_ENVIRONMENT` | Ambiente de execução (Development/Production) |

## Dependência local do FiapGames.Contracts

Este repositório usa `FiapGames.Contracts` 1.1.0, que inclui os contratos `UserLookupRequested`/`UserLookupResponded` (namespace `FiapGames.Contracts.Requests.User`) — ainda **não publicado** no nuget.org (lá só existe a 1.0.0). Por isso:

- `nuget.config` na raiz adiciona uma fonte local (`./local-packages`) além do nuget.org.
- `local-packages/FiapGames.Contracts.1.1.0.nupkg` está commitado no repositório (exceção aberta no `.gitignore`) para que o build — inclusive dentro do Docker — funcione sem depender de nada fora deste repositório.

Quando `FiapGames.Contracts` 1.1.0 (ou superior) for publicado de verdade no nuget.org, remova `local-packages/`, a entrada `fiapgames-contracts-local` do `nuget.config` e as duas linhas correspondentes do `Dockerfile`.

## Executando localmente

```bash
docker compose up -d --build
```

Isso inicia o SQL Server e a API (porta `8080`). As migrations do EF Core são aplicadas automaticamente na inicialização.

- Health check: `http://localhost:8080/health`
- Swagger: `http://localhost:8080/swagger`

## Deploy (Kubernetes)

Manifests em `k8s/`:

- `namespace.yaml`
- `configmap.yaml` / `secret.yaml` — configuração do `catalog-api`
- `deployment.yaml` — Deployment + Service da imagem `brendhom/fiapgames-catalog-api:latest`

O Secret assume um SQL Server já disponível no cluster em `sqlserver.fiapgames.svc.cluster.local`.

```bash
kubectl apply -f k8s/
kubectl get pods -n fiapgames
```

## Build e push da imagem Docker

```bash
docker build -t brendhom/fiapgames-catalog-api:latest .
docker push brendhom/fiapgames-catalog-api:latest
```
