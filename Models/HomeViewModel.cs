using System;
using System.Collections.Generic;

namespace MeetingRoomWeb.Models
{
    public class HomeViewModel
    {
        public List<Booking> UpcomingBookings { get; set; } = new();
        public List<Booking> AllBookings { get; set; } = new();
        public List<MeetingRoom> Rooms { get; set; } = new();
        // Room usage history - filtered
        public List<Booking> RoomUsageBookings { get; set; } = new();
        public List<User> Users { get; set; } = new();

        // Auth state
        public bool IsLoggedIn { get; set; }
        public bool IsAdmin { get; set; }
        public string CurrentUserName { get; set; } = "";
        public string CurrentUserEmail { get; set; } = "";
        public string CurrentUserId { get; set; } = "";

        // Filter state
        public string FilterPeriod { get; set; } = "week"; // day / week / month
        public DateTime FilterDate { get; set; } = DateTime.Today;
    }
}
