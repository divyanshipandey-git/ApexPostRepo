using ApexPost.Web.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using System.Text.Json;
using Xunit;

namespace ApexPost.Web.IntegrationTests;

public sealed class ValidationActionFilterTests
{
    [Fact]
    public async Task InvalidModelState_ReturnsBadRequestWithExistingValidationMessage_WithoutExecutingAction()
    {
        var context = CreateContext();
        context.ModelState.AddModelError("Reason", "Enter a reason for voiding this transaction.");
        var actionExecuted = false;
        var filter = new ValidationActionFilter();

        await filter.OnActionExecutionAsync(context, () =>
        {
            actionExecuted = true;
            return Task.FromResult(new ActionExecutedContext(context, [], new object()));
        });

        var result = Assert.IsType<BadRequestObjectResult>(context.Result);
        Assert.Contains("Enter a reason for voiding this transaction.", JsonSerializer.Serialize(result.Value));
        Assert.False(actionExecuted);
        Assert.Contains("Enter a reason for voiding this transaction.",
            context.ModelState["Reason"]!.Errors[0].ErrorMessage);
    }

    [Fact]
    public async Task ValidModelState_ExecutesAction()
    {
        var context = CreateContext();
        var actionExecuted = false;
        var filter = new ValidationActionFilter();

        await filter.OnActionExecutionAsync(context, () =>
        {
            actionExecuted = true;
            return Task.FromResult(new ActionExecutedContext(context, [], new object()));
        });

        Assert.True(actionExecuted);
        Assert.Null(context.Result);
    }

    [Fact]
    public async Task InvalidModelState_OnMarkedFormAction_AllowsViewRedisplay()
    {
        var context = CreateContext(redisplay: true);
        context.ModelState.AddModelError("Sku", "Choose a product.");
        var actionExecuted = false;

        await new ValidationActionFilter().OnActionExecutionAsync(context, () =>
        {
            actionExecuted = true;
            return Task.FromResult(new ActionExecutedContext(context, [], new object()));
        });

        Assert.True(actionExecuted);
        Assert.Null(context.Result);
        Assert.Contains("Choose a product.", context.ModelState["Sku"]!.Errors[0].ErrorMessage);
    }

    private static ActionExecutingContext CreateContext(bool redisplay = false)
    {
        var descriptor = new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor
        {
            EndpointMetadata = redisplay
                ? [new RedisplayInvalidModelStateAttribute()]
                : []
        };

        var actionContext = new ActionContext(
            new DefaultHttpContext(),
            new RouteData(),
            descriptor);
        return new ActionExecutingContext(actionContext, [], new Dictionary<string, object?>(), new object());
    }
}
