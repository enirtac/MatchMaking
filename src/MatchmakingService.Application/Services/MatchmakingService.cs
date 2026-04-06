using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using MatchmakingService.Application.Models;

namespace MatchmakingService.Application.Services
{
    public class MatchmakingService : IMatchmakingService
    {
        private readonly Dictionary<string, PlayerQueueEntry> _queue = new();
        private readonly Dictionary<string, string> _playerSessionMap = new();
        //Dictionary for sessions by id
        private readonly Dictionary<string, GameSession> _sessionsById = new();
        //Dictionary for open sessions by game
        private readonly Dictionary<string, GameSession> _openSessionByGame = new();


        private readonly object _lock = new();

        public void Enqueue(PlayerQueueEntry player)
        {
            lock (_lock)
            {
                _queue[player.PlayerId] = player;
            }
        }

        public void Dequeue(string playerId)
        {
            lock (_lock)
            {
                _queue.Remove(playerId);
            }
        }

        public void RunMatchmaking()
        {
            lock (_lock)
            {
                var players = _queue.Values.ToList();
                _queue.Clear();
                if (players.Count == 0) return;
                foreach (var group in players.GroupBy(p => p.GameId))
                {

                    var latencyGroups = group
                        .GroupBy(p => GetLatencyScore(p.LatencyMs))
                        .OrderBy(g => g.Key);

                    foreach (var latencyGroup in latencyGroups)
                    {
                        foreach (var player in latencyGroup.OrderBy(p => p.EnqueuedAt))
                        {

                            var sessionKey = $"{player.GameId}_{latencyGroup.Key}";
                            var session = GetOrCreateOpenSession(sessionKey, player.GameId);
                            session.Players.Add(player);
                            _playerSessionMap[player.PlayerId] = session.SessionId;
                            UpdateSessionStatus(session);

                        }
                    }
                }
            }
        }
        public GameSession? TryJoinExistingSession(PlayerQueueEntry player)
        {
            lock (_lock)
            {
                var sessionKey = $"{player.GameId}_{GetLatencyScore(player.LatencyMs)}";

                if (!_openSessionByGame.TryGetValue(sessionKey, out var session))
                    return null;

                if (session.Status != SessionStatus.Started)
                    return null;

                var alreadyInSession = session.Players.Any(p => p.PlayerId == player.PlayerId);

                if (alreadyInSession)
                    return session;
                session.Players.Add(player);
                _playerSessionMap[player.PlayerId] = session.SessionId;
                UpdateSessionStatus(session);

                return session;
            }
        }

        public GameSession? GetPlayerSession(string playerId)
        {
            lock (_lock)
            {
                if (!_playerSessionMap.TryGetValue(playerId, out var sessionId))
                    return null;

                _sessionsById.TryGetValue(sessionId, out var session);
                return session;
            }
        }
        public List<GameSession> GetSessions()
        {
            lock (_lock)
            {
                return _sessionsById.Values.ToList();
            }
        }

        private void UpdateSessionStatus(GameSession session)
        {
            session.Status = session.Players.Count >= session.MaxPlayersPerSession ? SessionStatus.Full
                             : session.Players.Count >= session.MinPlayersToStart ? SessionStatus.Started
                             : SessionStatus.Lobby;

            if (session.Status == SessionStatus.Full)
                _openSessionByGame.Remove(session.SessionKey);
        }
        private static int GetLatencyScore(int latencyMs) => latencyMs switch
        {
            < 50 => 0,    // 0–49ms   (very good)
            < 100 => 1,   // 50–99ms  (good)
            < 150 => 2,   // 100–149ms (ok)
            _ => 3         // 150ms+   (poor)
        };

        private GameSession GetOrCreateOpenSession(string sessionKey, string gameId)
        {
            if (_openSessionByGame.TryGetValue(sessionKey, out var session) && session.CanJoin)
                return session;

            var newSession = new GameSession { GameId = gameId, SessionKey = sessionKey };
            _sessionsById[newSession.SessionId] = newSession;
            _openSessionByGame[sessionKey] = newSession;
            return newSession;
        }
        public bool HasPlayersInQueue()
        {
            lock (_lock)
            {
                return _queue.Count > 0;
            }
        }
        public bool IsPlayerInQueue(string playerId)
        {
            lock (_lock)
            {
                return _queue.ContainsKey(playerId);
            }
        }
    }
}