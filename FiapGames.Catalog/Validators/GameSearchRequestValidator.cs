using FiapGames.Core.Dtos;
using FluentValidation;

namespace FiapGames.Catalog.Validators;

public sealed class GameSearchRequestValidator : AbstractValidator<GameSearchRequest>
{
    public GameSearchRequestValidator()
    {
        RuleFor(r => r.Q).NotEmpty().MaximumLength(200);
        RuleFor(r => r.Page).GreaterThanOrEqualTo(1);
        RuleFor(r => r.PageSize).InclusiveBetween(1, 100);
        RuleFor(r => r).Must(r => r.Page >= 1 && r.PageSize >= 1 && (long)r.Page * r.PageSize <= 10000)
            .WithMessage("A paginação deve ficar nos primeiros 10000 resultados.");
    }
}
