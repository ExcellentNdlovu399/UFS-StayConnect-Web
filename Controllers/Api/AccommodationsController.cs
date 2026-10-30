using System.Linq.Expressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UFSStayConnect.Data;
using UFSStayConnect.Models;

namespace UFSStayConnect.Controllers.Api
{
    [AllowAnonymous]
    [Route("api/[controller]")]
    [ApiController]
    public class AccommodationsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AccommodationsController(AppDbContext context)
        {
            _context = context;
        }

        // One projection shared by every endpoint, so the JSON shape is identical
        // everywhere. Property names become camelCase in the JSON
        // (id, name, monthlyRent, services ...), which is what the Android app reads.
        private static readonly Expression<Func<Accommodation, AccommodationDto>> ToDto =
            a => new AccommodationDto
            {
                Id = a.AccommodationID,
                Name = a.Name,
                Location = a.Location,
                Type = a.Type,
                Description = a.Description,
                MonthlyRent = a.MonthlyRent,
                Bedrooms = a.Bedrooms,
                Bathrooms = a.Bathrooms,
                TotalRooms = a.TotalRooms,
                AvailableRooms = a.AvailableRooms,
                ImagePath = a.ImagePath,
                Available = a.Available,
                Services = a.AccommodationServices
                    .Select(s => new ServiceDto
                    {
                        Id = s.ServiceID,
                        Name = s.Service.ServiceName
                    })
                    .ToList()
            };

        // GET: api/Accommodations
        [HttpGet]
        public async Task<IActionResult> GetAccommodations()
        {
            var accommodations = await _context.Accommodations
                .AsNoTracking()
                .Select(ToDto)
                .ToListAsync();

            return Ok(accommodations);
        }

        // GET: api/Accommodations/services
        [HttpGet("services")]
        public async Task<IActionResult> GetServices()
        {
            var services = await _context.Services
                .AsNoTracking()
                .OrderBy(s => s.ServiceName)
                .Select(s => new ServiceDto
                {
                    Id = s.ServiceID,
                    Name = s.ServiceName
                })
                .ToListAsync();

            return Ok(services);
        }

        // GET: api/Accommodations/types
        [HttpGet("types")]
        public IActionResult GetTypes()
        {
            // Matches the validation in the MVC AccommodationController.
            return Ok(new[] { "Flat", "Townhouse" });
        }

        // GET: api/Accommodations/2
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetAccommodation(int id)
        {
            var accommodation = await _context.Accommodations
                .AsNoTracking()
                .Where(a => a.AccommodationID == id)
                .Select(ToDto)
                .FirstOrDefaultAsync();

            if (accommodation == null)
            {
                return NotFound(new { message = "Accommodation not found." });
            }

            return Ok(accommodation);
        }

        // GET: api/Accommodations/filter?location=West&type=Flat&serviceId=1
        [HttpGet("filter")]
        public async Task<IActionResult> FilterAccommodations(
            [FromQuery] string? location,
            [FromQuery] string? type,
            [FromQuery] int? serviceId,
            [FromQuery] bool availableOnly = true)
        {
            var query = _context.Accommodations
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(location))
            {
                query = query.Where(a => a.Location.Contains(location));
            }

            if (!string.IsNullOrWhiteSpace(type))
            {
                query = query.Where(a => a.Type == type);
            }

            if (serviceId.HasValue)
            {
                query = query.Where(a =>
                    a.AccommodationServices.Any(s => s.ServiceID == serviceId.Value));
            }

            if (availableOnly)
            {
                query = query.Where(a => a.Available && a.AvailableRooms > 0);
            }

            var accommodations = await query
                .Select(ToDto)
                .ToListAsync();

            return Ok(accommodations);
        }
    }

    public class ServiceDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class AccommodationDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal MonthlyRent { get; set; }
        public int Bedrooms { get; set; }
        public int Bathrooms { get; set; }
        public int TotalRooms { get; set; }
        public int AvailableRooms { get; set; }
        public string? ImagePath { get; set; }
        public bool Available { get; set; }
        public List<ServiceDto> Services { get; set; } = new();
    }
}
