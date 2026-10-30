using System.Globalization;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UFSStayConnect.Data;
using UFSStayConnect.DataAccess;
using UFSStayConnect.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using UFSStayConnect.Services;

namespace UFSStayConnect.Controllers.Api
{
    /// <summary>
    /// Mobile API for customer payments. Mirrors the web PaymentController:
    ///   GET  api/payments/summary          account totals
    ///   GET  api/payments/charges          every charge (selectable ones have canPay = true)
    ///   GET  api/payments/checkout?ids=1&amp;ids=2   rebuilt checkout summary for selected charges
    ///   POST api/payments/checkout         submit Card / EFT payment for selected charges
    ///   GET  api/payments/history          transaction history
    ///   GET  api/payments/{id}/receipt     receipt data (owner only)
    ///
    /// If you already have a PaymentsController in your API project, replace it with this one.
    /// If your API uses JWT alongside cookies, change the attribute to:
    ///   [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Customer")]
    /// </summary>
    [ApiController]
    [Route("api/payments")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Customer")]
    public class PaymentsController : ControllerBase
    {
        private static readonly Regex EftPattern = new(@"^T\d{1,19}$", RegexOptions.Compiled);

        private readonly IRepositoryWrapper _repository;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<PaymentsController> _logger;
        private readonly AppDbContext _db;

        public PaymentsController(
            IRepositoryWrapper repository,
            UserManager<ApplicationUser> userManager,
            ILogger<PaymentsController> logger,
            AppDbContext db)
        {
            _repository = repository;
            _userManager = userManager;
            _logger = logger;
            _db = db;
        }

        // =========================================================
        // REQUEST / INTERNAL MODELS
        // =========================================================

        public class CheckoutRequest
        {
            public List<int>? SelectedChargeIds { get; set; }
            public List<int>? SelectedCustomerChargeIds { get; set; }
            public string? PaymentMethod { get; set; }
            public string? CardHolderName { get; set; }
            public string? CardNumber { get; set; }
            public string? ExpiryDate { get; set; }
            public string? Cvv { get; set; }
            public string? EftReference { get; set; }
        }

        private sealed class ChargeLine
        {
            public AccommodationCharge Charge = null!;
            public Models.Application Application = null!;
            public string AccommodationName = "";
            public string RoomNumber = "";
            public DateTime DueDate;
            public Payment? Latest;
            public string Status = "";
            public bool CanPay;
            public bool IsOverdue;
        }

        private sealed class CustomerChargeLine
        {
            public CustomerCharge Charge = null!;
            public string AccommodationName = "";
            public decimal PaidAmount;
            public decimal PendingAmount;
            public decimal Balance;
            public DateTime DueDate;
            public Payment? Latest;
            public string Status = "";
            public bool CanPay;
            public bool IsOverdue;
        }

        // =========================================================
        // SUMMARY
        // =========================================================

        [HttpGet("summary")]
        public async Task<IActionResult> Summary()
        {
            var user = await GetUserAsync();
            if (user == null) return Unauthorized(new { message = "Please sign in again." });

            var lines = BuildLines(user);
            var assigned = await BuildCustomerChargeLinesAsync(user);
            var payments = _repository.Payment.FindByUser(user.Id).ToList();

            var total = lines.Sum(l => l.Charge.Amount) + assigned.Sum(l => l.Charge.Amount);
            var paid = lines.Where(l => IsStatus(l.Status, "Paid")).Sum(l => l.Charge.Amount)
                + assigned.Sum(l => Math.Min(l.Charge.Amount, l.PaidAmount));
            var pending = lines.Where(l => IsStatus(l.Status, "Pending")).Sum(l => l.Charge.Amount)
                + assigned.Sum(l => l.PendingAmount);
            var outstanding = lines.Where(l => !IsStatus(l.Status, "Paid")).Sum(l => l.Charge.Amount)
                + assigned.Sum(l => Math.Max(0m, l.Charge.Amount - l.PaidAmount));
            var overdue = lines.Where(l => l.IsOverdue).Sum(l => l.Charge.Amount)
                + assigned.Where(l => l.IsOverdue && l.CanPay).Sum(l => l.Balance);

            var nextAccommodation = lines.Where(l => l.CanPay).OrderBy(l => l.DueDate).FirstOrDefault();
            var nextAssigned = assigned.Where(l => l.CanPay).OrderBy(l => l.DueDate).FirstOrDefault();
            DateTime? nextDate = null;
            decimal nextAmount = 0m;
            if (nextAccommodation != null && (nextAssigned == null || nextAccommodation.DueDate <= nextAssigned.DueDate))
            {
                nextDate = nextAccommodation.DueDate;
                nextAmount = nextAccommodation.Charge.Amount;
            }
            else if (nextAssigned != null)
            {
                nextDate = nextAssigned.DueDate;
                nextAmount = nextAssigned.Balance;
            }

            var year = DateTime.UtcNow.Year;
            var paidYtd = payments
                .Where(p => IsStatus(p.PaymentStatus, "Paid") && p.PaymentDate.Year == year)
                .Sum(p => p.Amount);

            return Ok(new
            {
                totalCharged = total,
                totalCharges = total,
                paidAmount = paid,
                pendingAmount = pending,
                outstandingBalance = outstanding,
                overdueAmount = overdue,
                totalPaidYearToDate = paidYtd,
                accommodationCount = lines.Select(l => l.Charge.AccommodationID)
                    .Concat(assigned.Select(l => l.Charge.AccommodationID)).Distinct().Count(),
                nextPaymentAmount = nextAmount,
                nextPaymentDate = nextDate,
                dueDate = nextDate,
                hasApprovedAccommodation = lines.Count > 0 || assigned.Count > 0
            });
        }

        // =========================================================
        // CHARGES
        // =========================================================

        [HttpGet("charges")]
        public async Task<IActionResult> Charges()
        {
            var user = await GetUserAsync();
            if (user == null) return Unauthorized(new { message = "Please sign in again." });

            var result = new List<(DateTime DueDate, object Item)>();
            foreach (var line in BuildLines(user))
            {
                result.Add((line.DueDate, (object)new
                {
                    chargeType = "Accommodation",
                    accommodationChargeId = line.Charge.AccommodationChargeID,
                    accommodationId = line.Charge.AccommodationID,
                    accommodationName = line.AccommodationName,
                    roomNumber = line.RoomNumber,
                    title = line.Charge.PaymentType?.Name ?? "Unknown",
                    paymentTypeName = line.Charge.PaymentType?.Name ?? "Unknown",
                    description = line.Charge.Description,
                    frequency = line.Charge.Frequency,
                    amount = line.Charge.Amount,
                    dueDate = line.DueDate,
                    status = line.Status,
                    isOverdue = line.IsOverdue,
                    canPay = line.CanPay,
                    lastReference = line.Latest?.ReferenceNumber,
                    amountPaid = 0m,
                    pendingVerification = 0m,
                    balance = line.Charge.Amount
                }));
            }

            foreach (var line in await BuildCustomerChargeLinesAsync(user))
            {
                result.Add((line.DueDate, (object)new
                {
                    chargeType = "Customer",
                    customerChargeId = line.Charge.CustomerChargeID,
                    id = line.Charge.CustomerChargeID,
                    accommodationChargeId = 0,
                    accommodationId = line.Charge.AccommodationID,
                    accommodationName = line.AccommodationName,
                    roomNumber = "",
                    title = line.Charge.Title,
                    paymentType = line.Charge.PaymentType?.Name ?? "Unknown",
                    paymentTypeName = line.Charge.PaymentType?.Name ?? "Unknown",
                    description = line.Charge.Description,
                    frequency = "Individual charge",
                    amount = line.Charge.Amount,
                    dueDate = line.DueDate,
                    status = line.Status,
                    isOverdue = line.IsOverdue,
                    canPay = line.CanPay,
                    lastReference = line.Latest?.ReferenceNumber,
                    amountPaid = line.PaidAmount,
                    pendingVerification = line.PendingAmount,
                    balance = line.Balance
                }));
            }

            return Ok(result.OrderBy(x => x.DueDate).Select(x => x.Item).ToList());
        }

        // =========================================================
        // CHECKOUT
        // =========================================================

        [HttpGet("checkout")]
        public async Task<IActionResult> Checkout(
            [FromQuery] List<int>? ids,
            [FromQuery] List<int>? customerChargeIds)
        {
            var user = await GetUserAsync();
            if (user == null) return Unauthorized(new { message = "Please sign in again." });

            var selectedIds = (ids ?? new List<int>()).Where(i => i > 0).Distinct().ToList();
            var selectedCustomerIds = (customerChargeIds ?? new List<int>()).Where(i => i > 0).Distinct().ToList();
            if (selectedIds.Count == 0 && selectedCustomerIds.Count == 0)
                return BadRequest(new { message = "Select at least one charge to pay." });

            var chosen = ResolveSelection(BuildLines(user), selectedIds);
            var chosenCustomer = ResolveCustomerChargeSelection(
                await BuildCustomerChargeLinesAsync(user), selectedCustomerIds);

            if (chosen == null || chosenCustomer == null)
            {
                return BadRequest(new
                {
                    message = "One or more selected charges are no longer payable. Please review your payment list."
                });
            }

            return Ok(ToCheckoutDto(chosen, chosenCustomer));
        }

        [HttpPost("checkout")]
        public async Task<IActionResult> SubmitCheckout([FromBody] CheckoutRequest request)
        {
            var user = await GetUserAsync();
            if (user == null) return Unauthorized(new { message = "Please sign in again." });

            var ids = (request.SelectedChargeIds ?? new List<int>()).Where(i => i > 0).Distinct().ToList();
            var customerIds = (request.SelectedCustomerChargeIds ?? new List<int>())
                .Where(i => i > 0).Distinct().ToList();

            if (ids.Count == 0 && customerIds.Count == 0)
                return BadRequest(new { message = "Select at least one charge to pay." });

            // Rebuild every amount and relationship from the database; never trust posted totals.
            var chosen = ResolveSelection(BuildLines(user), ids);
            var chosenCustomer = ResolveCustomerChargeSelection(
                await BuildCustomerChargeLinesAsync(user), customerIds);
            if (chosen == null || chosenCustomer == null)
            {
                return BadRequest(new
                {
                    message = "One or more selected charges are no longer payable. Please review your payment list."
                });
            }

            var errors = new Dictionary<string, string[]>();
            var method = (request.PaymentMethod ?? "").Trim();
            var isCard = method.Equals("Card", StringComparison.OrdinalIgnoreCase);
            var isEft = method.Equals("EFT", StringComparison.OrdinalIgnoreCase);
            var cardDigits = new string((request.CardNumber ?? "").Where(char.IsDigit).ToArray());

            if (!isCard && !isEft)
            {
                errors["paymentMethod"] = new[] { "Select a valid payment method." };
            }
            else if (isCard)
            {
                if (string.IsNullOrWhiteSpace(request.CardHolderName))
                    errors["cardHolderName"] = new[] { "Cardholder name is required." };
                if (cardDigits.Length != 16)
                    errors["cardNumber"] = new[] { "Card number must contain exactly 16 digits." };
                if (!TryValidateExpiry(request.ExpiryDate, out var expiryError))
                    errors["expiryDate"] = new[] { expiryError };
                if (!Regex.IsMatch(request.Cvv ?? "", @"^\d{3}$"))
                    errors["cvv"] = new[] { "CVV must contain exactly 3 digits." };
            }
            else
            {
                var eft = (request.EftReference ?? "").Trim().ToUpperInvariant();
                if (string.IsNullOrWhiteSpace(eft))
                    errors["eftReference"] = new[] { "Enter your EFT reference number." };
                else if (!EftPattern.IsMatch(eft))
                    errors["eftReference"] = new[] { "EFT reference must start with T and contain digits only." };
            }

            if (errors.Count > 0)
                return BadRequest(new { message = string.Join(" ", errors.Values.Select(v => v[0])), errors });

            var batch = "BATCH-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") + "-" + Random.Shared.Next(100, 999);
            var last4 = isCard ? cardDigits[^4..] : null;
            var eftRef = (request.EftReference ?? "").Trim().ToUpperInvariant();
            var created = new List<Payment>();
            var now = DateTime.UtcNow;

            try
            {
                foreach (var line in chosen)
                {
                    var reference = isEft
                        ? eftRef + "-AC-" + line.Charge.AccommodationChargeID
                        : "CARD-" + now.ToString("yyyyMMddHHmmssfff") + "-AC-" + line.Charge.AccommodationChargeID;

                    var payment = new Payment
                    {
                        UserID = user.Id,
                        PaymentTypeID = line.Charge.PaymentTypeID,
                        AccommodationChargeID = line.Charge.AccommodationChargeID,
                        CustomerChargeID = null,
                        ApplicationID = line.Application.ApplicationID,
                        DueDate = line.DueDate,
                        Amount = line.Charge.Amount,
                        PaymentStatus = "Pending",
                        ReferenceNumber = reference,
                        PaymentMethod = isCard ? "Card" : "EFT",
                        PaymentBatchReference = batch,
                        PaymentDate = now,
                        Description = isCard ? $"Card payment ending {last4}" : "EFT payment"
                    };
                    _repository.Payment.CreatePayment(payment);
                    created.Add(payment);
                }

                foreach (var line in chosenCustomer)
                {
                    // Recheck the charge under the authenticated customer and prevent duplicate pending payments.
                    var current = await _db.CustomerCharges
                        .Include(c => c.Payments)
                        .FirstOrDefaultAsync(c => c.CustomerChargeID == line.Charge.CustomerChargeID
                                                  && c.UserID == user.Id && c.IsActive);
                    if (current == null)
                        return BadRequest(new { message = "An assigned charge is no longer available. Refresh your payments." });

                    var currentPaid = current.Payments.Where(p => IsStatus(p.PaymentStatus, "Paid")).Sum(p => p.Amount);
                    var currentPending = current.Payments.Where(p => IsStatus(p.PaymentStatus, "Pending")).Sum(p => p.Amount);
                    if (currentPending > 0m || current.Amount - currentPaid <= 0m)
                        return BadRequest(new { message = "This assigned charge is already paid or awaiting verification." });

                    var reference = isEft
                        ? eftRef + "-CC-" + current.CustomerChargeID
                        : "CARD-" + now.ToString("yyyyMMddHHmmssfff") + "-CC-" + current.CustomerChargeID;
                    var payment = new Payment
                    {
                        UserID = user.Id,
                        PaymentTypeID = current.PaymentTypeID,
                        AccommodationChargeID = null,
                        CustomerChargeID = current.CustomerChargeID,
                        ApplicationID = current.ApplicationID,
                        DueDate = current.DueDate.Date,
                        Amount = current.Amount - currentPaid,
                        PaymentStatus = "Pending",
                        ReferenceNumber = reference,
                        PaymentMethod = isCard ? "Card" : "EFT",
                        PaymentBatchReference = batch,
                        PaymentDate = now,
                        Description = (isCard ? $"Card payment ending {last4}" : "EFT payment")
                            + $"; administrator-assigned charge: {current.Title}"
                    };
                    _repository.Payment.CreatePayment(payment);
                    created.Add(payment);
                }

                _repository.Save();
                var totalAmount = chosen.Sum(l => l.Charge.Amount) + chosenCustomer.Sum(l => l.Balance);

                _repository.Notification.CreateNotification(new Notification
                {
                    UserID = user.Id,
                    Title = "Payment Submitted",
                    Message = $"Your payment of R{totalAmount:N2} across your selected charges has been submitted and is awaiting verification.",
                    DateCreated = DateTime.UtcNow
                });
                await NotifyPaymentStaffAsync(totalAmount, user, batch);
                _repository.Save();

                return Ok(new
                {
                    message = $"Payment of R{totalAmount:N2} submitted successfully.",
                    batchReference = batch,
                    totalAmount,
                    status = "Pending",
                    paymentIds = created.Select(p => p.PaymentID).ToList()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to submit payment batch for user {UserId}", user.Id);
                return StatusCode(500, new { message = "The payment could not be processed. Please try again." });
            }
        }

        // =========================================================
        // HISTORY
        // =========================================================

        [HttpGet("history")]
        public async Task<IActionResult> History()
        {
            var user = await GetUserAsync();
            if (user == null) return Unauthorized(new { message = "Please sign in again." });

            var result = _repository.Payment.FindByUser(user.Id)
                .OrderByDescending(p => p.PaymentDate)
                .Select(p => new
                {
                    paymentId = p.PaymentID,
                    paymentTypeName = p.PaymentType?.Name ?? "Unknown",
                    description = p.CustomerChargeID.HasValue
                        ? p.Description
                        : p.PaymentType?.Name ?? p.Description,
                    amount = p.Amount,
                    method = p.PaymentMethod,
                    reference = p.ReferenceNumber,
                    status = p.PaymentStatus,
                    paidAt = AsUtc(p.PaymentDate),
                    dueDate = p.DueDate,
                    canViewReceipt = IsStatus(p.PaymentStatus, "Paid")
                })
                .ToList();

            return Ok(result);
        }

        // =========================================================
        // RECEIPT
        // =========================================================

        [HttpGet("{id:int}/receipt")]
        public async Task<IActionResult> Receipt(int id)
        {
            var user = await GetUserAsync();
            if (user == null) return Unauthorized(new { message = "Please sign in again." });

            var payment = _repository.Payment.GetPaymentById(id);
            if (payment == null || payment.UserID != user.Id)
                return NotFound(new { message = "Receipt not found." });

            var application = _repository.Application
                .FindByUser(user.Id)
                .FirstOrDefault(a => a.ApplicationID == payment.ApplicationID);

            string accommodationName = "";
            string roomNumber = "";

            if (application != null)
            {
                accommodationName = _repository.Accommodation
                    .GetAccommodationById(application.AccommodationID)?.Name ?? "";

                if (application.RoomID.HasValue)
                {
                    roomNumber = _repository.Room.GetRoomById(application.RoomID.Value)?.RoomNumber ?? "";
                }
            }

            return Ok(new
            {
                paymentId = payment.PaymentID,
                status = payment.PaymentStatus,
                amount = payment.Amount,
                paymentTypeName = payment.PaymentType?.Name ?? "Unknown",
                description = payment.Description,
                method = payment.PaymentMethod,
                reference = payment.ReferenceNumber,
                batchReference = payment.PaymentBatchReference,
                paymentDate = AsUtc(payment.PaymentDate),
                dueDate = payment.DueDate,
                accommodationName,
                roomNumber,
                customerName = $"{user.FirstName} {user.Surname}".Trim(),
                canPrint = IsStatus(payment.PaymentStatus, "Paid")
            });
        }

        // =========================================================
        // HELPERS
        // =========================================================

        private async Task<ApplicationUser?> GetUserAsync()
        {
            var id = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
            return id == null
                ? await _userManager.GetUserAsync(User)
                : await _userManager.FindByIdAsync(id);
        }

        private static bool IsStatus(string? value, string expected) =>
            string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);

        private static DateTime AsUtc(DateTime value) =>
            DateTime.SpecifyKind(value, DateTimeKind.Utc);

        /// <summary>One line per active charge on every approved accommodation (same logic as the web Index).</summary>
        private List<ChargeLine> BuildLines(ApplicationUser user)
        {
            var approved = _repository.Application
                .FindByUser(user.Id)
                .Where(a => IsStatus(a.Status, "Approved"))
                .GroupBy(a => a.AccommodationID)
                .Select(g => g.First())
                .ToList();

            var allPayments = _repository.Payment.FindByUser(user.Id).ToList();
            var today = DateTime.UtcNow.Date;
            var lines = new List<ChargeLine>();

            foreach (var application in approved)
            {
                var accommodation = _repository.Accommodation
                    .GetAccommodationById(application.AccommodationID);
                if (accommodation == null) continue;

                var room = application.RoomID.HasValue
                    ? _repository.Room.GetRoomById(application.RoomID.Value)
                    : null;

                var charges = _repository.AccommodationCharge
                    .FindByAccommodation(application.AccommodationID)
                    .Where(c => c.IsActive)
                    .ToList();

                foreach (var charge in charges)
                {
                    var dueDate = PaymentDueDateCalculator.GetCurrentCycleDueDate(charge, today);

                    var latest = allPayments
                        .Where(p =>
                            p.ApplicationID == application.ApplicationID &&
                            p.AccommodationChargeID == charge.AccommodationChargeID &&
                            p.DueDate.HasValue &&
                            p.DueDate.Value.Date == dueDate.Date)
                        .OrderByDescending(p => p.PaymentDate)
                        .FirstOrDefault();

                    // A charge can be paid when nothing was submitted, or the last attempt was rejected.
                    var canPay = latest == null || IsStatus(latest.PaymentStatus, "Rejected");

                    lines.Add(new ChargeLine
                    {
                        Charge = charge,
                        Application = application,
                        AccommodationName = accommodation.Name,
                        RoomNumber = room?.RoomNumber ?? "Not assigned",
                        DueDate = dueDate,
                        Latest = latest,
                        Status = latest?.PaymentStatus ?? (dueDate.Date < today ? "Overdue" : "Unpaid"),
                        CanPay = canPay,
                        IsOverdue = canPay && dueDate.Date < today
                    });
                }
            }

            return lines;
        }

        /// <summary>Loads active individual charges assigned to this customer and computes balances from saved payments.</summary>
        private async Task<List<CustomerChargeLine>> BuildCustomerChargeLinesAsync(ApplicationUser user)
        {
            var charges = await _db.CustomerCharges.AsNoTracking()
                .Include(c => c.PaymentType)
                .Include(c => c.Accommodation)
                .Include(c => c.Payments)
                .Where(c => c.UserID == user.Id && c.IsActive)
                .ToListAsync();

            var today = DateTime.UtcNow.Date;
            return charges.Select(c =>
            {
                var paid = c.Payments.Where(p => IsStatus(p.PaymentStatus, "Paid")).Sum(p => p.Amount);
                var pending = c.Payments.Where(p => IsStatus(p.PaymentStatus, "Pending")).Sum(p => p.Amount);
                var balance = Math.Max(0m, c.Amount - paid - pending);
                var canPay = pending <= 0m && c.Amount - paid > 0m;
                var status = paid >= c.Amount ? "Paid"
                    : pending > 0m ? "Pending verification"
                    : c.DueDate.Date < today ? "Overdue" : "Unpaid";
                var latest = c.Payments.OrderByDescending(p => p.PaymentDate).FirstOrDefault();

                return new CustomerChargeLine
                {
                    Charge = c,
                    AccommodationName = c.Accommodation?.Name ?? "Accommodation",
                    PaidAmount = paid,
                    PendingAmount = pending,
                    Balance = balance,
                    DueDate = c.DueDate,
                    Latest = latest,
                    Status = status,
                    CanPay = canPay,
                    IsOverdue = canPay && c.DueDate.Date < today
                };
            }).ToList();
        }

        /// <summary>Returns selected accommodation lines, or null when any is missing or not payable.</summary>
        private static List<ChargeLine>? ResolveSelection(List<ChargeLine> lines, List<int> ids)
        {
            var chosen = lines.Where(l => ids.Contains(l.Charge.AccommodationChargeID))
                .GroupBy(l => l.Charge.AccommodationChargeID).Select(g => g.First()).ToList();
            if (chosen.Count != ids.Count || chosen.Any(l => !l.CanPay)) return null;
            return chosen;
        }

        /// <summary>Returns selected administrator-assigned charges, or null when missing or not payable.</summary>
        private static List<CustomerChargeLine>? ResolveCustomerChargeSelection(
            List<CustomerChargeLine> lines, List<int> ids)
        {
            var chosen = lines.Where(l => ids.Contains(l.Charge.CustomerChargeID))
                .GroupBy(l => l.Charge.CustomerChargeID).Select(g => g.First()).ToList();
            if (chosen.Count != ids.Count || chosen.Any(l => !l.CanPay || l.Balance <= 0m)) return null;
            return chosen;
        }

        private static object ToCheckoutDto(List<ChargeLine> chosen, List<CustomerChargeLine> chosenCustomer)
        {
            var accommodationNames = chosen.Select(l => l.AccommodationName)
                .Concat(chosenCustomer.Select(l => l.AccommodationName)).Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct().ToList();
            var roomNames = chosen.Select(l => l.RoomNumber).Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct().ToList();

            var items = new List<object>();
            items.AddRange(chosen.Select(l => (object)new
            {
                chargeType = "Accommodation",
                accommodationChargeId = l.Charge.AccommodationChargeID,
                customerChargeId = (int?)null,
                title = l.Charge.PaymentType?.Name ?? "Unknown",
                paymentTypeName = l.Charge.PaymentType?.Name ?? "Unknown",
                frequency = l.Charge.Frequency,
                description = l.Charge.Description,
                accommodationName = l.AccommodationName,
                amount = l.Charge.Amount,
                dueDate = l.DueDate
            }));
            items.AddRange(chosenCustomer.Select(l => (object)new
            {
                chargeType = "Customer",
                accommodationChargeId = (int?)null,
                customerChargeId = l.Charge.CustomerChargeID,
                title = l.Charge.Title,
                paymentTypeName = l.Charge.PaymentType?.Name ?? "Unknown",
                frequency = "Individual charge",
                description = l.Charge.Description,
                accommodationName = l.AccommodationName,
                amount = l.Balance,
                dueDate = l.DueDate
            }));

            return new
            {
                selectedChargeIds = chosen.Select(l => l.Charge.AccommodationChargeID).ToList(),
                selectedCustomerChargeIds = chosenCustomer.Select(l => l.Charge.CustomerChargeID).ToList(),
                accommodationName = accommodationNames.Count == 0 ? "" : accommodationNames.Count == 1 ? accommodationNames[0] : "Multiple accommodations",
                roomNumber = roomNames.Count == 0 ? "Not assigned" : roomNames.Count == 1 ? roomNames[0] : "Multiple rooms",
                totalAmount = chosen.Sum(l => l.Charge.Amount) + chosenCustomer.Sum(l => l.Balance),
                items
            };
        }

        private static bool TryValidateExpiry(string? value, out string error)
        {
            error = "Expiry date must be in MM/YY format, for example 05/28.";

            if (string.IsNullOrWhiteSpace(value) ||
                !Regex.IsMatch(value.Trim(), @"^(0[1-9]|1[0-2])/\d{2}$"))
                return false;

            var parts = value.Trim().Split('/');
            var month = int.Parse(parts[0], CultureInfo.InvariantCulture);
            var year = 2000 + int.Parse(parts[1], CultureInfo.InvariantCulture);

            var lastDay = new DateTime(year, month, DateTime.DaysInMonth(year, month));
            if (lastDay < DateTime.UtcNow.Date)
            {
                error = "This card has expired.";
                return false;
            }

            return true;
        }

        private async Task NotifyPaymentStaffAsync(decimal total, ApplicationUser customer, string batchReference)
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
    }
}
