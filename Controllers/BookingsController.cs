using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MeetingRoomWeb.Data;
using MeetingRoomWeb.Models;

namespace MeetingRoomWeb.Controllers
{
    [Authorize]
    public class BookingsController : Controller
    {
        private readonly AppDbContext _context;

        public BookingsController(AppDbContext context)
        {
            _context = context;
        }

        // GET: Bookings
        public async Task<IActionResult> Index()
        {
            var appDbContext = _context.Bookings.Include(b => b.Room).Include(b => b.User);
            return View(await appDbContext.ToListAsync());
        }

        // GET: Bookings/Details/5
        public async Task<IActionResult> Details(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var booking = await _context.Bookings
                .Include(b => b.Room)
                .Include(b => b.User)
                .Include(b => b.Documents)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (booking == null)
            {
                return NotFound();
            }

            return View(booking);
        }

        // GET: Bookings/Create
        public IActionResult Create()
        {
            ViewData["RoomId"] = new SelectList(_context.MeetingRooms, "Id", "Name");
            ViewData["UserId"] = new SelectList(_context.Users, "Id", "Email");
            return View();
        }

        // POST: Bookings/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,RoomId,UserId,Title,StartTime,EndTime,Description,Status")] Booking booking, IFormFile? document)
        {
            if (ModelState.IsValid)
            {
                booking.Id = Guid.NewGuid();
                _context.Add(booking);

                // Handle file upload
                if (document != null && document.Length > 0)
                {
                    var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
                    if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);
                    
                    var fileName = Guid.NewGuid().ToString() + "_" + document.FileName;
                    var filePath = Path.Combine(uploadsFolder, fileName);
                    
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await document.CopyToAsync(stream);
                    }
                    
                    var doc = new Document
                    {
                        Id = Guid.NewGuid(),
                        BookingId = booking.Id,
                        FileName = document.FileName,
                        FilePath = "/uploads/" + fileName,
                        UploadedAt = DateTime.Now
                    };
                    _context.Documents.Add(doc);
                }

                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["RoomId"] = new SelectList(_context.MeetingRooms, "Id", "Name", booking.RoomId);
            ViewData["UserId"] = new SelectList(_context.Users, "Id", "Email", booking.UserId);
            return View(booking);
        }

        // GET: Bookings/Edit/5
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var booking = await _context.Bookings.FindAsync(id);
            if (booking == null)
            {
                return NotFound();
            }
            ViewData["RoomId"] = new SelectList(_context.MeetingRooms, "Id", "Name", booking.RoomId);
            ViewData["UserId"] = new SelectList(_context.Users, "Id", "Email", booking.UserId);
            return View(booking);
        }

        // POST: Bookings/Edit/5
        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, [Bind("Id,RoomId,UserId,Title,StartTime,EndTime,Description,Status")] Booking booking)
        {
            if (id != booking.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                _context.Update(booking);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["RoomId"] = new SelectList(_context.MeetingRooms, "Id", "Name", booking.RoomId);
            ViewData["UserId"] = new SelectList(_context.Users, "Id", "Email", booking.UserId);
            return View(booking);
        }

        // GET: Bookings/Delete/5
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(Guid? id)
        {
            if (id == null) return NotFound();
            var booking = await _context.Bookings.Include(b => b.Room).Include(b => b.User).FirstOrDefaultAsync(m => m.Id == id);
            return booking == null ? NotFound() : View(booking);
        }

        // POST: Bookings/Delete/5
        [Authorize(Roles = "Admin")]
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var booking = await _context.Bookings.FindAsync(id);
            if (booking != null)
            {
                _context.Bookings.Remove(booking);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool BookingExists(Guid id)
        {
            return _context.Bookings.Any(e => e.Id == id);
        }
    }
}
