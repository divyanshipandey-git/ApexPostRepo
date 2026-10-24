using System.Diagnostics;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ApexPost.Web.Filters;

public sealed class PerformanceActionFilter(ILogger<PerformanceActionFilter> logger) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var timer = Stopwatch.StartNew();
        try
        {
            await next();
        }
        finally
        {
            timer.Stop();
            logger.LogDebug(
                "MVC action {Controller}.{Action} executed in {ElapsedMilliseconds} ms",
                context.RouteData.Values["controller"],
                context.RouteData.Values["action"],
                timer.ElapsedMilliseconds);
        }
    }
}
