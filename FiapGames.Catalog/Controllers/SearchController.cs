using FiapGames.Catalog.Search;
using FiapGames.Core.Dtos;
using FiapGames.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FiapGames.Catalog.Controllers;

[ApiController]
[Route("search")]
[Authorize]
public sealed class SearchController(IGameSearchIndex index, SearchIndexState state,
    ILogger<SearchController> logger) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<GameSearchResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<GameSearchResult>> Search([FromQuery] GameSearchRequest request, CancellationToken cancellationToken)
    {
        if (!state.IsReady) return Unavailable();
        try { return Ok(await index.SearchAsync(request, cancellationToken)); }
        catch (SearchUnavailableException ex)
        {
            state.SetReady(false);
            logger.LogWarning(ex, "Search is temporarily unavailable");
            return Unavailable();
        }
    }

    private ObjectResult Unavailable()
    {
        Response.Headers.RetryAfter = "5";
        return Problem(statusCode: StatusCodes.Status503ServiceUnavailable,
            title: "Busca temporariamente indisponível", detail: "O índice está sendo sincronizado. Tente novamente em instantes.");
    }
}
