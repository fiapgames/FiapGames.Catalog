using FiapGames.Catalog.Dtos;
using FiapGames.Catalog.Services;
using Microsoft.AspNetCore.Mvc;

namespace FiapGames.Catalog.Controllers;

[ApiController]
[Route("games")]
public class GamesController(IGameService gameService, IPurchaseService purchaseService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<GameDto>>> GetAll(CancellationToken cancellationToken)
    {
        return Ok(await gameService.GetAllAsync(cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<GameDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var game = await gameService.GetByIdAsync(id, cancellationToken);
        return game is null ? NotFound() : Ok(game);
    }

    [HttpPost]
    public async Task<ActionResult<GameDto>> Create(CreateGameDto dto, CancellationToken cancellationToken)
    {
        var game = await gameService.CreateAsync(dto, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = game.Id }, game);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<GameDto>> Update(Guid id, UpdateGameDto dto, CancellationToken cancellationToken)
    {
        var game = await gameService.UpdateAsync(id, dto, cancellationToken);
        return game is null ? NotFound() : Ok(game);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        var deactivated = await gameService.DeactivateAsync(id, cancellationToken);
        return deactivated ? NoContent() : NotFound();
    }

    [HttpPost("{id:guid}/purchase")]
    public async Task<ActionResult<OrderDto>> Purchase(Guid id, PurchaseRequestDto dto, CancellationToken cancellationToken)
    {
        var result = await purchaseService.RequestPurchaseAsync(id, dto.UserId, cancellationToken);

        return result.Status switch
        {
            PurchaseResultStatus.Success => Accepted(result.Order),
            PurchaseResultStatus.GameNotFound => NotFound(),
            PurchaseResultStatus.GameInactive => Conflict("Jogo não está disponível para compra."),
            PurchaseResultStatus.AlreadyOwned => Conflict("Usuário já possui este jogo."),
            _ => Problem()
        };
    }
}
