using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MeetingRoomWeb.Models
{
    public class Document
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public Guid BookingId { get; set; }

        [ForeignKey("BookingId")]
        public virtual Booking? Booking { get; set; }

        [Required]
        [StringLength(255)]
        public string FileName { get; set; } = string.Empty;

        [Required]
        public string FilePath { get; set; } = string.Empty;

        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    }
}
