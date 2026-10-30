using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UFSStayConnect.Data;
using UFSStayConnect.DataAccess;
using UFSStayConnect.Models;
using UFSStayConnect.Services;
using UFSStayConnect.ViewModels;

namespace UFSStayConnect.Controllers
{
    [Authorize(Roles = "Customer")]
    public class PaymentController : Controller
    {
        private readonly IRepositoryWrapper _repository;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<PaymentController> _logger;
        private readonly AppDbContext _db;

        public PaymentController(
            IRepositoryWrapper repository,
            UserManager<ApplicationUser> userManager,
            ILogger<PaymentController> logger,
            AppDbContext db)
        {
            _repository = repository;
            _userManager = userManager;
            _logger = logger;
            _db = db;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Challenge();

            // Include every approved accommodation application for this customer.
            var approvedApplications = _repository.Application
                .FindByUser(user.Id)
                .Where(a => string.Equals(
                    a.Status, "Approved", StringComparison.OrdinalIgnoreCase) && a.RoomID.HasValue)
                .ToList();

            if (approvedApplications.Count == 0)
            {
                ViewBag.Message = "You do not currently have an approved accommodation. Any active charges assigned by the administrator are still shown below.";
            }

            var allPayments = _repository.Payment
                .FindByUser(user.Id)
                .ToList();

            var today = DateTime.UtcNow.Date;
            var model = new List<CustomerPaymentViewModel>();

            foreach (var application in approvedApplications)
            {
                var accommodation = _repository.Accommodation
                    .GetAccommodationById(application.AccommodationID);

                if (accommodation == null)
                    continue;

                var room = application.RoomID.HasValue
                    ? _repository.Room.GetRoomById(application.RoomID.Value)
                    : null;

                var allAccommodationCharges = _repository.AccommodationCharge
                    .FindByAccommodation(application.AccommodationID)
                    .Where(c => c.IsActive)
                    .ToList();
                var parkingFee = ParkingFeeCalculator.ForApprovedApplication(
                    application, accommodation, allAccommodationCharges);
                // Parking is folded into the rent line so it cannot be paid twice.
                var charges = allAccommodationCharges
                    .Where(c => !ParkingFeeCalculator.IsParkingCharge(c.PaymentType?.Name))
                    .ToList();

                var applicationPayments = allPayments
                    .Where(p => p.ApplicationID == application.ApplicationID)
                    .ToList();

                foreach (var charge in charges)
                {
                    var dueDate = PaymentDueDateCalculator
                        .GetCurrentCycleDueDate(charge, today);

                    var latest = applicationPayments
                        .Where(p =>
                            p.AccommodationChargeID == charge.AccommodationChargeID &&
                            p.DueDate.HasValue &&
                            p.DueDate.Value.Date == dueDate.Date)
                        .OrderByDescending(p => p.PaymentDate)
                        .FirstOrDefault();

                    var status = latest?.PaymentStatus
                        ?? (dueDate < today ? "Overdue" : "Unpaid");

                    var parkingForLine = ParkingFeeCalculator.IsRentCharge(charge.PaymentType?.Name)
                        ? parkingFee : 0m;
                    model.Add(new CustomerPaymentViewModel
                    {
                        AccommodationChargeID = charge.AccommodationChargeID,
                        AccommodationID = charge.AccommodationID,
                        AccommodationName = accommodation.Name,
                        RoomNumber = room?.RoomNumber ?? "Not assigned",
                        PaymentTypeID = charge.PaymentTypeID,
                        PaymentTypeName = charge.PaymentType?.Name ?? "Unknown",
                        BaseAmount = charge.Amount,
                        ParkingFeeAmount = parkingForLine,
                        Amount = charge.Amount + parkingForLine,
                        Frequency = charge.Frequency,
                        Description = ParkingFeeCalculator.IsRentCharge(charge.PaymentType?.Name)
                            ? ParkingFeeCalculator.AddParkingNote(charge.Description, parkingForLine)
                            : charge.Description,
                        Status = status,
                        LastReference = latest?.ReferenceNumber,
                        LastPaymentDate = latest?.PaymentDate,
                        DueDate = dueDate
                    });
                }
            }

            // Administrator-assigned one-time charges are deliberately added to this
            // same payment collection/table, but keep their own ID so they cannot be
            // mistaken for an AccommodationCharge with the same numeric key.
            var assignedCharges = await _db.CustomerCharges
                .AsNoTracking()
                .Include(c => c.Accommodation)
                .Include(c => c.PaymentType)
                .Where(c => c.UserID == user.Id && c.IsActive)
                .OrderBy(c => c.DueDate)
                .ToListAsync();

            foreach (var assigned in assignedCharges)
            {
                var attempts = allPayments
                    .Where(p => p.CustomerChargeID == assigned.CustomerChargeID)
                    .OrderByDescending(p => p.PaymentDate)
                    .ToList();
                var latest = attempts.FirstOrDefault();
                var paidAmount = attempts
                    .Where(p => string.Equals(p.PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase))
                    .Sum(p => p.Amount);
                var pendingAmount = attempts
                    .Where(p => string.Equals(p.PaymentStatus, "Pending", StringComparison.OrdinalIgnoreCase))
                    .Sum(p => p.Amount);
                var remaining = Math.Max(0m, assigned.Amount - paidAmount);

                string status;
                if (remaining <= 0m)
                    status = "Paid";
                else if (pendingAmount > 0m)
                    status = "Pending";
                else if (latest != null && string.Equals(latest.PaymentStatus, "Rejected", StringComparison.OrdinalIgnoreCase))
                    status = "Rejected";
                else
                    status = assigned.DueDate.Date < today ? "Overdue" : "Unpaid";

                var linkedApplication = approvedApplications
                    .FirstOrDefault(a => a.ApplicationID == assigned.ApplicationID);
                var assignedRoom = linkedApplication?.RoomID is int roomId
                    ? _repository.Room.GetRoomById(roomId)?.RoomNumber
                    : null;

                model.Add(new CustomerPaymentViewModel
                {
                    CustomerChargeID = assigned.CustomerChargeID,
                    IsAssignedCustomerCharge = true,
                    ChargeTitle = assigned.Title,
                    AccommodationID = assigned.AccommodationID,
                    AccommodationName = assigned.Accommodation?.Name ?? "Accommodation",
                    RoomNumber = assignedRoom ?? "Not assigned",
                    PaymentTypeID = assigned.PaymentTypeID,
                    PaymentTypeName = assigned.PaymentType?.Name ?? "Other charge",
                    Amount = assigned.Amount,
                    BaseAmount = assigned.Amount,
                    Frequency = "One-time",
                    Description = assigned.Description,
                    Status = status,
                    LastReference = latest?.ReferenceNumber,
                    LastPaymentDate = latest?.PaymentDate,
                    DueDate = assigned.DueDate.Date
                });
            }

            ViewBag.AccommodationCount = model
                .Select(x => x.AccommodationID)
                .Distinct()
                .Count();

            ViewBag.TotalCharges = model.Sum(x => x.Amount);
            ViewBag.PaidAmount = model
                .Where(x => string.Equals(x.Status, "Paid", StringComparison.OrdinalIgnoreCase))
                .Sum(x => x.Amount);
            ViewBag.Outstanding = model
                .Where(x => !string.Equals(x.Status, "Paid", StringComparison.OrdinalIgnoreCase))
                .Sum(x => x.Amount);
            ViewBag.Overdue = model
                .Where(x => x.IsOverdue)
                .Sum(x => x.Amount);

            return View(model
                .OrderBy(x => x.AccommodationName)
                .ThenBy(x => x.DueDate)
                .ToList());
        }

