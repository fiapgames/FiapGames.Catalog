using FiapGames.Core.Models;

namespace FiapGames.Core.Services;

public interface IPurchaseHistoryStore
{
    Task AppendAsync(PurchaseEvent purchaseEvent, CancellationToken cancellationToken = default);

    Task<List<PurchaseEvent>> GetByOrderAsync(Guid orderId, CancellationToken cancellationToken = default);
}
