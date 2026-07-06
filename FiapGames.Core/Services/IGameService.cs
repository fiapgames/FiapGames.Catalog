using FiapGames.Core.Dtos;

namespace FiapGames.Core.Services;

public interface IGameService
{
    Task<List<GameDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<GameDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<GameDto> CreateAsync(CreateGameDto dto, CancellationToken cancellationToken = default);
    Task<GameDto?> UpdateAsync(Guid id, UpdateGameDto dto, CancellationToken cancellationToken = default);
    Task<bool> DeactivateAsync(Guid id, CancellationToken cancellationToken = default);
}
