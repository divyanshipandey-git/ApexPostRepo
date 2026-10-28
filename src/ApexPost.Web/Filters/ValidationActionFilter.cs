using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ApexPost.Web.Filters;

public sealed class ValidationActionFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!context.ModelState.IsValid)
        {
            // MVC form controllers rebuild their view models and redisplay the submitted form themselves.
            // API controllers and other action types use the filter's standard 400 validation result.
            if (context.Controller is Controller)
            {
                await next();
                return;
            }

            context.Result = new BadRequestObjectResult(context.ModelState);
            return;
        }

        await next();
    }
}
