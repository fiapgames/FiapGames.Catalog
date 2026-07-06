using FiapGames.Data;
using FiapGames.Core.Models;
using FiapGames.Contracts.IntegrationEvents;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FiapGames.Services.Consumers;

public class PaymentProcessedConsumer(CatalogDbContext dbContext, ILogger<PaymentProcessedConsumer> logger)
    : IConsumer<PaymentProcessedEvent>
{
    public async Task Consume(ConsumeContext<PaymentProcessedEvent> context)
    {
        var paymentProcessed = context.Message;

        var order = await dbContext.Orders
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
            }
        }

        await dbContext.SaveChangesAsync(context.CancellationToken);

        logger.LogInformation("Order {OrderId} updated to status {Status}", order.Id, order.Status);
    }
}
