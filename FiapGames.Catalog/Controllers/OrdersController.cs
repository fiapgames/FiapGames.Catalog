using FiapGames.Data;
using FiapGames.Core.Dtos;
using FiapGames.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FiapGames.Catalog.Controllers;

[ApiController]
[Route("orders")]
[Authorize]
public class OrdersController(CatalogDbContext dbContext, IPurchaseHistoryStore purchaseHistoryStore) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<OrderDto>>> GetAll(CancellationToken cancellationToken)
    {
        var orders = await dbContext.Orders
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => new OrderDto(o.Id, o.UserId, o.GameId, o.Price, o.Status, o.RejectionReason, o.CreatedAt, o.UpdatedAt))
            .ToListAsync(cancellationToken);

        return Ok(orders);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OrderDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var order = await dbContext.Orders
            .Where(o => o.Id == id)
            .Select(o => new OrderDto(o.Id, o.UserId, o.GameId, o.Price, o.Status, o.RejectionReason, o.CreatedAt, o.UpdatedAt))
            .FirstOrDefaultAsync(cancellationToken);

        return order is null ? NotFound() : Ok(order);
    }

    [HttpGet("{id:guid}/history")]
    public async Task<ActionResult<List<PurchaseEventDto>>> GetHistory(Guid id, CancellationToken cancellationToken)
    {
        var orderExists = await dbContext.Orders.AnyAsync(o => o.Id == id, cancellationToken);
        if (!orderExists)
        {
            return NotFound();
        }

        var events = await purchaseHistoryStore.GetByOrderAsync(id, cancellationToken);

        var history = events
            .Select(e => new PurchaseEventDto(e.Id, e.OrderId, e.UserId, e.GameId, e.GameTitle, e.EventType, e.Price, e.Reason, e.OccurredAt))
            .ToList();

        return Ok(history);
    }
}
