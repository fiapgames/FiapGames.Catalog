using FiapGames.Core.Dtos;
using FluentValidation;

namespace FiapGames.Catalog.Validators;

public class CreateGameDtoValidator : AbstractValidator<CreateGameDto>
{
    public CreateGameDtoValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).NotNull().MaximumLength(2000);
        RuleFor(x => x.Genre).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0);
    }
}

public class UpdateGameDtoValidator : AbstractValidator<UpdateGameDto>
{
    public UpdateGameDtoValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).NotNull().MaximumLength(2000);
        RuleFor(x => x.Genre).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0);
    }
}
