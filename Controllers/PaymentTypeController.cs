using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UFSStayConnect.DataAccess;
using UFSStayConnect.Models;

namespace UFSStayConnect.Controllers
{
    [Authorize(Roles = "Admin")]
    public class PaymentTypeController : Controller
    {
        private readonly IRepositoryWrapper _repository;
        private readonly ILogger<PaymentTypeController> _logger;

        public PaymentTypeController(
            IRepositoryWrapper repository,
            ILogger<PaymentTypeController> logger)
        {
            _repository = repository;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Index()
        {
            try
            {
                var paymentTypes = _repository.PaymentType
                    .FindAllPaymentTypes()
                    .ToList();

                return View(paymentTypes);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error loading payment types.");

                TempData["ErrorMessage"] =
                    "Unable to load payment types.";

                return View(new List<PaymentType>());
            }
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View(new PaymentType
            {
                IsActive = true
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(PaymentType model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                var name = model.Name.Trim();

                if (_repository.PaymentType
                    .FindAllPaymentTypes()
                    .Any(p =>
                        p.Name.Equals(
                            name,
                            StringComparison.OrdinalIgnoreCase)))
                {
                    ModelState.AddModelError(
                        "Name",
                        "A payment type with this name already exists.");

                    return View(model);
                }

                var paymentType = new PaymentType
                {
                    Name = name,
                    Description = string.IsNullOrWhiteSpace(
                        model.Description)
                        ? null
                        : model.Description.Trim(),
                    IsActive = model.IsActive
                };

                _repository.PaymentType
                    .CreatePaymentType(paymentType);

                _repository.Save();

                TempData["SuccessMessage"] =
                    "Payment type was added successfully.";

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error creating payment type.");

                ModelState.AddModelError(
                    "",
                    "The payment type could not be added.");

                return View(model);
            }
        }

        [HttpGet]
        public IActionResult Edit(int id)
        {
            if (id <= 0)
            {
                return BadRequest();
            }

            var paymentType = _repository.PaymentType
                .GetPaymentTypeById(id);

            if (paymentType == null)
            {
                return NotFound();
            }

            return View(paymentType);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(PaymentType model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                var paymentType = _repository.PaymentType
                    .GetPaymentTypeById(model.PaymentTypeID);

                if (paymentType == null)
                {
                    return NotFound();
                }

                var name = model.Name.Trim();

                var duplicate = _repository.PaymentType
                    .FindAllPaymentTypes()
                    .Any(p =>
                        p.PaymentTypeID != model.PaymentTypeID &&
                        p.Name.Equals(
                            name,
                            StringComparison.OrdinalIgnoreCase));

                if (duplicate)
                {
                    ModelState.AddModelError(
                        "Name",
                        "A payment type with this name already exists.");

                    return View(model);
                }

                paymentType.Name = name;

                paymentType.Description =
                    string.IsNullOrWhiteSpace(model.Description)
                        ? null
                        : model.Description.Trim();

                paymentType.IsActive = model.IsActive;

                _repository.PaymentType
                    .UpdatePaymentType(paymentType);

                _repository.Save();

                TempData["SuccessMessage"] =
                    "Payment type was updated successfully.";

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error updating payment type {PaymentTypeID}.",
                    model.PaymentTypeID);

                ModelState.AddModelError(
                    "",
                    "The payment type could not be updated.");

                return View(model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ToggleStatus(int id)
        {
            if (id <= 0)
            {
                return BadRequest();
            }

            try
            {
                var paymentType = _repository.PaymentType
                    .GetPaymentTypeById(id);

                if (paymentType == null)
                {
                    return NotFound();
                }

                paymentType.IsActive = !paymentType.IsActive;

                _repository.PaymentType
                    .UpdatePaymentType(paymentType);

                _repository.Save();

                TempData["SuccessMessage"] =
                    paymentType.IsActive
                        ? "Payment type activated."
                        : "Payment type deactivated.";

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error changing status for payment type {PaymentTypeID}.",
                    id);

                TempData["ErrorMessage"] =
                    "The payment type status could not be changed.";

                return RedirectToAction(nameof(Index));
            }
        }
    }
}