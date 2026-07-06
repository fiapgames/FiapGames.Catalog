using FiapGames.Core.Models;

namespace FiapGames.Core.Dtos;

public record PurchaseRequestDto(Guid UserId);

public record OrderDto(Guid Id, Guid UserId, Guid GameId, decimal Price, OrderStatus Status, string? RejectionReason, DateTime CreatedAt, DateTime UpdatedAt);
