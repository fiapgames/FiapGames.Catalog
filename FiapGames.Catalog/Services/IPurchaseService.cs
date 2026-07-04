namespace FiapGames.Catalog.Services;

public interface IPurchaseService
{
    Task<PurchaseResult> RequestPurchaseAsync(Guid gameId, Guid userId, CancellationToken cancellationToken = default);
}
