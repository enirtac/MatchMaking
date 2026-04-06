using System;
using System.Linq;
using FluentAssertions;
using MatchmakingService.Application.Models;
using MatchmakingService.Application.Services;
using Xunit;

namespace MatchmakingService.Tests.Services
{
    public class MatchmakingServiceTests
    {
        private readonly Application.Services.MatchmakingService _service;

        public MatchmakingServiceTests()
        {
            _service = new Application.Services.MatchmakingService();
        }

        // === Enqueue ===

        [Fact]
        public void Enqueue_ShouldAddPlayerToQueue()
        {
            var player = CreatePlayer("player1");

            _service.Enqueue(player);
            _service.RunMatchmaking();

            _service.GetSessions().Should().HaveCount(1);
            _service.GetSessions().First().Players.Should().HaveCount(1);
        }

        // === Dequeue ===

        [Fact]
        public void Dequeue_ShouldRemovePlayerFromQueue()
        {
            var player = CreatePlayer("player1");
            _service.Enqueue(player);
            _service.Dequeue("player1");

            _service.RunMatchmaking();

            _service.GetSessions().Should().BeEmpty();
        }

        [Fact]
        public void Dequeue_NonExistentPlayer_ShouldNotThrow()
        {
            var act = () => _service.Dequeue("nonexistent");
            act.Should().NotThrow();
        }

        // === RunMatchmaking - Session Status ===

        [Fact]
        public void RunMatchmaking_With1Player_ShouldCreateLobbySession()
        {
            EnqueuePlayers(1);
            _service.RunMatchmaking();

            _service.GetSessions().Should().HaveCount(1);
            _service.GetSessions().First().Players.Should().HaveCount(1);
            _service.GetSessions().First().Status.Should().Be(SessionStatus.Lobby);
        }

        [Fact]
        public void RunMatchmaking_With3Players_ShouldStayInLobby()
        {
            EnqueuePlayers(3);
            _service.RunMatchmaking();

            _service.GetSessions().Should().HaveCount(1);
            _service.GetSessions().First().Players.Should().HaveCount(3);
            _service.GetSessions().First().Status.Should().Be(SessionStatus.Lobby);
        }

        [Fact]
        public void RunMatchmaking_With4Players_ShouldCreateStartedSession()
        {
            EnqueuePlayers(4);
            _service.RunMatchmaking();

            _service.GetSessions().Should().HaveCount(1);
            _service.GetSessions().First().Players.Should().HaveCount(4);
            _service.GetSessions().First().Status.Should().Be(SessionStatus.Started);
        }

        [Fact]
        public void RunMatchmaking_With5Players_ShouldBeStarted()
        {
            EnqueuePlayers(5);
            _service.RunMatchmaking();

            _service.GetSessions().Should().HaveCount(1);
            _service.GetSessions().First().Players.Should().HaveCount(5);
            _service.GetSessions().First().Status.Should().Be(SessionStatus.Started);
        }

        [Fact]
        public void RunMatchmaking_With10Players_ShouldCreateFullSession()
        {
            EnqueuePlayers(10);
            _service.RunMatchmaking();

            _service.GetSessions().Should().HaveCount(1);
            _service.GetSessions().First().Players.Should().HaveCount(10);
            _service.GetSessions().First().Status.Should().Be(SessionStatus.Full);
        }

        [Fact]
        public void RunMatchmaking_With11Players_ShouldCreateFullAndLobby()
        {
            for (int i = 0; i < 11; i++)
            {
                _service.Enqueue(new PlayerQueueEntry
                {
                    PlayerId = $"player{i}",
                    GameId = "TestGame",
                    LatencyMs = 50 // samma bracket
                });
            }

            _service.RunMatchmaking();

            var sessions = _service.GetSessions();
            sessions.Should().HaveCount(2);
            sessions.Should().ContainSingle(s => s.Status == SessionStatus.Full);
            sessions.Should().ContainSingle(s => s.Status == SessionStatus.Lobby);
            sessions.First(s => s.Status == SessionStatus.Full).Players.Should().HaveCount(10);
            sessions.First(s => s.Status == SessionStatus.Lobby).Players.Should().HaveCount(1);
        }