        [HttpGet]
        public async Task<IActionResult> Checkout(
            [FromQuery] List<int>? ids,
            [FromQuery] List<int>? customerChargeIds)
        {
            var selectedAccommodationIds = (ids ?? new List<int>())
                .Where(id => id > 0).Distinct().ToList();
            var selectedCustomerChargeIds = (customerChargeIds ?? new List<int>())
                .Where(id => id > 0).Distinct().ToList();

            if (selectedAccommodationIds.Count == 0 && selectedCustomerChargeIds.Count == 0)
                return RedirectToAction(nameof(Index));

            var result = await BuildCheckoutModel(selectedAccommodationIds, selectedCustomerChargeIds);
            if (result == null)
            {
                TempData["ErrorMessage"] =
                    "One or more selected charges are no longer payable. Please review your payment list.";
                return RedirectToAction(nameof(Index));
            }

            return View(result);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Checkout(PaymentCheckoutViewModel model)
        {
            model.SelectedChargeIds ??= new List<int>();
            model.SelectedCustomerChargeIds ??= new List<int>();
            model.SelectedChargeIds = model.SelectedChargeIds.Where(id => id > 0).Distinct().ToList();
            model.SelectedCustomerChargeIds = model.SelectedCustomerChargeIds.Where(id => id > 0).Distinct().ToList();

            // Accept either 0528 or 05/28 from browsers, normalize to MM/YY,
            // and clear the pre-normalization attribute errors for this field.
            var expiryDigits = new string((model.ExpiryDate ?? string.Empty).Where(char.IsDigit).ToArray());
            if (expiryDigits.Length == 4 && (model.ExpiryDate ?? string.Empty).All(ch => char.IsDigit(ch) || ch == '/'))
            {
                model.ExpiryDate = expiryDigits.Substring(0, 2) + "/" + expiryDigits.Substring(2, 2);
                ModelState.Remove(nameof(model.ExpiryDate));
            }

            if (model.SelectedChargeIds.Count == 0 && model.SelectedCustomerChargeIds.Count == 0)
            {
                TempData["ErrorMessage"] = "Select at least one charge to pay.";
                return RedirectToAction(nameof(Index));
            }

            // Rebuild all checkout values from the database. Never trust posted amounts.
            var rebuilt = await BuildCheckoutModel(model.SelectedChargeIds, model.SelectedCustomerChargeIds);
            if (rebuilt == null)
            {
                TempData["ErrorMessage"] =
                    "One or more selected charges are no longer payable. Please review your payment list.";
                return RedirectToAction(nameof(Index));
            }

            model.AccommodationName = rebuilt.AccommodationName;
            model.RoomNumber = rebuilt.RoomNumber;
            model.Items = rebuilt.Items;
            model.TotalAmount = rebuilt.TotalAmount;

            var method = model.PaymentMethod ?? string.Empty;

            if (!method.Equals("Card", StringComparison.OrdinalIgnoreCase) &&
                !method.Equals("EFT", StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(
                    nameof(model.PaymentMethod),
                    "Select a valid payment method.");
            }

            if (method.Equals("Card", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(model.CardHolderName))
                    ModelState.AddModelError(nameof(model.CardHolderName), "Cardholder name is required.");

                var digits = new string((model.CardNumber ?? string.Empty)
                    .Where(char.IsDigit).ToArray());

                if (digits.Length < 12 || digits.Length > 19)
                    ModelState.AddModelError(nameof(model.CardNumber), "Enter a valid card number.");

                if (string.IsNullOrWhiteSpace(model.ExpiryDate))
                    ModelState.AddModelError(nameof(model.ExpiryDate), "Expiry date is required.");
                else if (!TryValidateExpiry(model.ExpiryDate, out var expiryError))
                    ModelState.AddModelError(nameof(model.ExpiryDate), expiryError);

                if (string.IsNullOrWhiteSpace(model.CVV) ||
                    model.CVV.Length < 3 ||
                    model.CVV.Length > 4)
                {
                    ModelState.AddModelError(nameof(model.CVV), "Enter a valid CVV.");
                }
            }
            else if (method.Equals("EFT", StringComparison.OrdinalIgnoreCase) &&
                     string.IsNullOrWhiteSpace(model.EFTReference))
            {
                ModelState.AddModelError(
                    nameof(model.EFTReference),
                    "Enter your EFT reference number.");
            }

            if (!ModelState.IsValid)
                return View(model);

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Challenge();

            var approvedApplications = _repository.Application
                .FindByUser(user.Id)
                .Where(a => string.Equals(
                    a.Status, "Approved", StringComparison.OrdinalIgnoreCase) && a.RoomID.HasValue)
                .ToList();

            var applicationsByAccommodation = approvedApplications
                .GroupBy(a => a.AccommodationID)
                .ToDictionary(g => g.Key, g => g.First());

            var batch = "BATCH-" +
                DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") + "-" +
                Random.Shared.Next(100, 999);

            var cardDigits = new string((model.CardNumber ?? string.Empty)
                .Where(char.IsDigit).ToArray());

            var last4 = method.Equals("Card", StringComparison.OrdinalIgnoreCase)
                ? cardDigits.TakeLast(4).Aggregate(string.Empty, (a, c) => a + c)
                : null;

            foreach (var item in rebuilt.Items)
            {
                if (item.IsAssignedCustomerCharge)
                {
                    var assigned = await _db.CustomerCharges
                        .Include(c => c.Payments)
                        .FirstOrDefaultAsync(c => c.CustomerChargeID == item.CustomerChargeID
                                                  && c.UserID == user.Id && c.IsActive);
                    if (assigned == null)
                    {
                        TempData["ErrorMessage"] = "An assigned charge is no longer available. Please refresh your payments.";
                        return RedirectToAction(nameof(Index));
                    }

                    var paidAmount = assigned.Payments
                        .Where(p => string.Equals(p.PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase))
                        .Sum(p => p.Amount);
                    var pendingAmount = assigned.Payments
                        .Where(p => string.Equals(p.PaymentStatus, "Pending", StringComparison.OrdinalIgnoreCase))
                        .Sum(p => p.Amount);
                    var remaining = assigned.Amount - paidAmount;
                    if (pendingAmount > 0m || remaining <= 0m || item.Amount > remaining)
                    {
                        TempData["ErrorMessage"] = "This assigned charge has already been paid or is awaiting verification.";
                        return RedirectToAction(nameof(Index));
                    }

                    var chargeReference = method.Equals("EFT", StringComparison.OrdinalIgnoreCase)
                        ? model.EFTReference!.Trim() + "-CC-" + assigned.CustomerChargeID
                        : "CARD-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") + "-CC-" + assigned.CustomerChargeID;

                    _repository.Payment.CreatePayment(new Payment
                    {
                        UserID = user.Id,
                        PaymentTypeID = assigned.PaymentTypeID,
                        ApplicationID = assigned.ApplicationID,
                        AccommodationChargeID = null,
                        CustomerChargeID = assigned.CustomerChargeID,
                        DueDate = assigned.DueDate.Date,
                        Amount = item.Amount,
                        PaymentStatus = "Pending",
                        ReferenceNumber = chargeReference,
                        PaymentMethod = method,
                        PaymentBatchReference = batch,
                        PaymentDate = DateTime.UtcNow,
                        Description = (method.Equals("Card", StringComparison.OrdinalIgnoreCase)
                            ? $"Card payment ending {last4}"
                            : "EFT payment") + $"; administrator-assigned charge: {assigned.Title}"
                    });

                    continue;
                }

                var charge = _repository.AccommodationCharge.GetChargeById(item.AccommodationChargeID);
                if (charge == null || !charge.IsActive)
                {
                    TempData["ErrorMessage"] = "A selected charge is no longer available. Please try again.";
                    return RedirectToAction(nameof(Index));
                }

                if (!applicationsByAccommodation.TryGetValue(charge.AccommodationID, out var application))
                    return Forbid();

                var dueDate = PaymentDueDateCalculator.GetCurrentCycleDueDate(charge);
                var reference = method.Equals("EFT", StringComparison.OrdinalIgnoreCase)
                    ? model.EFTReference!.Trim() + "-AC-" + item.AccommodationChargeID
                    : "CARD-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") + "-AC-" + item.AccommodationChargeID;

                _repository.Payment.CreatePayment(new Payment
                {
                    UserID = user.Id,
                    PaymentTypeID = item.PaymentTypeID,
                    AccommodationChargeID = item.AccommodationChargeID,
                    CustomerChargeID = null,
                    ApplicationID = application.ApplicationID,
                    DueDate = dueDate,
                    Amount = item.Amount,
                    PaymentStatus = "Pending",
                    ReferenceNumber = reference,
                    PaymentMethod = method,
                    PaymentBatchReference = batch,
                    PaymentDate = DateTime.UtcNow,
                    Description = (method.Equals("Card", StringComparison.OrdinalIgnoreCase)
                        ? $"Card payment ending {last4}"
                        : "EFT payment") + (item.ParkingFeeAmount > 0m
                            ? $"; includes parking R{item.ParkingFeeAmount:N2}"
                            : string.Empty)
                });
            }

            _repository.Save();

            _repository.Notification.CreateNotification(new Notification
            {
                UserID = user.Id,
                Title = "Payment Submitted",
                Message = $"Your payment of R{rebuilt.TotalAmount:N2} across your selected charges has been submitted and is awaiting verification.",
                DateCreated = DateTime.UtcNow
            });

            await NotifyPaymentStaffAsync(rebuilt.TotalAmount, user, batch);
            _repository.Save();

            TempData["SuccessMessage"] =
                $"Payment of R{rebuilt.TotalAmount:N2} submitted successfully.";

            return RedirectToAction(nameof(History));
        }

