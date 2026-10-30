using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UFSStayConnect.DataAccess;
using UFSStayConnect.Models;
using UFSStayConnect.ViewModels;

namespace UFSStayConnect.Controllers;

[Authorize(Roles = "Admin")]
public class ServiceController : Controller
{
    private readonly IRepositoryWrapper _repository;

    public ServiceController(IRepositoryWrapper repository)
    {
        _repository = repository;
    }

    // GET: /Service
    [HttpGet]
    public IActionResult Index()
    {
        return View(_repository.Service.FindAllServices().ToList());
    }

    // GET: /Service/Create
    [HttpGet]
    public IActionResult Create()
    {
        return View(new Service());
    }

    // POST: /Service/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(Service model)
    {
        Normalize(model);

        if (!ModelState.IsValid)
            return View(model);

        var duplicate = _repository.Service.FindAllServices()
            .Any(s => s.ServiceName.Equals(
                model.ServiceName,
                StringComparison.OrdinalIgnoreCase));

        if (duplicate)
        {
            ModelState.AddModelError(
                nameof(model.ServiceName),
                "A service with this name already exists.");

            return View(model);
        }

        _repository.Service.CreateService(model);
        _repository.Save();

        TempData["SuccessMessage"] = "Service created successfully.";

        return RedirectToAction(nameof(Index));
    }

    // GET: /Service/Edit/5
    [HttpGet]
    public IActionResult Edit(int id)
    {
        var service = _repository.Service.GetServiceById(id);

        if (service == null)
            return NotFound();

        return View(service);
    }

    // POST: /Service/Edit
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(Service model)
    {
        Normalize(model);

        if (!ModelState.IsValid)
            return View(model);

        var service = _repository.Service.GetServiceById(model.ServiceID);

        if (service == null)
            return NotFound();

        var duplicate = _repository.Service.FindAllServices().Any(s =>
            s.ServiceID != model.ServiceID &&
            s.ServiceName.Equals(
                model.ServiceName,
                StringComparison.OrdinalIgnoreCase));

        if (duplicate)
        {
            ModelState.AddModelError(
                nameof(model.ServiceName),
                "A service with this name already exists.");

            return View(model);
        }

        service.ServiceName = model.ServiceName;
        service.Description = model.Description;
        service.RatePerUnit = model.RatePerUnit;
        service.ChargingMethod = model.ChargingMethod;
        service.UnitOfMeasure = model.UnitOfMeasure;
        service.IsActive = model.IsActive;

        _repository.Service.UpdateService(service);
        _repository.Save();

        TempData["SuccessMessage"] = "Service updated successfully.";

        return RedirectToAction(nameof(Index));
    }

    // GET: /Service/Manage?accommodationId=1
    [HttpGet]
    public IActionResult Manage(int accommodationId)
    {
        return RedirectToAction(
            nameof(Assign),
            new { accommodationId });
    }

    // GET: /ServiceUsage?accommodationId=1
    [HttpGet("/ServiceUsage")]
    public IActionResult ServiceUsage(int accommodationId)
    {
        return RedirectToAction(
            nameof(Assign),
            new { accommodationId });
    }

    // GET: /Service/Assign?accommodationId=1
    [HttpGet]
    public IActionResult Assign(int accommodationId)
    {
        var accommodation =
            _repository.Accommodation.GetAccommodationById(accommodationId);

        if (accommodation == null)
            return NotFound("Accommodation not found.");

        var assignments = _repository.ServiceAssignment
            .FindByAccommodation(accommodationId)
            .ToList()
            .GroupBy(x => x.ServiceID)
            .ToDictionary(g => g.Key, g => g.First());

        var services = _repository.Service
            .FindAllServices()
            .ToList();

        var model = new ServiceAssignmentViewModel
        {
            AccommodationID = accommodationId,
            AccommodationName = accommodation.Name,

            Services = services.Select(service =>
            {
                assignments.TryGetValue(
                    service.ServiceID,
                    out var assignment);

                return new ServiceSelectionViewModel
                {
                    ServiceID = service.ServiceID,
                    ServiceName = service.ServiceName,
                    Description = service.Description,

                    Selected = assignment != null,

                    RatePerUnit = assignment?.RatePerUnit
                        ?? service.RatePerUnit,

                    // Always derive the unit from the service name.
                    UnitOfMeasure = GetDefaultUnit(service.ServiceName),

                    // First car is free for parking by default.
                    FreeUnits = assignment?.FreeUnits
                        ?? (IsParking(service.ServiceName) ? 1 : 0),

                    // All accommodation services use usage-based charging.
                    IsPayAsYouGo = true,

                    IsEnabled = assignment?.IsEnabled ?? true
                };
            }).ToList()
        };

        return View(model);
    }

