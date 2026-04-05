# Matchmaking Service

REST API for matchmaking in multiplayer games. Players join a queue and get assigned to sessions based on latency and how long they've been waiting.

## How it works

```
Client → REST API → Queue (ConcurrentDictionary)
                       ↑
               Background Worker (every 2s)
                       ↓
                 Game Sessions
```

Players enter a queue via the API. A background worker runs every 2 seconds, picks players from the queue, scores them based on wait time and latency, and places them into sessions.

Sessions go through three states:

| Status | Players | What happens |
|---|---|---|
| `lobby` | 1–3 | Waiting for more players |
| `started` | 4–9 | Game is running, others can still join |
| `full` | 10 | No more players accepted |

When a session is full, the next player gets a new session.

## Running it

### Docker

```bash
docker-compose up -d --build
```

API runs on `http://localhost:8080`.

Swagger UI is available at [http://localhost:8080](http://localhost:8080) — you can browse and test all endpoints in the browser.



### Without Docker

```bash
dotnet restore
dotnet run --project src/MatchmakingService.Api/MatchmakingService.Api.csproj
```

## Endpoints

| Method | Endpoint | What it does |
|---|---|---|
| `GET` | `/matchmaking/ping` | Ping for getting players latency |
| `POST` | `/matchmaking/queue` | Add player to queue |
| `DELETE` | `/matchmaking/queue/{playerId}` | Remove player from queue |
| `POST` | `/matchmaking/join-or-queue` | Try to join a running session, otherwise queue |
| `GET` | `/matchmaking/sessions` | List all sessions |
| `GET` | `/matchmaking/status/{playerId}` | Check where a player is |

### Quick test

```bash
# Add four players
curl -X POST http://localhost:8080/matchmaking/queue \
  -H "Content-Type: application/json" \
  -d '{"playerId":"player1","gameId":"battle-royale","latencyMs":50}'

curl -X POST http://localhost:8080/matchmaking/queue \
  -H "Content-Type: application/json" \
  -d '{"playerId":"player3","gameId":"racing","latencyMs":60}'

curl -X POST http://localhost:8080/matchmaking/queue \
  -H "Content-Type: application/json" \
  -d '{"playerId":"player2","gameId":"battle-royale","latencyMs":45}'

curl -X POST http://localhost:8080/matchmaking/queue \
  -H "Content-Type: application/json" \
  -d '{"playerId":"player4","latencyMs":35}'

# Wait a few seconds for the worker to run, then check
curl http://localhost:8080/matchmaking/status/player1

# Get Gamesession for specific game
curl http://localhost:8080/matchmaking/sessions?gameId=battle-royale

# Get all gamesessions
curl http://localhost:8080/matchmaking/sessions
```

### Response examples

Waiting in queue:
```json
{ "status": "waiting", "message": "In queue, waiting for match" }
```

In a lobby:
```json
{ "status": "lobby", "sessionId": "abc-123", "playerCount": 2, "maxPlayers": 10 }
```

Game started:
```json
{ "status": "started", "sessionId": "abc-123", "players": ["player1","player2","player3","player4"], "playerCount": 4, "maxPlayers": 10 }
```

## Tests

```bash
dotnet test
```

There are unit tests for the service logic and controller, plus integration tests that run against the Docker container.

### Load testing

Load tests use [NBomber](https://nbomber.com/) and live in the test project. They simulate concurrent players queueing and checking status.

To run them, make sure the container is up first:

```bash
docker-compose up -d --build
dotnet test --filter "LoadTest"
```

Two scenarios:

- **Queue_ShouldHandle100ConcurrentPlayers** — ramps from 10 to 50 requests/sec over 35 seconds
- **FullFlow_ShouldHandleLoad** — 20 queue requests/sec + 30 status checks/sec for 30 seconds

NBomber generates an HTML report after each run. You can find it at:

```
~/Desktop/load-results/
```

Open it with:

```bash
open ~/Desktop/load-results/*.html
```

## Project layout

```
src/
  MatchmakingService.Api/
    Controllers/MatchmakingController.cs
    MatchmakingWorker.cs
    Program.cs
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

**ConcurrentDictionary for the queue** — gives O(1) lookups and prevents the same player from being added twice. Thread-safe without external locking.

**Background worker instead of matching on request** — keeps the API fast. The matching logic can take its time without blocking the caller.

**Scoring: `waitTime - (latencyMs * 0.01)`** — players who have waited longer get priority. Low-latency players get a small bonus since they'll have a better experience.

**Lobby → Started → Full** — lets sessions grow over time instead of requiring a fixed number of players upfront. Feels more natural for drop-in style games.

## Things I'd add with more time

- Real-time notifications via WebSockets instead of polling
- Redis for session storage so multiple instances can share state
- Rate limiting to prevent queue spam


##ASSIGNMENT
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

##GOALS
As a game server programmer, we encourage you to
showcase your best work!
-  Prioritize the architecture and apply best code practices
-  Your code and software architecture should be the main
focus.
- Feel free to extend the project to highlight your coding
skills, but be careful not to over-engineer it.