using FiapGames.Catalog.Data;
using FiapGames.Catalog.Dtos;
using FiapGames.Contracts.Requests.User;
using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FiapGames.Catalog.Controllers;

[ApiController]
[Route("library/{userId:guid}")]
public class LibraryController(CatalogDbContext dbContext, IRequestClient<UserLookupRequested> userRequestClient)
    : ControllerBase
{
    [HttpGet("")]
    public async Task<ActionResult<LibraryResponseDto>> GetLibrary(Guid userId, CancellationToken cancellationToken)
    {
        UserLookupResponded user;

        try
        {
            var response = await userRequestClient.GetResponse<UserLookupResponded>(
                new UserLookupRequested(Guid.NewGuid(), userId, DateTimeOffset.UtcNow),
                cancellationToken,
                RequestTimeout.After(s: 5));
            user = response.Message;
        }
        catch (RequestTimeoutException)
        {
            return Problem(
                title: "User service unavailable",
                detail: "Timed out waiting for a response from the users service.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        if (!user.Found)
        {
            return NotFound();
        }

        var games = await dbContext.UserGameLibraries
            .Where(l => l.UserId == userId)
            .OrderByDescending(l => l.PurchasedAt)
            .Select(l => new LibraryItemDto(l.GameId, l.Game!.Title, l.Game.Genre, l.OrderId, l.PurchasedAt))
            .ToListAsync(cancellationToken);

        return Ok(new LibraryResponseDto(user.UserId, user.Name ?? string.Empty, user.Email ?? string.Empty, games));
    }
}
