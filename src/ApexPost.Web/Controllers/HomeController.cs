using Microsoft.AspNetCore.Mvc;
using ApexPost.Application.Services.Reports;
using ApexPost.Web.Models.Home;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace ApexPost.Web.Controllers;

public sealed class HomeController(BranchDashboardService dashboardService) : Controller
{
    [Authorize]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var dashboard = await dashboardService.GetAsync(
            User.FindFirstValue("StaffId"),
            cancellationToken);
        if (dashboard is null)
        {
            return Forbid();
        }

        return View(new BranchDashboardViewModel(
            dashboard.BranchCode,
            dashboard.Date,
            dashboard.DailySales.TransactionCount,
            dashboard.DailySales.RetailSales,
            dashboard.DailySales.ServiceSales,
            dashboard.DailySales.TotalSales,
            dashboard.LowStockItems));
    }

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View();
}
