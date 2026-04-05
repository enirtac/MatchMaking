using System.Collections.Generic;
using FluentAssertions;
using MatchmakingService.Api.Controllers;
using MatchmakingService.Application.Models;
using MatchmakingService.Application.Services;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace MatchmakingService.Tests.Controllers
{
    public class MatchmakingControllerTests
    {
        private readonly Mock<IMatchmakingService> _serviceMock;
        private readonly MatchmakingController _controller;

        public MatchmakingControllerTests()
        {
            _serviceMock = new Mock<IMatchmakingService>();
            _controller = new MatchmakingController(_serviceMock.Object);
        }

        [Fact]
        public void Enqueue_ShouldCallServiceAndReturnOk()
        {
            var player = new PlayerQueueEntry { PlayerId = "player1", LatencyMs = 50, GameId = "TestGame" };

            var result = _controller.Enqueue(player);

            result.Should().BeOfType<OkObjectResult>();
            _serviceMock.Verify(s => s.Enqueue(player), Times.Once);
        }

        [Fact]
        public void Dequeue_ShouldCallServiceAndReturnOk()
        {
            var result = _controller.Dequeue("player1");

            result.Should().BeOfType<OkObjectResult>();
            _serviceMock.Verify(s => s.Dequeue("player1"), Times.Once);
        }

        [Fact]
        public void GetSessions_ShouldReturnOkWithSessions()
        {
            var sessions = new List<GameSession>
            {
                new GameSession { Players = new List<PlayerQueueEntry>(), GameId = "TestGame" }
            };
            _serviceMock.Setup(s => s.GetSessions()).Returns(sessions);

            var result = _controller.GetSessions();

            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().Be(sessions);
        }

        [Fact]
        public void JoinOrQueue_WhenSessionAvailable_ShouldReturnJoined()
        {
            var player = new PlayerQueueEntry { PlayerId = "player1", LatencyMs = 50, GameId = "TestGame" };
            var session = new GameSession { Players = new List<PlayerQueueEntry> { player }, GameId = "TestGame" };
            _serviceMock.Setup(s => s.TryJoinExistingSession(player)).Returns(session);

            var result = _controller.JoinOrQueue(player);

            result.Should().BeOfType<OkObjectResult>();
            _serviceMock.Verify(s => s.Enqueue(It.IsAny<PlayerQueueEntry>()), Times.Never);
        }

        [Fact]
        public void JoinOrQueue_WhenNoSessionAvailable_ShouldReturnQueued()
        {
            var player = new PlayerQueueEntry { PlayerId = "player1", LatencyMs = 50, GameId = "TestGame" };
            _serviceMock.Setup(s => s.TryJoinExistingSession(player)).Returns((GameSession?)null);

            var result = _controller.JoinOrQueue(player);

            result.Should().BeOfType<OkObjectResult>();
            _serviceMock.Verify(s => s.Enqueue(player), Times.Once);
        }

        [Fact]
        public void Ping_ShouldReturnOkWithPong()
        {
            var result = _controller.Ping();

            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().NotBeNull();
        }
        [Fact]
        public void GetPlayerStatus_WhenMatched_ShouldReturnSessionInfo()
        {
            var session = new GameSession
            {
                Players = new List<PlayerQueueEntry>
                {
                    new() { PlayerId = "player1", LatencyMs = 50, GameId = "TestGame" },
                    new() { PlayerId = "player2", LatencyMs = 45, GameId = "TestGame" },
                    new() { PlayerId = "player3", LatencyMs = 55, GameId = "TestGame" },
                    new() { PlayerId = "player4", LatencyMs = 40, GameId = "TestGame" }
                },
                GameId = "TestGame"
            };
            _serviceMock.Setup(s => s.GetPlayerSession("player1")).Returns(session);

            var result = _controller.GetPlayerStatus("player1");

            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().BeEquivalentTo(new
            {
                status = "lobby",
                sessionId = session.SessionId,
                players = new[] { "player1", "player2", "player3", "player4" }
            });
        }

        [Fact]
        public void GetPlayerStatus_WhenWaiting_ShouldReturnWaiting()
        {
            _serviceMock.Setup(s => s.GetPlayerSession("player1")).Returns((GameSession?)null);

            var result = _controller.GetPlayerStatus("player1");

            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().BeEquivalentTo(new { status = "waiting" });
        }
        [Fact]
        public void Enqueue_WithMissingGameId_ShouldReturnBadRequest()
        {
            var player = new PlayerQueueEntry { PlayerId = "player1", GameId = "", LatencyMs = 50 };

            var result = _controller.Enqueue(player);

            result.Should().BeOfType<BadRequestObjectResult>();
            _serviceMock.Verify(s => s.Enqueue(It.IsAny<PlayerQueueEntry>()), Times.Never);
        }

        [Fact]
        public void Enqueue_WithMissingPlayerId_ShouldReturnBadRequest()
        {
            var player = new PlayerQueueEntry { PlayerId = "", GameId = "TestGame", LatencyMs = 50 };

            var result = _controller.Enqueue(player);

            result.Should().BeOfType<BadRequestObjectResult>();
            _serviceMock.Verify(s => s.Enqueue(It.IsAny<PlayerQueueEntry>()), Times.Never);
        }

        [Fact]
        public void JoinOrQueue_WithMissingGameId_ShouldReturnBadRequest()
        {
            var player = new PlayerQueueEntry { PlayerId = "player1", GameId = "", LatencyMs = 50 };

            var result = _controller.JoinOrQueue(player);

            result.Should().BeOfType<BadRequestObjectResult>();
            _serviceMock.Verify(s => s.TryJoinExistingSession(It.IsAny<PlayerQueueEntry>()), Times.Never);
            _serviceMock.Verify(s => s.Enqueue(It.IsAny<PlayerQueueEntry>()), Times.Never);
        }

        [Fact]
        public void GetSessions_WithGameIdFilter_ShouldReturnFilteredSessions()
        {
            var sessions = new List<GameSession>
            {
                new GameSession { GameId = "racing", Players = new List<PlayerQueueEntry>() },
                new GameSession { GameId = "battle-royale", Players = new List<PlayerQueueEntry>() },
                new GameSession { GameId = "racing", Players = new List<PlayerQueueEntry>() }
            };
            _serviceMock.Setup(s => s.GetSessions()).Returns(sessions);

            var result = _controller.GetSessions("racing");

            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            var filtered = okResult.Value as List<GameSession>;
            filtered.Should().HaveCount(2);
            filtered.Should().OnlyContain(s => s.GameId == "racing");
        }

        [Fact]
        public void GetSessions_WithoutFilter_ShouldReturnAllSessions()
        {
            var sessions = new List<GameSession>
            {
                new GameSession { GameId = "racing", Players = new List<PlayerQueueEntry>() },
                new GameSession { GameId = "battle-royale", Players = new List<PlayerQueueEntry>() }
            };
            _serviceMock.Setup(s => s.GetSessions()).Returns(sessions);

            var result = _controller.GetSessions();

            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            var all = okResult.Value as List<GameSession>;
            all.Should().HaveCount(2);
        }

        [Fact]
        public void Dequeue_WithEmptyPlayerId_ShouldReturnBadRequest()
        {
            var result = _controller.Dequeue("");

            result.Should().BeOfType<BadRequestObjectResult>();
            _serviceMock.Verify(s => s.Dequeue(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void GetPlayerStatus_WithEmptyPlayerId_ShouldReturnBadRequest()
        {
            var result = _controller.GetPlayerStatus("");

            result.Should().BeOfType<BadRequestObjectResult>();
        }
    }
}