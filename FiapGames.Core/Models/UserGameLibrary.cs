namespace FiapGames.Core.Models;

public class UserGameLibrary
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid GameId { get; set; }
    public Guid OrderId { get; set; }
    public DateTime PurchasedAt { get; set; }

    public Game? Game { get; set; }
    public Order? Order { get; set; }
}
