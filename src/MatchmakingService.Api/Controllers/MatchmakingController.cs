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
            if (_service.IsPlayerInQueue(player.PlayerId) || _service.GetPlayerSession(player.PlayerId) != null)
                return Conflict(new { error = "Player is already in queue or session", playerId = player.PlayerId });

            _service.Enqueue(player);
            return Ok(new { status = "queued", message = "Player queued successfully", playerId = player.PlayerId });
        }
        [HttpPost("join-or-queue")]
        public IActionResult JoinOrQueue([FromBody] PlayerQueueEntry player)
        {
            if (_service.IsPlayerInQueue(player.PlayerId) || _service.GetPlayerSession(player.PlayerId) != null)
                return Conflict(new { error = "Player is already in queue or session", playerId = player.PlayerId });

            var session = _service.TryJoinExistingSession(player);

            if (session != null)
            {
                return Ok(new { status = "joined", sessionId = session.SessionId, playerId = player.PlayerId });
            }

            _service.Enqueue(player);

            return Ok(new { status = "queued", playerId = player.PlayerId });
        }
        [HttpDelete("queue/{playerId}")]
        public IActionResult Dequeue([FromRoute] string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId))
                return BadRequest(new { error = "playerId is required" });

            if (!_service.IsPlayerInQueue(playerId))
                return NotFound(new { error = "Player not found in queue", playerId });

            _service.Dequeue(playerId);
            return NoContent();
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
            if (string.IsNullOrWhiteSpace(playerId))
                return BadRequest(new { error = "playerId is required" });

            var session = _service.GetPlayerSession(playerId);
            var inQueue = _service.IsPlayerInQueue(playerId);

            var status = session != null
                ? session.Status.ToString().ToLowerInvariant()
                : inQueue ? "queued" : "unknown";

            return Ok(new
            {
                status,
                sessionId = session?.SessionId,
                players = session?.Players.Select(p => p.PlayerId),
                playerCount = session?.Players.Count ?? 0,
                maxPlayers = session?.MaxPlayersPerSession ?? 0
            });
        }

        [HttpGet("ping")]
        public IActionResult Ping()
        {
            return Ok(new { status = "pong", timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() });
        }
    }


}