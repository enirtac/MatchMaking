using System.Collections.Generic;
using System;
namespace MatchmakingService.Application.Models
{
    public class GameSession
    {
        public string SessionId { get; set; } = Guid.NewGuid().ToString();
        public string GameId { get; set; } = string.Empty;
        public List<PlayerQueueEntry> Players { get; set; } = new();
        public SessionStatus Status { get; set; } = SessionStatus.Lobby;
        public int MaxPlayers { get; set; } = 10;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool IsFull => Players.Count >= MaxPlayers;
        public bool CanJoin => Status != SessionStatus.Full;
    }
}