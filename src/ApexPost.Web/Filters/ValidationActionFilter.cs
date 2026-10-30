using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ApexPost.Web.Filters;

[AttributeUsage(AttributeTargets.Method)]
public sealed class RedisplayInvalidModelStateAttribute : Attribute { }

public sealed class ValidationActionFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!context.ModelState.IsValid)
        {
            // Only form actions explicitly marked for redisplay may rebuild the page and its options.
            if (context.ActionDescriptor.EndpointMetadata.OfType<RedisplayInvalidModelStateAttribute>().Any())
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