        [HttpGet]
        public async Task<IActionResult> History()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Challenge();

            return View(_repository.Payment.FindByUser(user.Id).ToList());
        }

        [HttpGet]
        public async Task<IActionResult> Receipt(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Challenge();

            var payment = _repository.Payment.GetPaymentById(id);
            if (payment == null || payment.UserID != user.Id)
                return NotFound();

            return View(payment);
        }

        private async Task NotifyPaymentStaffAsync(
            decimal total,
            ApplicationUser customer,
            string batchReference)
        {
            var message =
                $"Customer {customer.FirstName} {customer.Surname} submitted payment batch " +
                $"{batchReference} totalling R{total:N2}. Verification is required.";

            foreach (var role in new[] { "Admin", "FinancialOfficer" })
            {
                var staff = await _userManager.GetUsersInRoleAsync(role);

                foreach (var staffUser in staff)
                {
                    _repository.Notification.CreateNotification(new Notification
                    {
                        UserID = staffUser.Id,
                        Title = "Payment Awaiting Verification",
                        Message = message,
                        DateCreated = DateTime.UtcNow
                    });
                }
            }
        }

        private async Task<PaymentCheckoutViewModel?> BuildCheckoutModel(
            List<int> ids,
            List<int>? customerChargeIds = null)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return null;

