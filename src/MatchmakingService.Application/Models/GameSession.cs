using System.Collections.Generic;
using System;
namespace MatchmakingService.Application.Models
{
    public class GameSession
    {
        public string SessionId { get; set; } = Guid.NewGuid().ToString();
        public string GameId { get; set; } = string.Empty;
        public string SessionKey { get; set; } = string.Empty;
        public List<PlayerQueueEntry> Players { get; set; } = new();
        public SessionStatus Status { get; set; } = SessionStatus.Lobby;
        public int MaxPlayersPerSession { get; set; } = 10;
        public int MinPlayersToStart { get; set; } = 4;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool IsFull => Players.Count >= MaxPlayersPerSession;
        public bool CanJoin => Status != SessionStatus.Full;
    }
}