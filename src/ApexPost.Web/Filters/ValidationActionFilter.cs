using Microsoft.AspNetCore.Mvc.Filters;

namespace ApexPost.Web.Filters;

public sealed class ValidationActionFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!context.ModelState.IsValid && context.HttpContext.Response.StatusCode == StatusCodes.Status200OK)
        {
            context.HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        }

        await next();
    }
}
