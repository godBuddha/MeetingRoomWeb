using System.ComponentModel.DataAnnotations;

namespace MeetingRoomWeb.Models
{
    public class MeetingRoom
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        public int Capacity { get; set; }

        [StringLength(255)]
        public string Equipment { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;
    }
}