        // === RunMatchmaking - Fill Existing Session ===

        [Fact]
        public void RunMatchmaking_ShouldFillExistingSessionFirst()
        {
            EnqueuePlayers(3);
            _service.RunMatchmaking();

            EnqueuePlayers(2, startIndex: 200);
            _service.RunMatchmaking();

            _service.GetSessions().Should().HaveCount(1);
            _service.GetSessions().First().Players.Should().HaveCount(5);
            _service.GetSessions().First().Status.Should().Be(SessionStatus.Started);
        }

        [Fact]
        public void RunMatchmaking_ShouldRemoveMatchedPlayersFromQueue()
        {
            EnqueuePlayers(4);
            _service.RunMatchmaking();
            _service.RunMatchmaking();

            _service.GetSessions().Should().HaveCount(1);
        }

        // === RunMatchmaking - Latency Grouping ===

        [Fact]
        public void RunMatchmaking_ShouldGroupPlayersByLatencyBracket()
        {
            // Bracket 0: 0-49ms
            _service.Enqueue(CreatePlayer("low1", latency: 20));
            _service.Enqueue(CreatePlayer("low2", latency: 30));
            _service.Enqueue(CreatePlayer("low3", latency: 40));
            _service.Enqueue(CreatePlayer("low4", latency: 25));

            // Bracket 2: 100-149ms
            _service.Enqueue(CreatePlayer("high1", latency: 120));
            _service.Enqueue(CreatePlayer("high2", latency: 130));
            _service.Enqueue(CreatePlayer("high3", latency: 110));
            _service.Enqueue(CreatePlayer("high4", latency: 140));

            _service.RunMatchmaking();

            var sessions = _service.GetSessions();
            sessions.Should().HaveCount(2);

            var lowSession = sessions.First(s => s.Players.Any(p => p.PlayerId == "low1"));
            var highSession = sessions.First(s => s.Players.Any(p => p.PlayerId == "high1"));

            lowSession.Players.Should().HaveCount(4);
            lowSession.Players.Should().OnlyContain(p => p.LatencyMs < 50);

            highSession.Players.Should().HaveCount(4);
            highSession.Players.Should().OnlyContain(p => p.LatencyMs >= 100 && p.LatencyMs < 150);
        }

        [Fact]
        public void RunMatchmaking_DifferentLatencies_ShouldNotMixInSameSession()
        {
            _service.Enqueue(CreatePlayer("fast", latency: 10));
            _service.Enqueue(CreatePlayer("slow", latency: 200));

            _service.RunMatchmaking();

            var sessions = _service.GetSessions();
            sessions.Should().HaveCount(2);
            sessions.Should().NotContain(s =>
                s.Players.Any(p => p.LatencyMs < 50) &&
                s.Players.Any(p => p.LatencyMs >= 150));
        }

        [Fact]
        public void RunMatchmaking_SameLatencyBracket_ShouldPrioritizeWaitTime()
        {
            var oldPlayer = new PlayerQueueEntry
            {
                PlayerId = "old_player",
                LatencyMs = 30,
                EnqueuedAt = DateTime.UtcNow.AddMinutes(-5),
                GameId = "TestGame"
            };
            var newPlayer = new PlayerQueueEntry
            {
                PlayerId = "new_player",
                LatencyMs = 40,
                EnqueuedAt = DateTime.UtcNow,
                GameId = "TestGame"
            };

            _service.Enqueue(newPlayer);
            _service.Enqueue(oldPlayer);
            _service.RunMatchmaking();

            var session = _service.GetSessions().First();
            var players = session.Players.ToList();
            var oldIndex = players.FindIndex(p => p.PlayerId == "old_player");
            var newIndex = players.FindIndex(p => p.PlayerId == "new_player");
            oldIndex.Should().BeLessThan(newIndex, "older player should be added first");
        }

