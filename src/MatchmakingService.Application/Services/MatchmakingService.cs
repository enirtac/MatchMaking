using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using MatchmakingService.Application.Models;

namespace MatchmakingService.Application.Services
{
    public class MatchmakingService : IMatchmakingService
    {
        private readonly ConcurrentDictionary<string, PlayerQueueEntry> _queue = new();
        private readonly List<GameSession> _sessions = new();
        private readonly object _lock = new();

        private const int MinPlayersToStart = 4;
        private const int MaxPlayersPerSession = 10;

        public void Enqueue(PlayerQueueEntry player)
        {
            _queue[player.PlayerId] = player;
        }

        public void Dequeue(string playerId)
        {
            _queue.TryRemove(playerId, out _);
        }

        public void RunMatchmaking()
        {
            var players = new List<PlayerQueueEntry>();
            while (_queue.Count > 0)
            {
                var key = _queue.Keys.FirstOrDefault();
                if (key != null && _queue.TryRemove(key, out var player))
                {
                    players.Add(player);
                }
            }

            // Group by game so players only match within the same game
            var grouped = players.GroupBy(p => p.GameId);

            lock (_lock)
            {
                foreach (var group in grouped)
                {
                    var sorted = group
                        .OrderByDescending(p => Score(p))
                        .ToList();

                    foreach (var player in sorted)
                    {
                        var session = _sessions
                            .FirstOrDefault(s => s.GameId == player.GameId && s.Status != SessionStatus.Full);

                        if (session != null)
                        {
                            session.Players.Add(player);
                        }
                        else
                        {
                            session = new GameSession { GameId = player.GameId };
                            session.Players.Add(player);
                            _sessions.Add(session);
                        }

                        UpdateSessionStatus(session);
                    }
                }
            }
        }
        public GameSession? TryJoinExistingSession(PlayerQueueEntry player)
        {
            lock (_lock)
            {
                var session = _sessions
                    .FirstOrDefault(s => s.GameId == player.GameId && s.Status == SessionStatus.Started);

                if (session == null) return null;

                session.Players.Add(player);
                UpdateSessionStatus(session);
                return session;
            }
        }

        public GameSession? GetPlayerSession(string playerId)
        {
            lock (_lock)
            {
                return _sessions.FirstOrDefault(s =>
                    s.Players.Any(p => p.PlayerId == playerId));
            }
        }

        public List<GameSession> GetSessions()
        {
            lock (_lock)
            {
                return _sessions.ToList();
            }
        }

        private void UpdateSessionStatus(GameSession session)
        {
            if (session.Players.Count >= MaxPlayersPerSession)
                session.Status = SessionStatus.Full;
            else if (session.Players.Count >= MinPlayersToStart)
                session.Status = SessionStatus.Started;
            else
                session.Status = SessionStatus.Lobby;
        }
        private static double Score(PlayerQueueEntry player)
        {
            var waitTime = (DateTime.UtcNow - player.EnqueuedAt).TotalSeconds;
            return waitTime - (player.LatencyMs * 0.01);
        }

        public bool HasPlayersInQueue()
        {
            return !_queue.IsEmpty;
        }
    }
}