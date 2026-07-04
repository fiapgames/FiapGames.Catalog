using FiapGames.Catalog.Dtos;
using FluentValidation;

namespace FiapGames.Catalog.Validators;

public class PurchaseRequestDtoValidator : AbstractValidator<PurchaseRequestDto>
{
    public PurchaseRequestDtoValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
    }
}