        // === TryJoinExistingSession ===

        [Fact]
        public void TryJoinExistingSession_WithStartedSession_ShouldJoin()
        {
            EnqueuePlayers(4);
            _service.RunMatchmaking();

            var result = _service.TryJoinExistingSession(CreatePlayer("joiner"));

            result.Should().NotBeNull();
            result!.Players.Should().HaveCount(5);
            result.Players.Should().Contain(p => p.PlayerId == "joiner");
        }

        [Fact]
        public void TryJoinExistingSession_WithLobbySession_ShouldReturnNull()
        {
            EnqueuePlayers(2);
            _service.RunMatchmaking();

            var result = _service.TryJoinExistingSession(CreatePlayer("joiner"));

            result.Should().BeNull();
        }

        [Fact]
        public void TryJoinExistingSession_WithNoSessions_ShouldReturnNull()
        {
            var result = _service.TryJoinExistingSession(CreatePlayer("lonely"));

            result.Should().BeNull();
        }

        [Fact]
        public void TryJoinExistingSession_WithFullSession_ShouldReturnNull()
        {
            EnqueuePlayers(10);
            _service.RunMatchmaking();

            var result = _service.TryJoinExistingSession(CreatePlayer("extra"));

            result.Should().BeNull();
        }

        [Fact]
        public void TryJoinExistingSession_ShouldUpdateToFull()
        {
            EnqueuePlayers(9);
            _service.RunMatchmaking();

            _service.TryJoinExistingSession(CreatePlayer("last_one"));

            _service.GetSessions().First().Status.Should().Be(SessionStatus.Full);
        }

        // === GetPlayerSession ===

        [Fact]
        public void GetPlayerSession_WhenInSession_ShouldReturnSession()
        {
            EnqueuePlayers(1);
            _service.RunMatchmaking();

            var result = _service.GetPlayerSession("player_100");

            result.Should().NotBeNull();
            result!.Players.Should().Contain(p => p.PlayerId == "player_100");
        }

        [Fact]
        public void GetPlayerSession_WhenNotInSession_ShouldReturnNull()
        {
            var result = _service.GetPlayerSession("nonexistent");
            result.Should().BeNull();
        }

        [Fact]
        public void GetPlayerSession_WhenOnlyQueued_ShouldReturnNull()
        {
            _service.Enqueue(CreatePlayer("queued_player"));
            var result = _service.GetPlayerSession("queued_player");
            result.Should().BeNull();
        }

        // === IsPlayerInQueue ===

        [Fact]
        public void IsPlayerInQueue_WhenQueued_ShouldReturnTrue()
        {
            _service.Enqueue(CreatePlayer("player1"));
            _service.IsPlayerInQueue("player1").Should().BeTrue();
        }

        [Fact]
        public void IsPlayerInQueue_WhenNotQueued_ShouldReturnFalse()
        {
            _service.IsPlayerInQueue("nonexistent").Should().BeFalse();
        }

        [Fact]
        public void IsPlayerInQueue_WithEmptyString_ShouldReturnFalse()
        {
            _service.IsPlayerInQueue("").Should().BeFalse();
        }

        // === GetSessions ===

        [Fact]
        public void GetSessions_Initially_ShouldBeEmpty()
        {
            _service.GetSessions().Should().BeEmpty();
        }

        // === Helpers ===

        private void EnqueuePlayers(int count, int startIndex = 100)
        {
            for (int i = 0; i < count; i++)
            {
                _service.Enqueue(CreatePlayer($"player_{startIndex + i}"));
            }
        }

        private static PlayerQueueEntry CreatePlayer(string id, int latency = 50)
        {
            return new PlayerQueueEntry
            {
                PlayerId = id,
                LatencyMs = latency,
                EnqueuedAt = DateTime.UtcNow,
                GameId = "TestGame"
            };
        }
    }
}