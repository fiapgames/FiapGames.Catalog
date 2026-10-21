using FiapGames.Catalog.Validators;
using FiapGames.Core.Dtos;

namespace FiapGames.Catalog.Tests.Validators;

public class GameDtoValidatorsTests
{
    private readonly CreateGameDtoValidator _createValidator = new();
    private readonly UpdateGameDtoValidator _updateValidator = new();

    [Fact]
    public void Create_ValidGame_IsValid()
    {
        var dto = new CreateGameDto("Dark Souls III", "Souls-like", 39.90m, "RPG");

        Assert.True(_createValidator.Validate(dto).IsValid);
    }

    [Theory]
    [InlineData("", "RPG", 10)]
    [InlineData("Dark Souls III", "", 10)]
    [InlineData("Dark Souls III", "RPG", -0.01)]
    public void Create_InvalidGame_IsInvalid(string title, string genre, decimal price)
    {
        var dto = new CreateGameDto(title, "Souls-like", price, genre);

        Assert.False(_createValidator.Validate(dto).IsValid);
    }

    [Fact]
    public void Create_TitleLongerThan200_IsInvalid()
    {
        var dto = new CreateGameDto(new string('a', 201), "Souls-like", 10m, "RPG");

        Assert.Contains(_createValidator.Validate(dto).Errors, e => e.PropertyName == nameof(CreateGameDto.Title));
    }

    [Fact]
    public void Update_FreeGame_IsValid()
    {
        var dto = new UpdateGameDto("Dota 2", "MOBA", 0m, "Strategy", true);

        Assert.True(_updateValidator.Validate(dto).IsValid);
    }
}
