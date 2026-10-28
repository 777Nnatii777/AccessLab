using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AccessLab.Api.Authentication;

public sealed class CsrfValidationFilter : IAsyncActionFilter
{
    private readonly IAntiforgery _antiforgery;
    public CsrfValidationFilter(IAntiforgery antiforgery) => _antiforgery = antiforgery;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (HttpMethods.IsGet(context.HttpContext.Request.Method) ||
            HttpMethods.IsHead(context.HttpContext.Request.Method) ||
            HttpMethods.IsOptions(context.HttpContext.Request.Method))
        {
            await next();
            return;
        }
        try { await _antiforgery.ValidateRequestAsync(context.HttpContext); }
        catch (AntiforgeryValidationException)
        {
            context.Result = new BadRequestObjectResult(new { message = "Nieprawidłowy token CSRF." });
            return;
        }
        await next();
    }
}
