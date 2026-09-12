using FiapGames.Data;
using FiapGames.Data.Caching;
using FiapGames.Core.Services;
using FiapGames.Core.Models;
using FiapGames.Contracts.IntegrationEvents;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FiapGames.Services.Consumers;

public class PaymentProcessedConsumer(
    CatalogDbContext dbContext,
    IPurchaseHistoryStore purchaseHistoryStore,
    ICacheStore cacheStore,
    ILogger<PaymentProcessedConsumer> logger)
    : IConsumer<PaymentProcessedEvent>
{
    public async Task Consume(ConsumeContext<PaymentProcessedEvent> context)
    {
        var paymentProcessed = context.Message;

        var order = await dbContext.Orders
            .Include(o => o.Game)
            .FirstOrDefaultAsync(o => o.Id == paymentProcessed.OrderId, context.CancellationToken);

        if (order is null)
        {
            logger.LogWarning("Received PaymentProcessedEvent for unknown order {OrderId}", paymentProcessed.OrderId);
            return;
        }

        var approved = paymentProcessed.Status == PaymentStatus.Approved;
        order.Status = approved ? OrderStatus.Approved : OrderStatus.Rejected;
        order.RejectionReason = approved ? null : "Pagamento rejeitado pelo serviço de pagamentos.";
        order.UpdatedAt = DateTime.UtcNow;

        var gameGranted = false;

        if (approved)
        {
            var alreadyInLibrary = await dbContext.UserGameLibraries
                .AnyAsync(l => l.UserId == order.UserId && l.GameId == order.GameId, context.CancellationToken);

            if (!alreadyInLibrary)
            {
                dbContext.UserGameLibraries.Add(new UserGameLibrary
                {
                    Id = Guid.NewGuid(),
                    UserId = order.UserId,
                    GameId = order.GameId,
                    OrderId = order.Id,
                    PurchasedAt = paymentProcessed.ProcessedAt
                });
                gameGranted = true;
            }
        }

        await dbContext.SaveChangesAsync(context.CancellationToken);

        logger.LogInformation("Order {OrderId} updated to status {Status}", order.Id, order.Status);

        await AppendHistoryAsync(order, paymentProcessed, gameGranted, context.CancellationToken);

        if (gameGranted)
        {
            await cacheStore.RemoveAsync($"catalog:library:{order.UserId}", context.CancellationToken);
        }
    }

    private async Task AppendHistoryAsync(
        Order order,
        PaymentProcessedEvent paymentProcessed,
        bool gameGranted,
        CancellationToken cancellationToken)
    {
        var gameTitle = order.Game?.Title ?? string.Empty;
        var approved = paymentProcessed.Status == PaymentStatus.Approved;

        try
        {
            await purchaseHistoryStore.AppendAsync(
                new PurchaseEvent
                {
                    Id = Guid.NewGuid(),
                    OrderId = order.Id,
                    UserId = order.UserId,
                    GameId = order.GameId,
                    GameTitle = gameTitle,
                    EventType = approved ? PurchaseEventTypes.PaymentApproved : PurchaseEventTypes.PaymentRejected,
                    Price = order.Price,
                    Reason = order.RejectionReason,
                    OccurredAt = paymentProcessed.ProcessedAt
                },
                cancellationToken);

            if (gameGranted)
            {
                await purchaseHistoryStore.AppendAsync(
                    new PurchaseEvent
                    {
                        Id = Guid.NewGuid(),
                        OrderId = order.Id,
                        UserId = order.UserId,
                        GameId = order.GameId,
                        GameTitle = gameTitle,
                        EventType = PurchaseEventTypes.GameGranted,
                        Price = order.Price,
                        OccurredAt = order.UpdatedAt
                    },
                    cancellationToken);
            }
        }
        catch (Exception exception)
        {
            // Histórico é acessório: uma falha no Mongo não pode impedir a confirmação do pagamento.
            logger.LogWarning(exception, "Falha ao registrar histórico de compra para o pedido {OrderId}", order.Id);
        }
    }
}
