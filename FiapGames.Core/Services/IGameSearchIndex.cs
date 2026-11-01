using FiapGames.Core.Dtos;
using FiapGames.Core.Models;

namespace FiapGames.Core.Services;

public interface IGameSearchIndex
{
    Task<bool> EnsureCreatedAsync(CancellationToken cancellationToken);
    Task IndexAsync(Game game, CancellationToken cancellationToken);
    Task IndexBatchAsync(IReadOnlyCollection<Game> games, CancellationToken cancellationToken);
    Task<GameSearchResult> SearchAsync(GameSearchRequest request, CancellationToken cancellationToken);
}

public sealed class SearchUnavailableException(string message, Exception? inner = null) : Exception(message, inner);
