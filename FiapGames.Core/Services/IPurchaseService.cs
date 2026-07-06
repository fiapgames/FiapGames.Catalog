namespace FiapGames.Core.Services;

public interface IPurchaseService
{
    Task<PurchaseResult> RequestPurchaseAsync(Guid gameId, Guid userId, CancellationToken cancellationToken = default);
}
