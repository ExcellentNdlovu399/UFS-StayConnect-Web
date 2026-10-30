using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using UFSStayConnect.DataAccess;
using UFSStayConnect.Models;
using UFSStayConnect.ViewModels;
using UFSStayConnect.Services;

namespace UFSStayConnect.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AccommodationChargeController : Controller
    {
        private readonly IRepositoryWrapper _repository;
        private readonly ILogger<AccommodationChargeController> _logger;

        public AccommodationChargeController(
            IRepositoryWrapper repository,
            ILogger<AccommodationChargeController> logger)
        {
            _repository = repository;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Manage(int accommodationId)
        {
            if (accommodationId <= 0)
                return BadRequest();

            try
            {
                var accommodation =
                    _repository.Accommodation
                        .GetAccommodationById(accommodationId);

                if (accommodation == null)
                    return NotFound();

                var charges =
                    _repository.AccommodationCharge
                        .FindByAccommodation(accommodationId)
                        .ToList();

                ViewBag.Accommodation = accommodation;

                return View(charges);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error loading charges for accommodation {AccommodationID}.",
                    accommodationId);

                TempData["ErrorMessage"] =
                    "Unable to load accommodation charges.";

                return RedirectToAction("Manage", "Accommodation");
            }
        }

        [HttpGet]
        public IActionResult Create(int accommodationId)
        {
            if (accommodationId <= 0)
                return BadRequest();

            var accommodation =
                _repository.Accommodation
                    .GetAccommodationById(accommodationId);

            if (accommodation == null)
                return NotFound();

            var model = new AccommodationChargeViewModel
            {
                AccommodationID = accommodationId,
                IsActive = true,
                Frequency = "Monthly",
                DueDate = UFSStayConnect.Services.PaymentDueDateCalculator.DefaultDueDate(),
                ReminderDaysBeforeDue = 3,
                ReminderDaysAfterDue = 3
            };

            PopulateDropdowns(model);

            ViewBag.Accommodation = accommodation;

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(
            AccommodationChargeViewModel model)
        {
            if (!ModelState.IsValid)
            {
                PopulateDropdowns(model);

                ViewBag.Accommodation =
                    _repository.Accommodation
                        .GetAccommodationById(
                            model.AccommodationID);

                return View(model);
            }

            try
            {
                var accommodation =
                    _repository.Accommodation
                        .GetAccommodationById(
                            model.AccommodationID);

                if (accommodation == null)
                    return NotFound();

                var paymentType =
                    _repository.PaymentType
                        .GetPaymentTypeById(
                            model.PaymentTypeID);

                if (paymentType == null)
                {
                    ModelState.AddModelError(
                        "PaymentTypeID",
                        "Please select a valid payment type.");

                    PopulateDropdowns(model);
                    ViewBag.Accommodation = accommodation;

                    return View(model);
                }

                var existingCharge =
                    _repository.AccommodationCharge
                        .FindByAccommodation(
                            model.AccommodationID)
                        .FirstOrDefault(
                            c => c.PaymentTypeID ==
                                 model.PaymentTypeID);

                if (existingCharge != null)
                {
                    ModelState.AddModelError(
                        "PaymentTypeID",
                        "This charge type already exists for this residence.");

                    PopulateDropdowns(model);
                    ViewBag.Accommodation = accommodation;

                    return View(model);
                }

                var charge = new AccommodationCharge
                {
                    AccommodationID =
                        model.AccommodationID,

                    PaymentTypeID =
                        model.PaymentTypeID,

                    Amount =
                        model.Amount,

                    Frequency =
                        model.Frequency,

                    DueDate =
                        model.DueDate,

                    ReminderDaysBeforeDue =
                        model.ReminderDaysBeforeDue,

                    ReminderDaysAfterDue =
                        model.ReminderDaysAfterDue,

                    Description =
                        model.Description,

                    IsActive =
                        model.IsActive
                };

                _repository.AccommodationCharge
                    .CreateCharge(charge);

                _repository.Save();

                TempData["SuccessMessage"] =
                    "Payment charge was added successfully.";

                return RedirectToAction(
                    nameof(Manage),
                    new
                    {
                        accommodationId =
                            model.AccommodationID
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error creating accommodation charge.");

                ModelState.AddModelError(
                    "",
                    "The payment charge could not be added.");

                PopulateDropdowns(model);

                ViewBag.Accommodation =
                    _repository.Accommodation
                        .GetAccommodationById(
                            model.AccommodationID);

                return View(model);
            }
        }

        [HttpGet]
        public IActionResult Edit(int id)
        {
            if (id <= 0)
                return BadRequest();

            var charge =
                _repository.AccommodationCharge
                    .GetChargeById(id);

            if (charge == null)
                return NotFound();

            var model = new AccommodationChargeViewModel
            {
                AccommodationChargeID =
                    charge.AccommodationChargeID,

                AccommodationID =
                    charge.AccommodationID,

                PaymentTypeID =
                    charge.PaymentTypeID,

                Amount =
                    charge.Amount,

                Frequency =
                    charge.Frequency,

                DueDate =
                    charge.DueDate ?? UFSStayConnect.Services.PaymentDueDateCalculator.DefaultDueDate(),

                ReminderDaysBeforeDue =
                    charge.ReminderDaysBeforeDue,

                ReminderDaysAfterDue =
                    charge.ReminderDaysAfterDue,

                Description =
                    charge.Description,

                IsActive =
                    charge.IsActive
            };

            PopulateDropdowns(model);

            ViewBag.Accommodation =
                _repository.Accommodation
                    .GetAccommodationById(
                        charge.AccommodationID);

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(
            AccommodationChargeViewModel model)
        {
            if (!ModelState.IsValid)
            {
                PopulateDropdowns(model);

                ViewBag.Accommodation =
                    _repository.Accommodation
                        .GetAccommodationById(
                            model.AccommodationID);

                return View(model);
            }

            try
            {
                var charge =
                    _repository.AccommodationCharge
                        .GetChargeById(
                            model.AccommodationChargeID);

                if (charge == null)
                    return NotFound();

                var duplicate =
                    _repository.AccommodationCharge
                        .FindByAccommodation(
                            model.AccommodationID)
                        .FirstOrDefault(
                            c =>
                                c.PaymentTypeID ==
                                    model.PaymentTypeID &&
                                c.AccommodationChargeID !=
                                    model.AccommodationChargeID);

                if (duplicate != null)
                {
                    ModelState.AddModelError(
                        "PaymentTypeID",
                        "This charge type already exists for this residence.");

                    PopulateDropdowns(model);

                    ViewBag.Accommodation =
                        _repository.Accommodation
                            .GetAccommodationById(
                                model.AccommodationID);

                    return View(model);
                }

                charge.PaymentTypeID =
                    model.PaymentTypeID;

                charge.Amount =
                    model.Amount;

                charge.Frequency =
                    model.Frequency;

                charge.DueDate =
                    model.DueDate;

                charge.ReminderDaysBeforeDue =
                    model.ReminderDaysBeforeDue;

                charge.ReminderDaysAfterDue =
                    model.ReminderDaysAfterDue;

                charge.Description =
                    model.Description;

                charge.IsActive =
                    model.IsActive;

                _repository.AccommodationCharge
                    .UpdateCharge(charge);

                _repository.Save();

                TempData["SuccessMessage"] =
                    "Payment charge was updated successfully.";

                return RedirectToAction(
                    nameof(Manage),
                    new
                    {
                        accommodationId =
                            charge.AccommodationID
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error updating accommodation charge {ChargeID}.",
                    model.AccommodationChargeID);

                ModelState.AddModelError(
                    "",
                    "The payment charge could not be updated.");

                PopulateDropdowns(model);

                ViewBag.Accommodation =
                    _repository.Accommodation
                        .GetAccommodationById(
                            model.AccommodationID);

                return View(model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ToggleStatus(int id)
        {
            if (id <= 0)
                return BadRequest();

            var charge =
                _repository.AccommodationCharge
                    .GetChargeById(id);

            if (charge == null)
                return NotFound();

            charge.IsActive = !charge.IsActive;

            _repository.AccommodationCharge
                .UpdateCharge(charge);

            _repository.Save();

            TempData["SuccessMessage"] =
                charge.IsActive
                    ? "Payment charge activated."
                    : "Payment charge deactivated.";

            return RedirectToAction(
                nameof(Manage),
                new
                {
                    accommodationId =
                        charge.AccommodationID
                });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Delete(int id)
        {
            if (id <= 0)
                return BadRequest();

            var charge =
                _repository.AccommodationCharge
                    .GetChargeById(id);

            if (charge == null)
                return NotFound();

            var accommodationId =
                charge.AccommodationID;

            _repository.AccommodationCharge
                .DeleteCharge(charge);

            _repository.Save();

            TempData["SuccessMessage"] =
                "Payment charge was deleted.";

            return RedirectToAction(
                nameof(Manage),
                new
                {
                    accommodationId
                });
        }

        private void PopulateDropdowns(
            AccommodationChargeViewModel model)
        {
            model.Accommodations =
                _repository.Accommodation
                    .FindAllAccommodations()
                    .Select(a => new SelectListItem
                    {
                        Value =
                            a.AccommodationID.ToString(),

                        Text =
                            a.Name,

                        Selected =
                            a.AccommodationID ==
                            model.AccommodationID
                    })
                    .ToList();

            model.PaymentTypes =
                _repository.PaymentType
                    .FindAllPaymentTypes()
                    .Where(p => p.IsActive)
                    .Select(p => new SelectListItem
                    {
                        Value =
                            p.PaymentTypeID.ToString(),

                        Text =
                            p.Name,

                        Selected =
                            p.PaymentTypeID ==
                            model.PaymentTypeID
                    })
                    .ToList();
        }
    }
}