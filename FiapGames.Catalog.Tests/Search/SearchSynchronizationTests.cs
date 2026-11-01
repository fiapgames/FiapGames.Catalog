using FiapGames.Core.Dtos;
using FiapGames.Core.Models;
using FiapGames.Core.Services;
using FiapGames.Data;
using FiapGames.Data.Caching;
using FiapGames.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace FiapGames.Catalog.Tests.Search;

public sealed class SearchSynchronizationTests : IDisposable
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private readonly FakeIndex index = new();

    public SearchSynchronizationTests()
    {
        connection.Open();
        using var db = CreateDb();
        db.Database.EnsureCreated();
    }

    private CatalogDbContext CreateDb() => new(new DbContextOptionsBuilder<CatalogDbContext>().UseSqlite(connection).Options);
    private SearchIndexSynchronizer Synchronizer(CatalogDbContext db) => new(db, index, NullLogger<SearchIndexSynchronizer>.Instance);
    private GameService Service(CatalogDbContext db) => new(db, new NoCache(), Synchronizer(db));

    [Fact]
    public async Task Create_update_and_deactivate_replace_the_same_document_and_acknowledge_outbox()
    {
        using var db = CreateDb();
        var service = Service(db);
        var game = await service.CreateAsync(new("Minecraft", "Sandbox", 99, "Adventure"));
        await service.UpdateAsync(game.Id, new("Terraria", "New description", 20, "Adventure", true));
        await service.DeactivateAsync(game.Id);

        var document = Assert.Single(index.Documents).Value;
        Assert.Equal("Terraria", document.Title);
        Assert.False(document.Active);
        Assert.Equal(3, document.SearchVersion);
        Assert.Equal(0, await db.SearchIndexOutbox.CountAsync());
    }

    [Fact]
    public async Task Outage_keeps_pending_writes_durable_and_retry_reads_latest_version_after_restart()
    {
        index.Unavailable = true;
        Guid gameId;
        using (var db = CreateDb())
        {
            var game = await Service(db).CreateAsync(new("Old title", "", 1, "RPG"));
            gameId = game.Id;
            await Service(db).UpdateAsync(game.Id, new("Latest title", "", 2, "RPG", false));
            Assert.Equal(2, await db.SearchIndexOutbox.CountAsync());
        }

        index.Unavailable = false;
        using (var restartedDb = CreateDb())
        {
            await Synchronizer(restartedDb).DrainAsync(default);
            Assert.Empty(await restartedDb.SearchIndexOutbox.ToListAsync());
        }
        Assert.Equal("Latest title", index.Documents[gameId].Title);
        Assert.False(index.Documents[gameId].Active);
    }

    [Fact]
    public async Task Failed_SQL_write_does_not_create_an_outbox_record_or_search_document()
    {
        using (var db = CreateDb())
            await Assert.ThrowsAsync<DbUpdateException>(() => Service(db).CreateAsync(new(null!, "", 1, "RPG")));
        using var check = CreateDb();
        Assert.Empty(await check.Games.ToListAsync());
        Assert.Empty(await check.SearchIndexOutbox.ToListAsync());
        Assert.Empty(index.Documents);
    }

    [Fact]
    public async Task Competing_edit_is_rejected_before_it_can_overwrite_newer_document()
    {
        using var first = CreateDb();
        var game = await Service(first).CreateAsync(new("Initial", "", 1, "RPG"));
        using var second = CreateDb();
        await second.Games.SingleAsync(g => g.Id == game.Id);
        await Service(first).UpdateAsync(game.Id, new("Winning edit", "", 1, "RPG", true));
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
            Service(second).UpdateAsync(game.Id, new("Stale edit", "", 1, "RPG", true)));
        Assert.Equal("Winning edit", index.Documents[game.Id].Title);
    }

    [Fact]
    public async Task Rebuild_restores_existing_games_in_multiple_batches_including_inactive_documents()
    {
        using var db = CreateDb();
        db.Games.AddRange(Enumerable.Range(0, 205).Select(i => new Game
        {
            Id = Guid.NewGuid(), Title = $"Game {i}", Genre = "RPG", Active = i % 2 == 0, SearchVersion = 1
        }));
        await db.SaveChangesAsync();
        await Synchronizer(db).RebuildAsync(default);
        Assert.Equal(205, index.Documents.Count);
        Assert.Equal(2, index.BatchCalls);
        Assert.Contains(index.Documents.Values, g => !g.Active);
    }

    public void Dispose() => connection.Dispose();

    private sealed class FakeIndex : IGameSearchIndex
    {
        public bool Unavailable { get; set; }
        public int BatchCalls { get; private set; }
        public Dictionary<Guid, Game> Documents { get; } = [];
        public Task<bool> EnsureCreatedAsync(CancellationToken cancellationToken) => Task.FromResult(false);
        public Task IndexAsync(Game game, CancellationToken cancellationToken)
        {
            if (Unavailable) throw new SearchUnavailableException("Simulated outage");
            Documents[game.Id] = new Game
            {
                Id = game.Id, Title = game.Title, Active = game.Active, SearchVersion = game.SearchVersion
            };
            return Task.CompletedTask;
        }
        public async Task IndexBatchAsync(IReadOnlyCollection<Game> games, CancellationToken cancellationToken)
        {
            BatchCalls++;
            foreach (var game in games) await IndexAsync(game, cancellationToken);
        }
        public Task<GameSearchResult> SearchAsync(GameSearchRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class NoCache : ICacheStore
    {
        public Task<T> GetOrSetAsync<T>(string key, Func<CancellationToken, Task<T>> factory,
            TimeSpan ttl, CancellationToken cancellationToken = default) => factory(cancellationToken);
        public Task RemoveAsync(string key, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
