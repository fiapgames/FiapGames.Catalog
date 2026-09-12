using FiapGames.Data;
using FiapGames.Core.Services;
using FiapGames.Core.Dtos;
using FiapGames.Core.Models;
using FiapGames.Contracts.IntegrationEvents;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FiapGames.Services;

public class PurchaseService(
    CatalogDbContext dbContext,
    IPublishEndpoint publishEndpoint,
    IPurchaseHistoryStore purchaseHistoryStore,
    ILogger<PurchaseService> logger) : IPurchaseService
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

        try
        {
            await purchaseHistoryStore.AppendAsync(
                new PurchaseEvent
                {
                    Id = Guid.NewGuid(),
                    OrderId = order.Id,
                    UserId = order.UserId,
                    GameId = order.GameId,
                    GameTitle = game.Title,
                    EventType = PurchaseEventTypes.OrderPlaced,
                    Price = order.Price,
                    OccurredAt = order.CreatedAt
                },
                cancellationToken);
        }
        catch (Exception exception)
        {
            // Histórico é acessório: uma falha no Mongo não pode derrubar a compra.
            logger.LogWarning(exception, "Falha ao registrar histórico de compra para o pedido {OrderId}", order.Id);
        }

        return new PurchaseResult(
            PurchaseResultStatus.Success,
            new OrderDto(order.Id, order.UserId, order.GameId, order.Price, order.Status, order.RejectionReason, order.CreatedAt, order.UpdatedAt));
    }
}
