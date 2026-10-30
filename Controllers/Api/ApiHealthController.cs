
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UFSStayConnect.Data;

namespace UFS_StayConnect.Controllers.Api
{
    [Route("api/[controller]")]
    [ApiController]
    public class ApiHealthController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ApiHealthController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetHealth()
        {
            var databaseConnected =
                await _context.Database.CanConnectAsync();

            if (!databaseConnected)
            {
                return StatusCode(503, new
                {
                    status = "error",
                    message = "The application database is unavailable."
                });
            }

            return Ok(new
            {
                status = "healthy",
                message = "UFSStayConnect API is running.",
                database = "connected"
            });
        }
    }
}
