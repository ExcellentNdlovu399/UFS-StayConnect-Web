using Microsoft.AspNetCore.Mvc;

namespace UFSStayConnect.Controllers
{
    public class ProfileController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