    // POST: /Service/Assign
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Assign(ServiceAssignmentViewModel model)
    {
        var accommodation =
            _repository.Accommodation.GetAccommodationById(
                model.AccommodationID);

        if (accommodation == null)
            return NotFound("Accommodation not found.");

        if (model.Services == null)
        {
            ModelState.AddModelError(
                string.Empty,
                "No services were submitted.");

            return AssignWithErrors(model);
        }

        var allServices = _repository.Service
            .FindAllServices()
            .ToDictionary(s => s.ServiceID);

        // Prevent a client from submitting duplicate service IDs.
        if (model.Services
            .GroupBy(s => s.ServiceID)
            .Any(g => g.Count() > 1))
        {
            ModelState.AddModelError(
                string.Empty,
                "A service was submitted more than once.");

            return AssignWithErrors(model);
        }

        // Units and service details are controlled by the server.
        // They are not accepted from user-entered form values.
        for (var i = 0; i < model.Services.Count; i++)
        {
            var input = model.Services[i];

            // Remove validation errors caused by the unit not being
            // entered in the form.
            ModelState.Remove($"Services[{i}].UnitOfMeasure");
            ModelState.Remove($"Services[{i}].ServiceName");
            ModelState.Remove($"Services[{i}].Description");
            ModelState.Remove($"Services[{i}].IsPayAsYouGo");

            if (!allServices.TryGetValue(input.ServiceID, out var service))
            {
                if (input.Selected)
                {
                    ModelState.AddModelError(
                        string.Empty,
                        "An invalid service was selected.");
                }

                continue;
            }

            // Use the actual database service name.
            input.ServiceName = service.ServiceName;
            input.Description = service.Description;

            // Never require the administrator to enter the unit.
            input.UnitOfMeasure = GetDefaultUnit(service.ServiceName);

            // All services are pay-as-you-go.
            input.IsPayAsYouGo = true;

            if (!input.Selected)
                continue;

            if (input.RatePerUnit < 0)
            {
                ModelState.AddModelError(
                    $"Services[{i}].RatePerUnit",
                    "The rate cannot be negative.");
            }

            if (input.FreeUnits < 0 || input.FreeUnits > 1000)
            {
                ModelState.AddModelError(
                    $"Services[{i}].FreeUnits",
                    "Free units must be between 0 and 1000.");
            }
        }

        if (!ModelState.IsValid)
            return AssignWithErrors(model);

        var selectedServices = model.Services
            .Where(s => s.Selected)
            .ToDictionary(s => s.ServiceID);

        var existingAssignments = _repository.ServiceAssignment
            .FindByAccommodation(model.AccommodationID)
            .ToList();

        // Update selected existing assignments and remove unselected ones.
        foreach (var existing in existingAssignments)
        {
            if (!selectedServices.TryGetValue(
                    existing.ServiceID,
                    out var input))
            {
                _repository.ServiceAssignment.Remove(existing);
                continue;
            }

            var serviceDefinition = allServices[existing.ServiceID];

            existing.RatePerUnit = input.RatePerUnit;
            existing.UnitOfMeasure =
                GetDefaultUnit(serviceDefinition.ServiceName);
            existing.FreeUnits = input.FreeUnits;
            existing.IsPayAsYouGo = true;
            existing.IsEnabled = input.IsEnabled;
        }

        // Add selected services that have not already been assigned.
        var existingServiceIds = existingAssignments
            .Select(x => x.ServiceID)
            .ToHashSet();

        foreach (var input in selectedServices.Values.Where(
                     s => !existingServiceIds.Contains(s.ServiceID)))
        {
            var serviceDefinition = allServices[input.ServiceID];

            var assignment = new AccommodationService
            {
                AccommodationID = model.AccommodationID,
                ServiceID = input.ServiceID,
                RatePerUnit = input.RatePerUnit,
                UnitOfMeasure =
                    GetDefaultUnit(serviceDefinition.ServiceName),
                FreeUnits = input.FreeUnits,
                IsPayAsYouGo = true,
                IsEnabled = input.IsEnabled
            };

            _repository.ServiceAssignment.Add(assignment);
        }

        _repository.Save();

        TempData["SuccessMessage"] =
            "Residence services and rates saved successfully.";

        return RedirectToAction(
            nameof(Assign),
            new { accommodationId = model.AccommodationID });
    }

    // Automatically determine a service's unit of measure.
    private static string GetDefaultUnit(string? serviceName)
    {
        if (string.IsNullOrWhiteSpace(serviceName))
            return "unit";

        var name = serviceName.Trim().ToLowerInvariant();

        if (name.Contains("parking"))
            return "car";

        if (name.Contains("electricity"))
            return "kWh";

        if (name.Contains("water"))
            return "kL";

        if (name.Contains("wi-fi") ||
            name.Contains("wifi") ||
            name.Contains("internet"))
            return "GB";

        if (name.Contains("laundry"))
            return "load";

        if (name.Contains("security"))
            return "month";

        if (name.Contains("refuse") ||
            name.Contains("waste") ||
            name.Contains("garbage"))
            return "collection";

        return "unit";
    }

    private static bool IsParking(string? serviceName)
    {
        return !string.IsNullOrWhiteSpace(serviceName) &&
               serviceName.Contains(
                   "parking",
                   StringComparison.OrdinalIgnoreCase);
    }

    // Restore service details if validation fails.
    private IActionResult AssignWithErrors(
        ServiceAssignmentViewModel model)
    {
        var accommodation =
            _repository.Accommodation.GetAccommodationById(
                model.AccommodationID);

        if (accommodation == null)
            return NotFound("Accommodation not found.");

        model.AccommodationName = accommodation.Name;

        var services = _repository.Service
            .FindAllServices()
            .ToDictionary(s => s.ServiceID);

        if (model.Services != null)
        {
            for (var i = 0; i < model.Services.Count; i++)
            {
                var input = model.Services[i];

                if (!services.TryGetValue(input.ServiceID, out var service))
                    continue;

                input.ServiceName = service.ServiceName;
                input.Description = service.Description;
                input.UnitOfMeasure = GetDefaultUnit(service.ServiceName);
                input.IsPayAsYouGo = true;
            }
        }

        return View("Assign", model);
    }

    private static void Normalize(Service model)
    {
        model.ServiceName = model.ServiceName?.Trim() ?? "";
        model.Description = model.Description?.Trim();
        model.UnitOfMeasure =
            model.UnitOfMeasure?.Trim() ?? "unit";

        model.ChargingMethod =
            model.ChargingMethod?.Trim() ?? "PayAsYouGo";
    }
}