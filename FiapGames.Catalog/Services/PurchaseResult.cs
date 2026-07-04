using FiapGames.Catalog.Dtos;

namespace FiapGames.Catalog.Services;

public enum PurchaseResultStatus
{
    Success,
    GameNotFound,
    GameInactive,
    AlreadyOwned
}

public record PurchaseResult(PurchaseResultStatus Status, OrderDto? Order = null);