            ids = ids.Where(id => id > 0).Distinct().ToList();
            customerChargeIds = (customerChargeIds ?? new List<int>())
                .Where(id => id > 0).Distinct().ToList();
            if (ids.Count == 0 && customerChargeIds.Count == 0) return null;

            var approvedApplications = _repository.Application
                .FindByUser(user.Id)
                .Where(a => string.Equals(a.Status, "Approved", StringComparison.OrdinalIgnoreCase)
                            && a.RoomID.HasValue)
                .ToList();
            var applicationsByAccommodation = approvedApplications
                .GroupBy(a => a.AccommodationID)
                .ToDictionary(g => g.Key, g => g.First());
            var eligibleAccommodationIds = applicationsByAccommodation.Keys.ToHashSet();

            var allChargesByAccommodation = new Dictionary<int, List<AccommodationCharge>>();
            var accommodationsById = new Dictionary<int, Accommodation>();
            var chargesByAccommodation = new Dictionary<int, List<AccommodationCharge>>();
            foreach (var accommodationId in eligibleAccommodationIds)
            {
                var allCharges = _repository.AccommodationCharge
                    .FindByAccommodation(accommodationId)
                    .Where(c => c.IsActive)
                    .ToList();
                allChargesByAccommodation[accommodationId] = allCharges;
                chargesByAccommodation[accommodationId] = allCharges
                    .Where(c => !ParkingFeeCalculator.IsParkingCharge(c.PaymentType?.Name))
                    .ToList();

                var accommodation = _repository.Accommodation.GetAccommodationById(accommodationId);
                if (accommodation != null) accommodationsById[accommodationId] = accommodation;
            }

