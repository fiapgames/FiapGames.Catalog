namespace FiapGames.Catalog.Dtos;

public record LibraryItemDto(Guid GameId, string Title, string Genre, Guid OrderId, DateTime PurchasedAt);
