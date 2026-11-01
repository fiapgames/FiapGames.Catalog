using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace FiapGames.Catalog.Validators;

public sealed class ConcurrencyExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not DbUpdateConcurrencyException) return;
        context.Result = new ObjectResult(new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "O jogo foi alterado por outra requisição. Atualize os dados e tente novamente."
        }) { StatusCode = StatusCodes.Status409Conflict };
        context.ExceptionHandled = true;
    }
}
