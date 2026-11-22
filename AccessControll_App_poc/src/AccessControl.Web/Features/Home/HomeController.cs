using System.Diagnostics;
using AccessControl.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace AccessControl.Web.Features.Home;

public sealed class HomeController : Controller
{
    [HttpGet]
    public IActionResult Index() => View();

    [HttpGet]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() =>
        View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