            var selectedCharges = chargesByAccommodation.Values
                .SelectMany(charges => charges)
                .Where(c => ids.Contains(c.AccommodationChargeID))
                .GroupBy(c => c.AccommodationChargeID)
                .Select(g => g.First())
                .ToList();
            // Parking is included in the Rent line. Never allow selecting the
            // standalone parking charge in addition to rent.
            if (selectedCharges.Count != ids.Count) return null;

            var allPayments = _repository.Payment.FindByUser(user.Id).ToList();
            var today = DateTime.UtcNow.Date;
            var payableCharges = new List<AccommodationCharge>();
            foreach (var charge in selectedCharges)
            {
                var application = applicationsByAccommodation[charge.AccommodationID];
                var dueDate = PaymentDueDateCalculator.GetCurrentCycleDueDate(charge, today);
                var latest = allPayments
                    .Where(p => p.ApplicationID == application.ApplicationID
                                && p.AccommodationChargeID == charge.AccommodationChargeID
                                && p.DueDate.HasValue && p.DueDate.Value.Date == dueDate.Date)
                    .OrderByDescending(p => p.PaymentDate)
                    .FirstOrDefault();
                if (latest == null || string.Equals(latest.PaymentStatus, "Rejected", StringComparison.OrdinalIgnoreCase))
                    payableCharges.Add(charge);
            }
            if (payableCharges.Count != ids.Count) return null;

