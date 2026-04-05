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
            EnqueuePlayers(11);
            _service.RunMatchmaking();

            _service.GetSessions().Should().HaveCount(2);
            _service.GetSessions()[0].Status.Should().Be(SessionStatus.Full);
            _service.GetSessions()[0].Players.Should().HaveCount(10);
            _service.GetSessions()[1].Status.Should().Be(SessionStatus.Lobby);
            _service.GetSessions()[1].Players.Should().HaveCount(1);
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

        // === RunMatchmaking - Scoring ===

        [Fact]
        public void RunMatchmaking_ShouldPrioritizeLongWaitAndLowLatency()
        {
            var oldPlayer = new PlayerQueueEntry
            {
                PlayerId = "old_player",
                LatencyMs = 10,
                EnqueuedAt = DateTime.UtcNow.AddMinutes(-5),
                GameId = "TestGame"
            };
            var newPlayer = new PlayerQueueEntry
            {
                PlayerId = "new_player",
                LatencyMs = 200,
                EnqueuedAt = DateTime.UtcNow,
                GameId = "TestGame"
            };

            _service.Enqueue(oldPlayer);
            _service.Enqueue(newPlayer);
            _service.RunMatchmaking();

            var session = _service.GetSessions().First();
            session.Players.Should().Contain(p => p.PlayerId == "old_player");
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