# FiapGames.Catalog.Tests

Testes unitários do Catalog, rodados pelo passo **Build & Test** da pipeline (`dotnet test FiapGames.Catalog.sln`).

**Esta é só a base**, criada para a pipeline da Fase 4 ter o que executar. Hoje cobre apenas os validators de entrada.

## Coberto

- `CreateGameDtoValidator` / `UpdateGameDtoValidator`: título, gênero, preço e limite de tamanho
- `PurchaseRequestDtoValidator`: `UserId` obrigatório

## Pendente

Para as classes com dependências, use mocks. Sugestão: NSubstitute.

- [ ] `GameService`: criar, editar, listar e invalidar o cache do Redis depois de cada escrita
- [ ] `PurchaseService`: jogo inexistente ou inativo, jogo já na biblioteca, pedido criado e `OrderPlacedEvent` publicado
- [ ] `PaymentProcessedConsumer`: pagamento aprovado libera o jogo na biblioteca; rejeitado grava o motivo
- [ ] `CacheStore`: hit, miss e expiração
- [ ] Controllers: códigos HTTP (201, 400, 404) de `GamesController`, `OrdersController` e `LibraryController`
- [ ] Busca: indexação no OpenSearch a cada insert/update e o `/search` com fuzzy, quando forem implementados
