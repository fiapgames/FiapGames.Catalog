using FiapGames.Catalog.Validators;
using FiapGames.Core.Dtos;

namespace FiapGames.Catalog.Tests.Validators;

public class PurchaseRequestDtoValidatorTests
{
    private readonly PurchaseRequestDtoValidator _validator = new();

    [Fact]
    public void UserId_Filled_IsValid()
    {
        Assert.True(_validator.Validate(new PurchaseRequestDto(Guid.NewGuid())).IsValid);
    }

    [Fact]
    public void UserId_Empty_IsInvalid()
    {
        Assert.False(_validator.Validate(new PurchaseRequestDto(Guid.Empty)).IsValid);
    }
}
