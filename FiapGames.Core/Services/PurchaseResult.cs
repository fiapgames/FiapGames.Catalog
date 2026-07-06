using FiapGames.Core.Dtos;

namespace FiapGames.Core.Services;

public enum PurchaseResultStatus
{
    Success,
    GameNotFound,
    GameInactive,
    AlreadyOwned
}

public record PurchaseResult(PurchaseResultStatus Status, OrderDto? Order = null);
