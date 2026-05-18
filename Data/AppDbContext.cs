using Microsoft.EntityFrameworkCore;
using MeetingRoomWeb.Models;

namespace MeetingRoomWeb.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<MeetingRoom> MeetingRooms { get; set; }
        public DbSet<Booking> Bookings { get; set; }
        public DbSet<Document> Documents { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
        }
    }
}
