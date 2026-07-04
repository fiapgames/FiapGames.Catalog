using FiapGames.Catalog.Models;

namespace FiapGames.Catalog.Dtos;

public record PurchaseRequestDto(Guid UserId);

public record OrderDto(Guid Id, Guid UserId, Guid GameId, decimal Price, OrderStatus Status, DateTime CreatedAt, DateTime UpdatedAt);
