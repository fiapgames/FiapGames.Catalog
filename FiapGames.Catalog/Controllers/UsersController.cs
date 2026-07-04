using FiapGames.Catalog.Data;
using FiapGames.Catalog.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FiapGames.Catalog.Controllers;

[ApiController]
[Route("users/{userId:guid}")]
public class UsersController(CatalogDbContext dbContext) : ControllerBase
{
    [HttpGet("library")]
    public async Task<ActionResult<List<LibraryItemDto>>> GetLibrary(Guid userId, CancellationToken cancellationToken)
    {
        var library = await dbContext.UserGameLibraries
            .Where(l => l.UserId == userId)
            .OrderByDescending(l => l.PurchasedAt)
            .Select(l => new LibraryItemDto(l.GameId, l.Game!.Title, l.Game.Genre, l.OrderId, l.PurchasedAt))
            .ToListAsync(cancellationToken);

        return Ok(library);
    }
}
