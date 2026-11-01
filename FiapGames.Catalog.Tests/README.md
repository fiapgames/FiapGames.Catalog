# FiapGames.Catalog.Tests

Testes unitários do Catalog, rodados pelo passo **Build & Test** da pipeline (`dotnet test FiapGames.Catalog.sln`).

Inclui validators de entrada e testes de sincronização OpenSearch com outbox em SQLite transacional. A integração com SQL Server, OpenSearch e Kong reais é verificada pelo runner separado `node tests/search.integration.mjs`, a partir da raiz do repositório. Veja [SEARCH.md](../SEARCH.md).

## Coberto

- `CreateGameDtoValidator` / `UpdateGameDtoValidator`: título, gênero, preço e limite de tamanho
- `PurchaseRequestDtoValidator`: `UserId` obrigatório
- `GameSearchRequestValidator`: termo, paginação e limites
- Sincronização de criação, edição e inativação; outbox durável, rollback SQL, concorrência e reconstrução em lotes

## Pendente

Para as classes com dependências, use mocks. Sugestão: NSubstitute.

- [ ] `GameService`: criar, editar, listar e invalidar o cache do Redis depois de cada escrita
- [ ] `PurchaseService`: jogo inexistente ou inativo, jogo já na biblioteca, pedido criado e `OrderPlacedEvent` publicado
- [ ] `PaymentProcessedConsumer`: pagamento aprovado libera o jogo na biblioteca; rejeitado grava o motivo
- [ ] `CacheStore`: hit, miss e expiração
- [ ] Controllers: códigos HTTP (201, 400, 404) de `GamesController`, `OrdersController` e `LibraryController`
- [x] Busca: indexação e `/search` fuzzy, relevância, inativação, indisponibilidade e reconstrução (unitários e integração)
