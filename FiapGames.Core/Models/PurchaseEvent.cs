namespace FiapGames.Core.Models;

public class PurchaseEvent
{
    public Guid Id { get; set; }

    public Guid OrderId { get; set; }

    public Guid UserId { get; set; }

    public Guid GameId { get; set; }

    public string GameTitle { get; set; } = string.Empty;

    public string EventType { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public string? Reason { get; set; }

    public DateTime OccurredAt { get; set; }
}
