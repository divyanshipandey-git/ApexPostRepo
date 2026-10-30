using System.Diagnostics;
using System.Security.Claims;
using ApexPost.Application.Abstractions.Persistence;
using ApexPost.Application.Services.Auditing;
using ApexPost.Web.Middleware;
using ApexPost.Web.Models.Shipping;
using ApexPost.Web.Models.Stock;
using ApexPost.Web.Models.Transactions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace ApexPost.Web.Filters;

public sealed class AuditActionFilter(
    IAuditStore auditStore,
    ILogger<AuditActionFilter> logger) : IAsyncActionFilter
{
    private static readonly HashSet<(string Controller, string Action)> AuditedActions =
    [
        ("Transactions", "Create"),
        ("Transactions", "Void"),
        ("Transactions", "Refund"),
        ("Stock", "Adjust"),
        ("MoneyOrders", "Create"),
        ("CourierIntegration", "UpdateShipmentStatus")
    ];

    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        if (!HttpMethods.IsPost(context.HttpContext.Request.Method) || !IsBusinessAction(context))
        {
            await next();
            return;
        }

        var timer = Stopwatch.StartNew();
        ActionExecutedContext? executed;
        try
        {
            executed = await next();
        }
        catch
        {
            timer.Stop();
            await TryWriteRecordAsync(context, StatusCodes.Status500InternalServerError, timer.ElapsedMilliseconds);
            throw;
        }

        timer.Stop();
        await TryWriteRecordAsync(
            context,
            GetStatusCode(context, executed),
            timer.ElapsedMilliseconds,
            executed);
    }

    private async Task TryWriteRecordAsync(
        ActionExecutingContext context,
        int statusCode,
        long durationMilliseconds,
        ActionExecutedContext? executed = null)
    {
        try
        {
            await auditStore.WriteAsync(CreateRecord(context, statusCode, durationMilliseconds, executed));
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Audit record write failed for {Controller}.{Action}; correlation ID {CorrelationId}. The business result has already been determined.",
                context.RouteData.Values["controller"],
                context.RouteData.Values["action"],
                CorrelationIdMiddleware.GetCorrelationId(context.HttpContext));
        }
    }

    private static AuditActionRecord CreateRecord(
        ActionExecutingContext context,
        int statusCode,
        long durationMilliseconds,
        ActionExecutedContext? executed)
    {
        var user = context.HttpContext.User;
        var routeValues = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        if (IsTransactionReversal(context) &&
            context.ActionArguments.Values.OfType<TransactionVoidViewModel>().FirstOrDefault() is { } voidModel)
        {
            AddSafeValue(routeValues, "transactionId", voidModel.TransactionId);
        }
        else if (IsTransactionReversal(context) &&
                 context.ActionArguments.Values.OfType<TransactionRefundViewModel>().FirstOrDefault() is { } refundModel)
        {
            AddSafeValue(routeValues, "transactionId", refundModel.TransactionId);
        }

        if (IsStockAdjustment(context) &&
            context.ActionArguments.Values.OfType<StockManagementViewModel>().FirstOrDefault() is { } stockModel)
        {
            AddSafeValue(routeValues, "branchCode", stockModel.BranchCode);
            AddSafeValue(routeValues, "sku", stockModel.Sku);
        }

        if (IsCourierStatusUpdate(context))
        {
            AddSafeValue(routeValues, "shipmentId", context.RouteData.Values["shipmentId"]?.ToString());
        }

        if (IsSaleOrMoneyOrder(context) &&
            executed?.Result is RedirectToActionResult { RouteValues: not null } redirect &&
            redirect.RouteValues.TryGetValue("id", out var transactionId))
        {
            AddSafeValue(routeValues, "transactionId", transactionId?.ToString());
        }

        return new AuditActionRecord(
            DateTimeOffset.UtcNow,
            user.FindFirstValue("StaffId"),
            user.FindFirstValue("Role") ?? user.FindFirstValue(ClaimTypes.Role),
            user.FindFirstValue("BranchCode"),
            context.RouteData.Values["controller"]?.ToString() ?? "Unknown",
            context.RouteData.Values["action"]?.ToString() ?? "Unknown",
            routeValues,
            statusCode,
            durationMilliseconds,
            CorrelationIdMiddleware.GetCorrelationId(context.HttpContext));
    }

    private static bool IsBusinessAction(ActionExecutingContext context) =>
        context.RouteData.Values["controller"]?.ToString() is { } controller &&
        context.RouteData.Values["action"]?.ToString() is { } action &&
        AuditedActions.Contains((controller, action));

    private static bool IsTransactionReversal(ActionExecutingContext context) =>
        context.RouteData.Values["controller"]?.ToString() is { } controller &&
        context.RouteData.Values["action"]?.ToString() is { } action &&
        controller.Equals("Transactions", StringComparison.OrdinalIgnoreCase) &&
        (action.Equals("Void", StringComparison.OrdinalIgnoreCase) ||
         action.Equals("Refund", StringComparison.OrdinalIgnoreCase));

    private static bool IsStockAdjustment(ActionExecutingContext context) =>
        IsAction(context, "Stock", "Adjust");

    private static bool IsCourierStatusUpdate(ActionExecutingContext context) =>
        IsAction(context, "CourierIntegration", "UpdateShipmentStatus");

    private static bool IsSaleOrMoneyOrder(ActionExecutingContext context) =>
        IsAction(context, "Transactions", "Create") || IsAction(context, "MoneyOrders", "Create");

    private static bool IsAction(ActionExecutingContext context, string controller, string action) =>
        string.Equals(context.RouteData.Values["controller"]?.ToString(), controller, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(context.RouteData.Values["action"]?.ToString(), action, StringComparison.OrdinalIgnoreCase);

    private static void AddSafeValue(IDictionary<string, string?> values, string key, string? value)
    {
        if (value is { Length: > 0 and <= 256 } && !value.Any(char.IsControl))
        {
            values[key] = value;
        }
    }

    private static int GetStatusCode(ActionExecutingContext context, ActionExecutedContext executed)
    {
        if (executed.Exception is not null && !executed.ExceptionHandled)
        {
            return StatusCodes.Status500InternalServerError;
        }

        if (executed.Result is IStatusCodeActionResult statusResult && statusResult.StatusCode is int statusCode)
        {
            return statusCode;
        }

        if (executed.Result is RedirectResult or RedirectToActionResult or RedirectToRouteResult)
        {
            return StatusCodes.Status302Found;
        }

        return context.HttpContext.Response.StatusCode;
    }
}
