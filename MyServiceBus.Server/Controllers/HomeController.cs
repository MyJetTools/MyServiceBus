using Microsoft.AspNetCore.Mvc;

namespace MyServiceBus.Server.Controllers
{
    public class HomeController : Controller
    {

        [HttpGet("/")]
        public IActionResult Index()
        {
            return View();
        }
    }
}