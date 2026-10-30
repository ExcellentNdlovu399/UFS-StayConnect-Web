using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using UFSStayConnect.DataAccess;
using UFSStayConnect.Models;

namespace UFSStayConnect.Controllers
{
    public class HomeController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IRepositoryWrapper _repository;

        public HomeController(
            UserManager<ApplicationUser> userManager,
            IRepositoryWrapper repository)
        {
            _userManager = userManager;
            _repository = repository;
        }

        [AllowAnonymous]
        public async Task<IActionResult> Index()
        {
            // User is not logged in
            if (User.Identity == null ||
                !User.Identity.IsAuthenticated)
            {
                LoadAvailableAccommodations();
                return View();
            }

            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                LoadAvailableAccommodations();
                return View();
            }

            // ADMIN
            if (await _userManager.IsInRoleAsync(user, "Admin"))
            {
                return RedirectToAction(
                    "Index",
                    "Admin");
            }

            // FINANCIAL OFFICER
            if (await _userManager.IsInRoleAsync(
                    user,
                    "FinancialOfficer"))
            {
                return RedirectToAction(
                    "Index",
                    "FinancialOfficer");
            }

            // CUSTOMER
            if (await _userManager.IsInRoleAsync(
                    user,
                    "Customer"))
            {
                return RedirectToAction(
                    "Index",
                    "Dashboard");
            }

            LoadAvailableAccommodations();

            return View();
        }

        private void LoadAvailableAccommodations()
        {
            var accommodations = _repository.Accommodation
                .FindAllAccommodations()
                .Where(a =>
                    a.Available &&
                    a.AvailableRooms > 0)
                .OrderBy(a => a.Name)
                .ToList();

            ViewBag.AvailableAccommodations = accommodations;
        }
    }
}