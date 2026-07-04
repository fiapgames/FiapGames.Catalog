using FiapGames.Catalog.Data;
using FiapGames.Catalog.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FiapGames.Catalog.Controllers;

[ApiController]
[Route("orders")]
public class OrdersController(CatalogDbContext dbContext) : ControllerBase
{
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OrderDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var order = await dbContext.Orders
            .Where(o => o.Id == id)
            .Select(o => new OrderDto(o.Id, o.UserId, o.GameId, o.Price, o.Status, o.CreatedAt, o.UpdatedAt))
            .FirstOrDefaultAsync(cancellationToken);

        return order is null ? NotFound() : Ok(order);
    }
}