            var selectedCustomerCharges = new List<CustomerCharge>();
            if (customerChargeIds.Count > 0)
            {
                selectedCustomerCharges = await _db.CustomerCharges
                    .AsNoTracking()
                    .Include(c => c.Accommodation)
                    .Include(c => c.PaymentType)
                    .Include(c => c.Payments)
                    .Where(c => c.UserID == user.Id && c.IsActive && customerChargeIds.Contains(c.CustomerChargeID))
                    .ToListAsync();

                // Do not assume customer-charge IDs and accommodation-charge IDs
                // are in the same key space.
                if (selectedCustomerCharges.Count != customerChargeIds.Count) return null;

                foreach (var assigned in selectedCustomerCharges)
                {
                    var paid = assigned.Payments
                        .Where(p => string.Equals(p.PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase))
                        .Sum(p => p.Amount);
                    var pending = assigned.Payments
                        .Where(p => string.Equals(p.PaymentStatus, "Pending", StringComparison.OrdinalIgnoreCase))
                        .Sum(p => p.Amount);
                    if (pending > 0m || paid >= assigned.Amount) return null;
                }
            }

            var items = new List<PaymentCheckoutItemViewModel>();
            foreach (var charge in payableCharges)
            {
                var application = applicationsByAccommodation[charge.AccommodationID];
                var parkingFee = 0m;
                if (ParkingFeeCalculator.IsRentCharge(charge.PaymentType?.Name)
                    && accommodationsById.TryGetValue(charge.AccommodationID, out var accommodation))
                {
                    parkingFee = ParkingFeeCalculator.ForApprovedApplication(
                        application, accommodation, allChargesByAccommodation[charge.AccommodationID]);
                }

                items.Add(new PaymentCheckoutItemViewModel
                {
                    AccommodationChargeID = charge.AccommodationChargeID,
                    IsAssignedCustomerCharge = false,
                    PaymentTypeID = charge.PaymentTypeID,
                    PaymentTypeName = charge.PaymentType?.Name ?? "Unknown",
                    ChargeTitle = charge.PaymentType?.Name ?? "Accommodation charge",
                    BaseAmount = charge.Amount,
                    ParkingFeeAmount = parkingFee,
                    Amount = charge.Amount + parkingFee,
                    Frequency = charge.Frequency,
                    DueDate = PaymentDueDateCalculator.GetCurrentCycleDueDate(charge, today)
                });
            }

