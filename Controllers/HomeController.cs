using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using MeetingRoomWeb.Data;
using MeetingRoomWeb.Models;
using System.Diagnostics;
using BC = BCrypt.Net.BCrypt;

namespace MeetingRoomWeb.Controllers;

public class HomeController : Controller
{
    private readonly AppDbContext _context;
    private readonly ILogger<HomeController> _logger;

    public HomeController(AppDbContext context, ILogger<HomeController> logger)
    {
        _context = context;
        _logger  = logger;
    }

    // ─── GET: Index (main dashboard) ───────────────────────────────────────────
    public async Task<IActionResult> Index(string period = "week", DateTime? date = null)
    {
        var filterDate = date ?? DateTime.Today;
        var isAdmin = User.IsInRole("Admin");

        // Compute range for RoomUsageBookings based on filter
        DateTime rangeStart, rangeEnd;
        switch (period)
        {
            case "day":
                rangeStart = filterDate.Date;
                rangeEnd   = rangeStart.AddDays(1);
                break;
            case "month":
                rangeStart = new DateTime(filterDate.Year, filterDate.Month, 1);
                rangeEnd   = rangeStart.AddMonths(1);
                break;
            default: // week
                int diff = (int)filterDate.DayOfWeek - (int)DayOfWeek.Monday;
                if (diff < 0) diff += 7;
                rangeStart = filterDate.AddDays(-diff).Date;
                rangeEnd   = rangeStart.AddDays(7);
                break;
        }

        var viewModel = new HomeViewModel
        {
            UpcomingBookings = await _context.Bookings
                .Include(b => b.Room)
                .Include(b => b.User)
                .Include(b => b.Documents)
                .Where(b => b.StartTime >= DateTime.Now && (b.Status == "Approved" || b.Status == "Rejected"))
                .OrderBy(b => b.StartTime)
                .Take(10)
                .ToListAsync(),

            Rooms = await _context.MeetingRooms.ToListAsync(),

            RoomUsageBookings = await _context.Bookings
                .Include(b => b.Room)
                .Include(b => b.User)
                .Where(b => b.StartTime >= rangeStart && b.StartTime < rangeEnd)
                .OrderBy(b => b.StartTime)
                .ToListAsync(),

            AllBookings = await _context.Bookings
                .Include(b => b.Room)
                .Include(b => b.User)
                .Include(b => b.Documents)
                .OrderByDescending(b => b.StartTime)
                .ToListAsync(),

            Users = await _context.Users.ToListAsync(),

            IsLoggedIn = User.Identity?.IsAuthenticated == true,
            IsAdmin    = isAdmin,
            CurrentUserName  = User.Identity?.Name ?? "",
            CurrentUserEmail = User.FindFirst(ClaimTypes.Email)?.Value ?? "",
            CurrentUserId    = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "",

            FilterPeriod = period,
            FilterDate   = filterDate,
        };

        return View(viewModel);
    }

