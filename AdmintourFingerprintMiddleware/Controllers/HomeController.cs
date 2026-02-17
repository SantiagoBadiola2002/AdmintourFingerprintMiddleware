using Microsoft.AspNetCore.Mvc;

namespace AdmintourFingerprintMiddleware.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
