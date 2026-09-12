using FiapGames.Data;
using FiapGames.Data.Caching;
using FiapGames.Core.Services;
using FiapGames.Core.Dtos;
using FiapGames.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace FiapGames.Services;

public class GameService(CatalogDbContext dbContext, ICacheStore cacheStore) : IGameService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    private const string AllGamesCacheKey = "catalog:games:all";

    private static string GameCacheKey(Guid id) => $"catalog:game:{id}";

    public async Task<List<GameDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await cacheStore.GetOrSetAsync(
            AllGamesCacheKey,
            ct => dbContext.Games
                .OrderBy(g => g.Title)
                .Select(g => ToDto(g))
                .ToListAsync(ct),
            CacheTtl,
            cancellationToken);
    }

    public async Task<GameDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await cacheStore.GetOrSetAsync(
            GameCacheKey(id),
            async ct =>
            {
                var game = await dbContext.Games.FindAsync([id], ct);
                return game is null ? null : ToDto(game);
            },
            CacheTtl,
            cancellationToken);
    }

    public async Task<GameDto> CreateAsync(CreateGameDto dto, CancellationToken cancellationToken = default)
    {
        var game = new Game
        {
            Id = Guid.NewGuid(),
            Title = dto.Title,
            Description = dto.Description,
            Price = dto.Price,
            Genre = dto.Genre,
            Active = true,
            CreatedAt = DateTime.UtcNow
        };

        dbContext.Games.Add(game);
        await dbContext.SaveChangesAsync(cancellationToken);

        await cacheStore.RemoveAsync(AllGamesCacheKey, cancellationToken);

        return ToDto(game);
    }

    public async Task<GameDto?> UpdateAsync(Guid id, UpdateGameDto dto, CancellationToken cancellationToken = default)
    {
        var game = await dbContext.Games.FindAsync([id], cancellationToken);
        if (game is null)
        {
            return null;
        }

        game.Title = dto.Title;
        game.Description = dto.Description;
        game.Price = dto.Price;
        game.Genre = dto.Genre;
        game.Active = dto.Active;

        await dbContext.SaveChangesAsync(cancellationToken);

        await cacheStore.RemoveAsync(AllGamesCacheKey, cancellationToken);
        await cacheStore.RemoveAsync(GameCacheKey(id), cancellationToken);

        return ToDto(game);
    }

    public async Task<bool> DeactivateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var game = await dbContext.Games.FindAsync([id], cancellationToken);
        if (game is null)
        {
            return false;
        }

        game.Active = false;
        await dbContext.SaveChangesAsync(cancellationToken);

        await cacheStore.RemoveAsync(AllGamesCacheKey, cancellationToken);
        await cacheStore.RemoveAsync(GameCacheKey(id), cancellationToken);

        return true;
    }

    private static GameDto ToDto(Game game) =>
        new(game.Id, game.Title, game.Description, game.Price, game.Genre, game.Active, game.CreatedAt);
}
