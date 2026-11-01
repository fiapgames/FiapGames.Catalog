namespace FiapGames.Core.Dtos;

public sealed record GameSearchRequest
{
    public string Q { get; init; } = string.Empty;
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public sealed record GameSearchHit(GameDto Game, double Score);
public sealed record GameSearchResult(IReadOnlyList<GameSearchHit> Items, long Total, int Page, int PageSize);
