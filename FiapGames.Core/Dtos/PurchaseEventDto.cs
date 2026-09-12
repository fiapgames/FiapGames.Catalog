namespace FiapGames.Core.Dtos;

public record PurchaseEventDto(
    Guid Id,
    Guid OrderId,
    Guid UserId,
    Guid GameId,
    string GameTitle,
    string EventType,
    decimal Price,
    string? Reason,
    DateTime OccurredAt);
