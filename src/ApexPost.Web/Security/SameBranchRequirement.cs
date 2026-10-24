using System.Security.Claims;
using ApexPost.Domain.Entities;
using ApexPost.Domain.ValueObjects;
using Microsoft.AspNetCore.Authorization;

namespace ApexPost.Web.Security;

public sealed class SameBranchRequirement : IAuthorizationRequirement { }

public sealed class SameBranchAuthorizationHandler
    : AuthorizationHandler<SameBranchRequirement, HttpContext>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        SameBranchRequirement requirement,
        HttpContext resource)
    {
        var staffBranch = context.User.FindFirstValue("BranchCode");
        if (context.User.IsInRole(nameof(StaffRole.Administrator)))
        {
            context.Succeed(requirement);
            return;
        }

        if (string.IsNullOrWhiteSpace(staffBranch))
        {
            return;
        }

        var requestedBranch = resource.Request.Query["branchCode"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(requestedBranch)
            && HttpMethods.IsPost(resource.Request.Method)
            && resource.Request.HasFormContentType)
        {
            requestedBranch = (await resource.Request.ReadFormAsync())
                .TryGetValue("BranchCode", out var formValue)
                    ? formValue.FirstOrDefault()
                    : null;
        }

        if (string.IsNullOrWhiteSpace(requestedBranch))
        {
            context.Succeed(requirement);
            return;
        }

        try
        {
            var requestedCode = new BranchCode(requestedBranch);
            var staffCode = new BranchCode(staffBranch);
            if (requestedCode == staffCode)
            {
                context.Succeed(requirement);
            }
        }
        catch (ArgumentException)
        {
            // Let MVC's input validation render a useful error for malformed values.
            context.Succeed(requirement);
        }
    }
}
