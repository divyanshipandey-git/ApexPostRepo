using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Web.Controllers;

public sealed class HomeController : Controller
{
    public IActionResult Index() => View();
}
