using FiapGames.Catalog.Validators;
using FiapGames.Core.Dtos;

namespace FiapGames.Catalog.Tests.Search;

public sealed class GameSearchRequestValidatorTests
{
    private readonly GameSearchRequestValidator validator = new();

    [Theory]
    [InlineData("", 1, 20)]
    [InlineData("   ", 1, 20)]
    [InlineData("minecraft", 0, 20)]
    [InlineData("minecraft", 1, 0)]
    [InlineData("minecraft", 1, 101)]
    [InlineData("minecraft", 501, 20)]
    [InlineData("minecraft", int.MaxValue, int.MaxValue)]
    public void Rejects_invalid_or_unbounded_search(string q, int page, int pageSize) =>
        Assert.False(validator.Validate(new GameSearchRequest { Q = q, Page = page, PageSize = pageSize }).IsValid);

    [Fact]
    public void Rejects_overlong_query() =>
        Assert.False(validator.Validate(new GameSearchRequest { Q = new string('a', 201) }).IsValid);

    [Fact]
    public void Accepts_last_valid_page() =>
        Assert.True(validator.Validate(new GameSearchRequest { Q = "ação", Page = 100, PageSize = 100 }).IsValid);
}