            foreach (var assigned in selectedCustomerCharges)
            {
                var paid = assigned.Payments
                    .Where(p => string.Equals(p.PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase))
                    .Sum(p => p.Amount);
                var remaining = Math.Max(0m, assigned.Amount - paid);
                items.Add(new PaymentCheckoutItemViewModel
                {
                    CustomerChargeID = assigned.CustomerChargeID,
                    IsAssignedCustomerCharge = true,
                    PaymentTypeID = assigned.PaymentTypeID,
                    PaymentTypeName = assigned.PaymentType?.Name ?? "Other charge",
                    ChargeTitle = assigned.Title,
                    BaseAmount = remaining,
                    Amount = remaining,
                    Frequency = "One-time",
                    DueDate = assigned.DueDate.Date
                });
            }

            var accommodationNames = new List<string>();
            accommodationNames.AddRange(payableCharges
                .Select(c => accommodationsById.TryGetValue(c.AccommodationID, out var a) ? a.Name : null)
                .OfType<string>()
                .Where(name => !string.IsNullOrWhiteSpace(name)));
            accommodationNames.AddRange(selectedCustomerCharges
                .Select(c => c.Accommodation?.Name)
                .OfType<string>()
                .Where(name => !string.IsNullOrWhiteSpace(name)));
            accommodationNames = accommodationNames.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            var roomNames = new List<string>();
            foreach (var charge in payableCharges)
            {
                var app = applicationsByAccommodation[charge.AccommodationID];
                if (app.RoomID.HasValue)
                {
                    var room = _repository.Room.GetRoomById(app.RoomID.Value)?.RoomNumber;
                    if (!string.IsNullOrWhiteSpace(room)) roomNames.Add(room);
                }
            }
            foreach (var assigned in selectedCustomerCharges)
            {
                var app = approvedApplications.FirstOrDefault(a => a.ApplicationID == assigned.ApplicationID);
                if (app?.RoomID is int roomId)
                {
                    var room = _repository.Room.GetRoomById(roomId)?.RoomNumber;
                    if (!string.IsNullOrWhiteSpace(room)) roomNames.Add(room);
                }
            }
            roomNames = roomNames.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            return new PaymentCheckoutViewModel
            {
                SelectedChargeIds = payableCharges.Select(c => c.AccommodationChargeID).ToList(),
                SelectedCustomerChargeIds = selectedCustomerCharges.Select(c => c.CustomerChargeID).ToList(),
                AccommodationName = accommodationNames.Count == 0 ? "Assigned charges" : accommodationNames.Count == 1 ? accommodationNames[0] : "Multiple accommodations",
                RoomNumber = roomNames.Count == 0 ? "Not assigned" : roomNames.Count == 1 ? roomNames[0] : "Multiple rooms",
                Items = items,
                TotalAmount = items.Sum(i => i.Amount)
            };
        }

        private static bool TryValidateExpiry(string? value, out string error)
        {
            error = "Expiry date must be in MM/YY format, for example 05/28.";
            if (string.IsNullOrWhiteSpace(value)) return false;
            var trimmed = value.Trim();
            if (!System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^(0[1-9]|1[0-2])/?\d{2}$"))
                return false;
            var digits = new string(trimmed.Where(char.IsDigit).ToArray());
            if (!int.TryParse(digits.Substring(0, 2), out var month) || month < 1 || month > 12)
                return false;
            if (!int.TryParse(digits.Substring(2, 2), out var shortYear)) return false;
            var year = 2000 + shortYear;
            var endOfMonth = new DateTime(year, month, DateTime.DaysInMonth(year, month));
            if (endOfMonth < DateTime.Today)
            {
                error = "This card has expired.";
                return false;
            }
            return true;
        }

        private void SetEmptySummary()
        {
            ViewBag.TotalCharges = 0m;
            ViewBag.PaidAmount = 0m;
            ViewBag.Outstanding = 0m;
            ViewBag.Overdue = 0m;
            ViewBag.AccommodationCount = 0;
        }
    }
}
