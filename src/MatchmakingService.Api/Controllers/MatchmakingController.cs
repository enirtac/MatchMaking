using Microsoft.AspNetCore.Mvc;
using MatchmakingService.Application.Models;
using MatchmakingService.Application.Services;

namespace MatchmakingService.Api.Controllers
{

    [ApiController]
    [Route("matchmaking")]
    public class MatchmakingController : ControllerBase
    {
        private readonly IMatchmakingService _service;

        public MatchmakingController(IMatchmakingService service)
        {
            _service = service;
        }

        [HttpPost("queue")]
        public IActionResult Enqueue([FromBody] PlayerQueueEntry player)
        {
            if (string.IsNullOrWhiteSpace(player.PlayerId) || string.IsNullOrWhiteSpace(player.GameId))
                return BadRequest(new { error = "playerId and gameId are required" });

            _service.Enqueue(player);
            return Ok(new { message = "Player queued successfully", playerId = player.PlayerId });
        }
        [HttpPost("join-or-queue")]
        public IActionResult JoinOrQueue([FromBody] PlayerQueueEntry player)
        {
            if (string.IsNullOrWhiteSpace(player.PlayerId) || string.IsNullOrWhiteSpace(player.GameId))
                return BadRequest(new { error = "playerId and gameId are required" });

            var session = _service.TryJoinExistingSession(player);

            if (session != null)
            {
                return Ok(new { status = "joined", sessionId = session.SessionId });
            }

            _service.Enqueue(player);

            return Ok(new { status = "queued", playerId = player.PlayerId });
        }
        [HttpDelete("queue/{playerId}")]
        public IActionResult Dequeue(string playerId)
        {
            if (playerId == string.Empty)
                return BadRequest(new { message = "PlayerId is required" });
            _service.Dequeue(playerId);
            return Ok(new { message = "Player dequeued successfully", playerId });
        }

        [HttpGet("sessions")]
        public IActionResult GetSessions([FromQuery] string? gameId = null)
        {
            var sessions = _service.GetSessions();

            if (!string.IsNullOrWhiteSpace(gameId))
                sessions = sessions.Where(s => s.GameId == gameId).ToList();

            return Ok(sessions);
        }

        [HttpGet("status/{playerId}")]
        public IActionResult GetPlayerStatus(string playerId)
        {
            if (playerId == string.Empty)
                return BadRequest(new { message = "PlayerId is required" });
            var session = _service.GetPlayerSession(playerId);

            if (session == null)
                return Ok(new { status = "waiting", message = "In queue, waiting for match" });

            return Ok(new
            {
                status = session.Status.ToString().ToLower(),
                sessionId = session.SessionId,
                players = session.Players.Select(p => p.PlayerId),
                playerCount = session.Players.Count,
                maxPlayers = session.MaxPlayers
            });
        }
        [HttpGet("ping")]
        public IActionResult Ping()
        {
            return Ok(new { status = "pong", timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() });
        }
    }
}