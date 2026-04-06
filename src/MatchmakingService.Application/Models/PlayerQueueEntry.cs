using System;
using System.ComponentModel.DataAnnotations;
namespace MatchmakingService.Application.Models
{
    public class PlayerQueueEntry
    {
        [Required]
        public string PlayerId { get; set; } = string.Empty;
        [Required]
        public string GameId { get; set; } = string.Empty;
        [Range(1, int.MaxValue)]
        public int LatencyMs { get; set; }

        public DateTime EnqueuedAt { get; set; } = DateTime.UtcNow;
    }
}