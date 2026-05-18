using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MeetingRoomWeb.Models
{
    public class Booking
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public int RoomId { get; set; }

        [ForeignKey("RoomId")]
        public virtual MeetingRoom? Room { get; set; }

        [Required]
        public Guid UserId { get; set; }

        [ForeignKey("UserId")]
        public virtual User? User { get; set; }

        [Required]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        public DateTime StartTime { get; set; }

        [Required]
        public DateTime EndTime { get; set; }

        public string Description { get; set; } = string.Empty;

        [StringLength(20)]
        public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected

        public string? RejectionReason { get; set; }

        public virtual ICollection<Document> Documents { get; set; } = new List<Document>();
    }
}
