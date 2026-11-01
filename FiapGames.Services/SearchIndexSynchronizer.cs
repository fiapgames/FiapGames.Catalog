using FiapGames.Core.Models;
using FiapGames.Core.Services;
using FiapGames.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FiapGames.Services;

public sealed class SearchIndexSynchronizer(
    CatalogDbContext db, IGameSearchIndex index, ILogger<SearchIndexSynchronizer> logger)
{
    public async Task TrySynchronizeAsync(Game game, CancellationToken cancellationToken)
    {
        try
        {
            await SynchronizeAsync(game, cancellationToken);
        }
        catch (SearchUnavailableException ex)
        {
            // SQL has committed; the durable outbox will be retried by the worker.
            logger.LogWarning(ex, "Search indexing pending for game {GameId}, version {Version}", game.Id, game.SearchVersion);
        }
    }

    private async Task SynchronizeAsync(Game game, CancellationToken cancellationToken)
    {
        await index.IndexAsync(game, cancellationToken);
        await db.SearchIndexOutbox.Where(m => m.GameId == game.Id && m.Version <= game.SearchVersion)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task DrainAsync(CancellationToken cancellationToken)
    {
        var ids = await db.SearchIndexOutbox.AsNoTracking().OrderBy(m => m.CreatedAt)
            .Take(200).Select(m => m.GameId).ToListAsync(cancellationToken);
        foreach (var id in ids.Distinct())
        {
            var game = await db.Games.AsNoTracking().SingleAsync(g => g.Id == id, cancellationToken);
            await SynchronizeAsync(game, cancellationToken);
        }
    }

    public async Task RebuildAsync(CancellationToken cancellationToken)
    {
        await index.EnsureCreatedAsync(cancellationToken);
        const int batchSize = 200;
        var offset = 0;
        while (true)
        {
            var games = await db.Games.AsNoTracking().OrderBy(g => g.Id).Skip(offset).Take(batchSize)
                .ToListAsync(cancellationToken);
            if (games.Count == 0) break;
            await index.IndexBatchAsync(games, cancellationToken);
            offset += games.Count;
        }
        await DrainAsync(cancellationToken);
        logger.LogInformation("Search index rebuilt from SQL Server ({Count} games)", offset);
    }
}
