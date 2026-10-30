using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UFSStayConnect.DataAccess;
using UFSStayConnect.Models;
using UFSStayConnect.ViewModels;

namespace UFSStayConnect.Controllers
{
    [Authorize(Roles = "Admin")]
    public class RoomController : Controller
    {
        private readonly IRepositoryWrapper _repository;
        private readonly ILogger<RoomController> _logger;
        public RoomController(IRepositoryWrapper repository, ILogger<RoomController> logger) { _repository = repository; _logger = logger; }

        [HttpGet]
        public IActionResult Manage(int accommodationId)
        {
            var accommodation = _repository.Accommodation.GetAccommodationById(accommodationId);
            if (accommodation == null) return NotFound();
            ViewBag.Accommodation = accommodation;
            return View(_repository.Room.FindByAccommodation(accommodationId).ToList());
        }

        [HttpGet]
        public IActionResult Create(int accommodationId)
        {
            var accommodation = _repository.Accommodation.GetAccommodationById(accommodationId);
            if (accommodation == null) return NotFound();
            ViewBag.Accommodation = accommodation;
            return View(new RoomViewModel { AccommodationID = accommodationId });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult Create(RoomViewModel model)
        {
            var accommodation = _repository.Accommodation.GetAccommodationById(model.AccommodationID);
            if (accommodation == null) return NotFound();
            if (_repository.Room.FindByAccommodation(model.AccommodationID).Any(r => r.RoomNumber.Equals(model.RoomNumber.Trim(), StringComparison.OrdinalIgnoreCase)))
                ModelState.AddModelError(nameof(model.RoomNumber), "That room number already exists for this residence.");
            if (!ModelState.IsValid) { ViewBag.Accommodation = accommodation; return View(model); }
            _repository.Room.CreateRoom(new Room { AccommodationID = model.AccommodationID, RoomNumber = model.RoomNumber.Trim(), Floor = model.Floor?.Trim(), RoomType = model.RoomType.Trim(), Status = model.Status, Description = model.Description?.Trim() });
            SyncAvailability(model.AccommodationID);
            _repository.Save();
            TempData["SuccessMessage"] = "Room added successfully.";
            return RedirectToAction(nameof(Manage), new { accommodationId = model.AccommodationID });
        }

        [HttpGet]
        public IActionResult Edit(int id)
        {
            var room = _repository.Room.GetRoomById(id);
            if (room == null) return NotFound();
            ViewBag.Accommodation = room.Accommodation;
            return View(new RoomViewModel { RoomID = room.RoomID, AccommodationID = room.AccommodationID, RoomNumber = room.RoomNumber, Floor = room.Floor, RoomType = room.RoomType, Status = room.Status, Description = room.Description });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult Edit(RoomViewModel model)
        {
            var room = _repository.Room.GetRoomById(model.RoomID);
            if (room == null) return NotFound();
            var accommodation = _repository.Accommodation.GetAccommodationById(room.AccommodationID);
            if (_repository.Room.FindByAccommodation(room.AccommodationID).Any(r => r.RoomID != room.RoomID && r.RoomNumber.Equals(model.RoomNumber.Trim(), StringComparison.OrdinalIgnoreCase)))
                ModelState.AddModelError(nameof(model.RoomNumber), "That room number already exists for this residence.");
            if (!ModelState.IsValid) { ViewBag.Accommodation = accommodation; return View(model); }
            room.RoomNumber = model.RoomNumber.Trim(); room.Floor = model.Floor?.Trim(); room.RoomType = model.RoomType.Trim();
            if (room.Status != "Occupied") room.Status = model.Status;
            room.Description = model.Description?.Trim();
            _repository.Room.UpdateRoom(room); SyncAvailability(room.AccommodationID); _repository.Save();
            TempData["SuccessMessage"] = "Room updated successfully.";
            return RedirectToAction(nameof(Manage), new { accommodationId = room.AccommodationID });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult Delete(int id)
        {
            var room = _repository.Room.GetRoomById(id); if (room == null) return NotFound();
            if (room.Status == "Occupied") { TempData["ErrorMessage"] = "An occupied room cannot be deleted."; return RedirectToAction(nameof(Manage), new { accommodationId = room.AccommodationID }); }
            var accommodationId = room.AccommodationID; _repository.Room.DeleteRoom(room); _repository.Save(); SyncAvailability(accommodationId); _repository.Save();
            TempData["SuccessMessage"] = "Room deleted successfully."; return RedirectToAction(nameof(Manage), new { accommodationId });
        }

        private void SyncAvailability(int accommodationId)
        {
            var accommodation = _repository.Accommodation.GetAccommodationById(accommodationId);
            if (accommodation == null) return;
            var rooms = _repository.Room.FindByAccommodation(accommodationId).ToList();
            if (rooms.Count > 0)
            {
                accommodation.TotalRooms = rooms.Count;
                accommodation.AvailableRooms = rooms.Count(r => r.Status == "Available");
                accommodation.Available = accommodation.AvailableRooms > 0;
                _repository.Accommodation.UpdateAccommodation(accommodation);
            }
        }
    }
}
