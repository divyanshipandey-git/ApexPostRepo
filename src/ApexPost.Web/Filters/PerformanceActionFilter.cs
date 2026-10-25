using System.Diagnostics;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ApexPost.Web.Filters;

public sealed class PerformanceActionFilter(
    ILogger<PerformanceActionFilter> logger,
    IOptions<PerformanceFilterOptions> options) : IAsyncActionFilter
{
    private readonly int _warningThresholdMilliseconds = options.Value.WarningThresholdMilliseconds;

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
            if (timer.ElapsedMilliseconds >= _warningThresholdMilliseconds)
            {
                logger.LogWarning(
                    "Slow MVC action {Controller}.{Action} executed in {ElapsedMilliseconds} ms; threshold is {ThresholdMilliseconds} ms",
                    context.RouteData.Values["controller"],
                    context.RouteData.Values["action"],
                    timer.ElapsedMilliseconds,
                    _warningThresholdMilliseconds);
            }
            else
            {
                logger.LogDebug(
                    "MVC action {Controller}.{Action} executed in {ElapsedMilliseconds} ms",
                    context.RouteData.Values["controller"],
                    context.RouteData.Values["action"],
                    timer.ElapsedMilliseconds);
            }
        }
    }
}
