using FiapGames.Catalog.Data;
using FiapGames.Catalog.Dtos;
using FiapGames.Catalog.Models;
using FiapGames.Contracts.IntegrationEvents;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace FiapGames.Catalog.Services;

public class PurchaseService(CatalogDbContext dbContext, IPublishEndpoint publishEndpoint) : IPurchaseService
{
    public async Task<PurchaseResult> RequestPurchaseAsync(Guid gameId, Guid userId, CancellationToken cancellationToken = default)
    {
        var game = await dbContext.Games.FindAsync([gameId], cancellationToken);
        if (game is null)
        {
            return new PurchaseResult(PurchaseResultStatus.GameNotFound);
        }

        if (!game.Active)
        {
            return new PurchaseResult(PurchaseResultStatus.GameInactive);
        }

        var alreadyOwned = await dbContext.UserGameLibraries
            .AnyAsync(l => l.UserId == userId && l.GameId == gameId, cancellationToken);
        if (alreadyOwned)
        {
            return new PurchaseResult(PurchaseResultStatus.AlreadyOwned);
        }

        var now = DateTime.UtcNow;
        var order = new Order
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            GameId = gameId,
            Price = game.Price,
            Status = OrderStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync(cancellationToken);

        await publishEndpoint.Publish(
            new OrderPlacedEvent(order.Id, order.UserId, order.GameId, order.Price, order.CreatedAt),
            cancellationToken);

        return new PurchaseResult(
            PurchaseResultStatus.Success,
            new OrderDto(order.Id, order.UserId, order.GameId, order.Price, order.Status, order.CreatedAt, order.UpdatedAt));
    }
}
