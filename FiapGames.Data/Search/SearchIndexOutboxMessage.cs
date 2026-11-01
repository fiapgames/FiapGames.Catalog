namespace FiapGames.Data.Search;

// Stored in the same SQL transaction as the game. A retry reads the latest game,
// so pending updates never resurrect an old title or an inactive game.
public sealed class SearchIndexOutboxMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GameId { get; set; }
    public long Version { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
