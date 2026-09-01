using FiapGames.Data;
using FiapGames.Core.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FiapGames.Catalog.Controllers;

[ApiController]
[Route("orders")]
[Authorize]
public class OrdersController(CatalogDbContext dbContext) : ControllerBase
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
}
