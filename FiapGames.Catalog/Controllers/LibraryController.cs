using FiapGames.Data;
using FiapGames.Data.Caching;
using FiapGames.Core.Dtos;
using FiapGames.Contracts.Requests.User;
using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FiapGames.Catalog.Controllers;

[ApiController]
[Route("library/{userId:guid}")]
[Authorize]
public class LibraryController(
    CatalogDbContext dbContext,
    IRequestClient<UserLookupRequested> userRequestClient,
    ICacheStore cacheStore)
    : ControllerBase
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(2);

    [HttpGet("")]
    public async Task<ActionResult<LibraryResponseDto>> GetLibrary(Guid userId, CancellationToken cancellationToken)
    {
        LibraryResponseDto? library;

        try
        {
            // O cache guarda a resposta inteira: um hit evita o RPC síncrono no
            // RabbitMQ (timeout de 5s) além da consulta ao SQL Server.
            library = await cacheStore.GetOrSetAsync(
                $"catalog:library:{userId}",
                ct => LoadLibraryAsync(userId, ct),
                CacheTtl,
                cancellationToken);
        }
        catch (RequestTimeoutException)
        {
            return Problem(
                title: "Serviço de usuários indisponível",
                detail: "Tempo esgotado aguardando resposta do serviço de usuários.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        return library is null ? NotFound() : Ok(library);
    }

    private async Task<LibraryResponseDto?> LoadLibraryAsync(Guid userId, CancellationToken cancellationToken)
    {
        var response = await userRequestClient.GetResponse<UserLookupResponded>(
            new UserLookupRequested(Guid.NewGuid(), userId, DateTimeOffset.UtcNow),
            cancellationToken,
            RequestTimeout.After(s: 5));
        var user = response.Message;

        if (!user.Found)
        {
            return null;
        }

        var games = await dbContext.UserGameLibraries
            .Where(l => l.UserId == userId)
            .OrderByDescending(l => l.PurchasedAt)
            .Select(l => new LibraryItemDto(l.GameId, l.Game!.Title, l.Game.Genre, l.OrderId, l.PurchasedAt))
            .ToListAsync(cancellationToken);

        return new LibraryResponseDto(user.UserId, user.Name ?? string.Empty, user.Email ?? string.Empty, games);
    }
}