    // ─── POST: Login ───────────────────────────────────────────────────────────
    [HttpPost][ValidateAntiForgeryToken][EnableRateLimiting("auth")]
    public async Task<IActionResult> Login(string email, string password)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == email);

        if (user == null || !VerifyPassword(password, user.PasswordHash))
        {
            _logger.LogWarning("AUDIT_LOGIN_FAILED email={Email} ip={IP}", email, ip);
            TempData["LoginError"] = "Email hoặc mật khẩu không đúng.";
            return RedirectToAction("Index");
        }

        // Migrate SHA-256 → BCrypt if still using old hash
        if (user.PasswordHash.Length == 64 && !user.PasswordHash.StartsWith("$2"))
        {
            user.PasswordHash = BC.HashPassword(password, workFactor: 12);
            await _context.SaveChangesAsync();
            _logger.LogInformation("AUDIT_HASH_MIGRATED userId={Id} email={Email}", user.Id, user.Email);
        }

        _logger.LogInformation("AUDIT_LOGIN_SUCCESS userId={Id} email={Email} ip={IP}", user.Id, user.Email, ip);
        await SignInUser(user);
        return RedirectToAction("Index");
    }

    // ─── POST: Register ────────────────────────────────────────────────────────
    [HttpPost][ValidateAntiForgeryToken][EnableRateLimiting("auth")]
    public async Task<IActionResult> Register(string fullName, string email, string username, string password)
    {
        if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            TempData["RegisterError"] = "Vui lòng điền đầy đủ thông tin.";
            TempData["ShowRegister"] = "1";
            return RedirectToAction("Index");
        }

        if (password.Length < 8)
        {
            TempData["RegisterError"] = "Mật khẩu phải có ít nhất 8 ký tự.";
            TempData["ShowRegister"] = "1";
            return RedirectToAction("Index");
        }

        if (await _context.Users.AnyAsync(u => u.Email == email))
        {
            TempData["RegisterError"] = "Email này đã được đăng ký.";
            TempData["ShowRegister"] = "1";
            return RedirectToAction("Index");
        }

        var user = new User
        {
            Id           = Guid.NewGuid(),
            FullName     = fullName,
            Email        = email,
            Username     = username,
            PasswordHash = BC.HashPassword(password, workFactor: 12),
            Role         = "Employee"
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        await SignInUser(user);
        TempData["Success"] = "Đăng ký thành công! Chào mừng " + user.FullName;
        return RedirectToAction("Index");
    }

    // ─── POST: Logout ──────────────────────────────────────────────────────────
    [HttpPost][ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index");
    }

    // ─── POST: ForgotPassword (Step 1 - verify identity) ──────────────────────
    [HttpPost][ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(string email, string username)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == email && u.Username == username);

        if (user == null)
        {
            TempData["ForgotError"] = "Email hoặc tên đăng nhập không khớp.";
            TempData["ShowForgot"] = "1";
            return RedirectToAction("Index");
        }
        TempData["ResetUserId"]   = user.Id.ToString();
        TempData["ResetUserName"] = user.FullName;
        TempData["ShowReset"]     = "1";
        return RedirectToAction("Index");
    }

    // ─── POST: ResetPassword (Step 2 - save new password) ────────────────────
    [HttpPost][ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(string userId, string newPassword, string confirmPassword)
    {
        if (newPassword != confirmPassword)
        {
            TempData["ResetError"]  = "Mật khẩu xác nhận không khớp.";
            TempData["ResetUserId"] = userId;
            TempData["ShowReset"]   = "1";
            return RedirectToAction("Index");
        }
        if (newPassword.Length < 8)
        {
            TempData["ResetError"]  = "Mật khẩu phải có ít nhất 8 ký tự.";
            TempData["ResetUserId"] = userId;
            TempData["ShowReset"]   = "1";
            return RedirectToAction("Index");
        }
        if (!Guid.TryParse(userId, out var uid))
        {
            TempData["ForgotError"] = "Phiên khôi phục không hợp lệ.";
            TempData["ShowForgot"]  = "1";
            return RedirectToAction("Index");
        }
        var user = await _context.Users.FindAsync(uid);
        if (user == null) { TempData["LoginError"] = "Không tìm thấy tài khoản."; return RedirectToAction("Index"); }

        user.PasswordHash = BC.HashPassword(newPassword, workFactor: 12);
        await _context.SaveChangesAsync();
        // Invalidate old session, create fresh one
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        _logger.LogInformation("AUDIT_PASSWORD_RESET userId={Id} email={Email}", user.Id, user.Email);
        await SignInUser(user);
        TempData["Success"] = "Đặt lại mật khẩu thành công! Chào mừng " + user.FullName;
        return RedirectToAction("Index");
    }

    // ─── POST: CreateBooking (AJAX) ────────────────────────────────────────────
    [HttpPost][ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateBooking(int roomId, string title, DateTime startTime, DateTime endTime, string description, string status, IFormFile? document)
    {
        if (User.Identity?.IsAuthenticated != true)
            return Json(new { success = false, message = "Bạn phải đăng nhập." });

        var conflict = await _context.Bookings.AnyAsync(b =>
            b.RoomId == roomId && b.StartTime < endTime && b.EndTime > startTime &&
            b.Status != "Rejected");
        if (conflict)
            return Json(new { success = false, conflict = true, message = "Trùng lịch, đã có lịch đăng ký!" });

        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdStr, out var userId))
            return Json(new { success = false, message = "Phiên đăng nhập không hợp lệ." });

        var booking = new Booking
        {
            Id          = Guid.NewGuid(),
            RoomId      = roomId,
            UserId      = userId,
            Title       = title,
            StartTime   = startTime,
            EndTime     = endTime,
            Description = description ?? "",
            Status      = "Pending"
        };
        _context.Bookings.Add(booking);

        if (document != null && document.Length > 0)
        {
            // ── Security: validate file type and size ──
            var allowedExtensions = new[] { ".pdf", ".docx", ".xlsx", ".doc", ".xls", ".png", ".jpg", ".jpeg" };
            var ext = Path.GetExtension(document.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(ext))
                return Json(new { success = false, message = "Chỉ chấp nhận file PDF, DOCX, XLSX, PNG, JPG." });
            if (document.Length > 10 * 1024 * 1024)
                return Json(new { success = false, message = "File không được vượt quá 10MB." });

            // Store OUTSIDE wwwroot so files are not publicly accessible by URL
            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "secure_uploads");
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);
            var safeFileName = Guid.NewGuid() + ext; // never use original filename on disk
            using var stream = new FileStream(Path.Combine(uploadsFolder, safeFileName), FileMode.Create);
            await document.CopyToAsync(stream);
            _context.Documents.Add(new Document { Id = Guid.NewGuid(), BookingId = booking.Id, FileName = document.FileName, FilePath = safeFileName, UploadedAt = DateTime.Now });
        }

        await _context.SaveChangesAsync();
        return Json(new { success = true, message = "Đặt lịch thành công! Chờ Admin phê duyệt." });
    }

    // ─── POST: EditBooking (AJAX) ──────────────────────────────────────────────
    [HttpPost][ValidateAntiForgeryToken]
    public async Task<IActionResult> EditBooking(Guid id, int roomId, string title, DateTime startTime, DateTime endTime, string description)
    {
        if (User.Identity?.IsAuthenticated != true)
            return Json(new { success = false, message = "Bạn phải đăng nhập." });

        var booking = await _context.Bookings.FindAsync(id);
        if (booking == null) return Json(new { success = false, message = "Không tìm thấy lịch." });

        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var isAdmin   = User.IsInRole("Admin");

        // Only owner or admin can edit
        if (!isAdmin && booking.UserId.ToString() != userIdStr)
            return Json(new { success = false, message = "Bạn không có quyền chỉnh sửa lịch này." });

        // Check conflict (exclude self)
        var conflict = await _context.Bookings.AnyAsync(b =>
            b.Id != id && b.RoomId == roomId && b.StartTime < endTime && b.EndTime > startTime &&
            b.Status != "Rejected");
        if (conflict)
            return Json(new { success = false, conflict = true, message = "Trùng lịch, đã có lịch đăng ký!" });

        booking.RoomId      = roomId;
        booking.Title       = title;
        booking.StartTime   = startTime;
        booking.EndTime     = endTime;
        booking.Description = description ?? "";
        booking.Status      = "Pending"; // reset to pending after edit
        booking.RejectionReason = null;

        await _context.SaveChangesAsync();
        return Json(new { success = true, message = "Cập nhật lịch thành công!" });
    }

    // ─── POST: ApproveBooking (Admin only, AJAX) ─────────────────────────────
    [Authorize(Roles = "Admin")]
    [HttpPost][ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveBooking(Guid id)
    {
        var booking = await _context.Bookings.FindAsync(id);
        if (booking == null) return Json(new { success = false, message = "Không tìm thấy lịch." });

        booking.Status = "Approved";
        booking.RejectionReason = null;
        await _context.SaveChangesAsync();
        _logger.LogInformation("AUDIT_BOOKING_APPROVED bookingId={Id} by={Admin}", id, User.Identity?.Name);
        return Json(new { success = true, message = "Đã duyệt lịch họp." });
    }

    // ─── POST: RejectBooking (Admin only, AJAX) ──────────────────────────────
    [Authorize(Roles = "Admin")]
    [HttpPost][ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectBooking(Guid id, string reason)
    {
        var booking = await _context.Bookings.FindAsync(id);
        if (booking == null) return Json(new { success = false, message = "Không tìm thấy lịch." });

        booking.Status = "Rejected";
        booking.RejectionReason = reason;
        await _context.SaveChangesAsync();
        _logger.LogInformation("AUDIT_BOOKING_REJECTED bookingId={Id} reason={Reason} by={Admin}", id, reason, User.Identity?.Name);
        return Json(new { success = true, message = "Đã từ chối lịch họp." });
    }

    // ─── POST: DeleteBooking (AJAX) ────────────────────────────────────────────
    [HttpPost][ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteBooking(Guid id)
    {
        if (User.Identity?.IsAuthenticated != true)
            return Json(new { success = false, message = "Bạn phải đăng nhập." });

        var booking = await _context.Bookings.FindAsync(id);
        if (booking == null) return Json(new { success = false, message = "Không tìm thấy lịch." });

        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var isAdmin   = User.IsInRole("Admin");

        if (!isAdmin && booking.UserId.ToString() != userIdStr)
            return Json(new { success = false, message = "Bạn không có quyền xoá lịch này." });

        _context.Bookings.Remove(booking);
        await _context.SaveChangesAsync();
        return Json(new { success = true });
    }

    // ─── POST: CreateRoom (Admin only, AJAX) ─────────────────────────────────
    [Authorize(Roles = "Admin")]
    [HttpPost][ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateRoom(string name, int capacity, string equipment, bool isActive)
    {
        if (!User.IsInRole("Admin"))
            return Json(new { success = false, message = "Chỉ Admin mới có quyền tạo phòng." });

        var room = new MeetingRoom { Name = name, Capacity = capacity, Equipment = equipment ?? "", IsActive = isActive };
        _context.MeetingRooms.Add(room);
        await _context.SaveChangesAsync();
        return Json(new { success = true, message = "Tạo phòng họp thành công!", roomId = room.Id });
    }

    // ─── POST: DeleteRoom (Admin only, AJAX) ─────────────────────────────────
    [Authorize(Roles = "Admin")]
    [HttpPost][ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteRoom(int id)
    {
        if (!User.IsInRole("Admin"))
            return Json(new { success = false, message = "Chỉ Admin mới có quyền xoá phòng." });

        var room = await _context.MeetingRooms.FindAsync(id);
        if (room != null) _context.MeetingRooms.Remove(room);
        await _context.SaveChangesAsync();
        return Json(new { success = true });
    }

    // ─── POST: UpdateRoom (Admin only, AJAX) ──────────────────────────────────
    [Authorize(Roles = "Admin")]
    [HttpPost][ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateRoom(int id, string name, int capacity, string equipment, bool isActive)
    {
        if (!User.IsInRole("Admin"))
            return Json(new { success = false, message = "Chỉ Admin mới có quyền sửa phòng." });

        var room = await _context.MeetingRooms.FindAsync(id);
        if (room == null) return Json(new { success = false, message = "Không tìm thấy phòng." });

        room.Name      = name?.Trim() ?? room.Name;
        room.Capacity  = capacity > 0 ? capacity : room.Capacity;
        room.Equipment = equipment ?? "";
        room.IsActive  = isActive;
        await _context.SaveChangesAsync();

        return Json(new { success = true, id = room.Id, name = room.Name, capacity = room.Capacity, equipment = room.Equipment, isActive = room.IsActive });
    }


    // ─── POST: ChangeRole (Admin only, AJAX) ───────────────────────────────────
    [Authorize(Roles = "Admin")]
    [HttpPost][ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeRole(string userId)
    {
        if (!User.IsInRole("Admin"))
            return Json(new { success = false, message = "Chỉ Admin mới có quyền này." });

        var currentId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == currentId)
            return Json(new { success = false, message = "Không thể thay đổi quyền của chính mình." });

        var user = await _context.Users.FindAsync(Guid.Parse(userId));
        if (user == null) return Json(new { success = false, message = "Không tìm thấy tài khoản." });

        user.Role = user.Role == "Admin" ? "Employee" : "Admin";
        await _context.SaveChangesAsync();
        _logger.LogWarning("AUDIT_ROLE_CHANGED targetUserId={Id} email={Email} newRole={Role} by={Admin}",
            user.Id, user.Email, user.Role, User.Identity?.Name);
        return Json(new { success = true, newRole = user.Role });
    }

    // ─── POST: DeleteUser (Admin only, AJAX) ───────────────────────────────────
    [HttpPost][ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUser(string userId)
    {
        if (!User.IsInRole("Admin"))
            return Json(new { success = false, message = "Chỉ Admin mới có quyền này." });

        var currentId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == currentId)
            return Json(new { success = false, message = "Không thể xoá chính mình." });

        var user = await _context.Users.FindAsync(Guid.Parse(userId));
        if (user == null) return Json(new { success = false, message = "Không tìm thấy tài khoản." });

        _context.Users.Remove(user);
        await _context.SaveChangesAsync();
        _logger.LogWarning("AUDIT_USER_DELETED targetUserId={Id} email={Email} by={Admin}",
            user.Id, user.Email, User.Identity?.Name);
        return Json(new { success = true });
    }

    public IActionResult Privacy() => View();

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() =>
        View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });

    // ─── Helpers ───────────────────────────────────────────────────────────────
    private async Task SignInUser(User user)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, user.FullName),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Role, user.Role)
        };
        var identity  = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        var props = new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8) };
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, props);
    }

    /// <summary>Verify password — supports both BCrypt and legacy SHA-256 hashes.</summary>
    private static bool VerifyPassword(string password, string storedHash)
    {
        // BCrypt hash always starts with $2a, $2b or $2y
        if (storedHash.StartsWith("$2"))
            return BC.Verify(password, storedHash);

        // Legacy SHA-256 (64 hex chars) — used for migration
        using var sha = SHA256.Create();
        var sha256 = Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(password))).ToLower();
        return sha256 == storedHash;
    }

    private static string HashPassword(string password)
    {
        // BCrypt with work factor 12 (~250ms per hash on modern hardware)
        return BC.HashPassword(password, workFactor: 12);
    }
}
