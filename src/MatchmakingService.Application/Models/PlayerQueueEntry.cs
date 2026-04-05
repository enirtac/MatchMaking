using System;
namespace MatchmakingService.Application.Models
{
    public class PlayerQueueEntry
    {
        public string PlayerId { get; set; } = string.Empty;
        public string GameId { get; set; } = string.Empty;
        public int LatencyMs { get; set; }

        public DateTime EnqueuedAt { get; set; } = DateTime.UtcNow;
    }
}