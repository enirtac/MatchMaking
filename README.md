# Matchmaking Service

REST API for matchmaking in multiplayer games. Players join a queue and get assigned to sessions based on latency and how long they've been waiting.

## How it works

```
Client → REST API → Queue (Dictionary + lock)
                       ↑
               Background Worker (every 500ms)
                       ↓
                 Game Sessions
```

Players enter a queue via the API. A background worker runs every 500ms, groups players by game and latency bracket, sorts them by wait time (oldest first), and places them into sessions.

### Latency brackets

Players are grouped so similar latency players play together:

| Bracket | Latency | Quality |
|---|---|---|
| 0 | 0–49ms | Very good |
| 1 | 50–99ms | Good |
| 2 | 100–149ms | OK |
| 3 | 150ms+ | Poor |

Within each bracket, players who have waited longest are placed first.

### Session lifecycle

| Status | Players | What happens |
|---|---|---|
| `lobby` | 1–3 | Waiting for more players |
| `started` | 4–9 | Game is running, others can still join via `join-or-queue` |
| `full` | 10 | No more players accepted |

When a session is full, the next player gets a new session.

## Running it

### Docker

```bash
docker-compose up -d --build
```

API runs on `http://localhost:8080`.

Swagger UI is available at [http://localhost:8080](http://localhost:8080).

### Without Docker

```bash
dotnet restore
dotnet run --project src/MatchmakingService.Api/MatchmakingService.Api.csproj
```

### Current limitations for horizontal scaling

The current implementation uses in-memory storage, so each pod has its own queue and sessions. For production with multiple replicas:

| Concern | Solution |
|---|---|
| Shared state | Redis for queue and sessions |
| Single worker | Distributed lock so only one pod processes the queue |
| Persistence | PostgreSQL or similiar for data that we need to persist |
| Monitoring | Some kind of monetoring for cluster

## Endpoints

| Method | Endpoint | What it does |
|---|---|---|
| `GET` | `/matchmaking/ping` | Health check / latency measurement |
| `POST` | `/matchmaking/queue` | Add player to queue |
| `DELETE` | `/matchmaking/queue/{playerId}` | Remove player from queue |
| `POST` | `/matchmaking/join-or-queue` | Try to join a running session, otherwise queue |
| `GET` | `/matchmaking/sessions` | List all sessions, could be used in admin tool |
| `GET` | `/matchmaking/sessions?gameId=X` | List sessions for a specific game, could be used in admin tool |
| `GET` | `/matchmaking/status/{playerId}` | Check where a player is |

### Error handling

| Scenario | Response |
|---|---|
| Missing/invalid fields | `400 Bad Request` (via data annotations) |
| Player already in queue or session | `409 Conflict` |
| Dequeue player not in queue | `404 Not Found` |
| Empty playerId on status/dequeue | `400 Bad Request` |
| Unknown player status | `200 OK` with `"status": "unknown"` |
| Unexpected server error | `500` with JSON error message |

### Quick test

```bash
# Add players to different games
curl -X POST http://localhost:8080/matchmaking/queue \
  -H "Content-Type: application/json" \
  -d '{"playerId":"player1","gameId":"battle-royale","latencyMs":50}'

curl -X POST http://localhost:8080/matchmaking/queue \
  -H "Content-Type: application/json" \
  -d '{"playerId":"player2","gameId":"battle-royale","latencyMs":45}'

curl -X POST http://localhost:8080/matchmaking/queue \
  -H "Content-Type: application/json" \
  -d '{"playerId":"player3","gameId":"racing","latencyMs":60}'

curl -X POST http://localhost:8080/matchmaking/queue \
  -H "Content-Type: application/json" \
  -d '{"playerId":"player4","gameId":"battle-royale","latencyMs":35}'

# Wait a second for the worker to run, then check
curl http://localhost:8080/matchmaking/status/player1

# Get sessions for a specific game
curl http://localhost:8080/matchmaking/sessions?gameId=battle-royale

# Get all sessions
curl http://localhost:8080/matchmaking/sessions
```

### Response examples

Player status responses always return the same shape:

```json
// unknown — player not found
{ "status": "unknown", "sessionId": null, "players": null, "playerCount": 0, "maxPlayers": 0 }

// queued — in queue, waiting for matchmaking
{ "status": "queued", "sessionId": null, "players": null, "playerCount": 0, "maxPlayers": 0 }

// lobby — in session, waiting for more players
{ "status": "lobby", "sessionId": "abc-123", "players": ["p1","p2"], "playerCount": 2, "maxPlayers": 10 }

// started — game is running
{ "status": "started", "sessionId": "abc-123", "players": ["p1","p2","p3","p4"], "playerCount": 4, "maxPlayers": 10 }

// full — session is full
{ "status": "full", "sessionId": "abc-123", "players": ["p1","...","p10"], "playerCount": 10, "maxPlayers": 10 }
```


## Tests

```bash
# Run all tests 
dotnet test
```

- **Unit tests** — matchmaking logic (service) and controller with mocked dependencies
- **Integration tests** — run against the Docker container
- **Load tests** — NBomber-based, Results are saved to `~/Desktop/load-results/`.


## Project layout

```
src/
  MatchmakingService.Api/
    Controllers/MatchmakingController.cs
    Properties/
      launchSettings.json          — Dev server settings (ports, environment)
    MatchmakingWorker.cs
    MatchmakingService.Api.http    — HTTP request examples for testing in VS/Rider
    Program.cs
    appsettings.json               — App configuration
  MatchmakingService.Application/
    Models/
      GameSession.cs
      PlayerQueueEntry.cs
      SessionStatus.cs
    Services/
      IMatchmakingService.cs
      MatchmakingService.cs
tests/
  MatchmakingService.Tests/
    Controllers/
    Services/
    Integration/
    LoadTest/
```

## Why I made certain choices

**Dictionary + lock instead of ConcurrentDictionary** — all operations need to be atomic across multiple dictionaries (e.g. adding a player to a session and updating the session map). A single lock is simpler and safer than coordinating multiple concurrent collections.

**Four dictionaries for O(1) lookups everywhere** — `_queue` for queued players, `_sessionsById` for session lookup, `_playerSessionMap` for player → session mapping, `_openSessionByGame` for finding the current open session per game/bracket. No scanning.

**Latency brackets for matchmaking** — players are grouped by latency range so similar-quality connections play together. Within each bracket, longest-waiting players are placed first.

**Background worker instead of matching on request** — keeps the API fast. The matching logic runs every 500ms without blocking the caller.

**Data annotations for input validation** — `[Required]`, `[Range]` on the model. `[ApiController]` handles 400 responses automatically — no manual validation needed in the controller.

**Lobby → Started → Full** — lets sessions grow over time instead of requiring a fixed number of players upfront.

## Things I'd add with more time

- Redis for shared state across multiple pods
- WebSocket notifications instead of polling
- Rate limiting to prevent queue spam
- Session cleanup for inactive sessions
- Expanding latency tolerance based on wait time (strict at first, relaxes over time)

## Assignment
Make your own matchmaking service!
Design and develop a custom matchmaking service using
C# to create a scalable REST architecture capable of
supporting multiple games and millions of players.
The service must include the following functionalities:
- Queue requests from players to join a session.
- Dequeue previous player requests.
- Assign a session to the player based primarily on their
latency and time spent queuing (player skill rating is not
a factor).
- (Optional): Allow players to join a session that has
already started.

## GOALS
As a game server programmer, we encourage you to
showcase your best work!
-  Prioritize the architecture and apply best code practices
-  Your code and software architecture should be the main
focus.
- Feel free to extend the project to highlight your coding
skills, but be careful not to over-engineer it.