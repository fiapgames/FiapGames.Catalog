namespace FiapGames.Core.Dtos;

public record GameDto(Guid Id, string Title, string Description, decimal Price, string Genre, bool Active, DateTime CreatedAt);

public record CreateGameDto(string Title, string Description, decimal Price, string Genre);

public record UpdateGameDto(string Title, string Description, decimal Price, string Genre, bool Active);
