using MatchmakingService.Application.Models;
using System.Collections.Generic;

namespace MatchmakingService.Application.Services
{
    public interface IMatchmakingService
    {
        void Enqueue(PlayerQueueEntry player);
        void Dequeue(string playerId);
        void RunMatchmaking();
        bool HasPlayersInQueue();
        bool IsPlayerInQueue(string playerId);
        GameSession? TryJoinExistingSession(PlayerQueueEntry player);
        List<GameSession> GetSessions();
        GameSession? GetPlayerSession(string playerId);

    }
}