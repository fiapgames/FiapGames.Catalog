namespace FiapGames.Core.Dtos;

public record LibraryItemDto(Guid GameId, string Title, string Genre, Guid OrderId, DateTime PurchasedAt);

public record LibraryResponseDto(Guid UserId, string UserName, string UserEmail, List<LibraryItemDto> Games);
