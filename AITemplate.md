# AI Usage Summary

## Tools Used
- GitHub Copilot inside VS Code using Claude Opus 4.6

## How I Used It

### What I delegated to AI
- **Project scaffolding** — Setting up the C# solution structure, `.csproj` files, and Dockerfile since I hadn't done that in VS Code for a while
- **Boilerplate** — Controller setup, `Program.cs` configuration, Swagger/Swashbuckle wiring
- **Docker test fixture** — `DockerFixture` with `IAsyncLifetime` for spinning up containers in integration tests
- **NBomber load tests** — Scenario setup and load simulation configuration
- **Debugging** — Resolving package version conflicts (OpenApi vs Swashbuckle), JSON serialization issues (camelCase vs PascalCase), and xUnit collection fixture wiring

### What I wrote/designed myself
- **Architecture decisions** — Chose the separation into Api/Application layers, background worker pattern, and Dictionary with lock for thread-safe queue
- **Matchmaking logic** — created brackets dependent on latency and made sure to queue player in to sessions with other players with the equal latency.
- **API design** — Endpoint structure, request/response shapes, and validation rules
- **Test strategy** — Decided what to unit test vs integration test vs load test, and which edge cases to cover

### Where AI output was wrong and what I corrected
- **Swagger package confusion** — AI initially suggested `Microsoft.AspNetCore.OpenApi` + Scalar which caused version conflicts with .NET 9. Switched to Swashbuckle which worked
- **JSON casing** — `PostAsync` in integration tests serialized with PascalCase by default, causing 400 errors. Had to add `JsonNamingPolicy.CamelCase` explicitly

## Prompts That Mattered

### 1. "Have I implemented the entire assignment?" + assignment text
I pasted the full assignment requirements and asked AI to evaluate against them. This surfaced that **multi-game support** (`GameId`) was missing — something I had overlooked. Structuring the prompt as "evaluate against these criteria" rather than "what should I add" gave a focused, actionable answer.

### 2. "This test is failing why?" + error output + code snippet
When integration tests returned 400, I included the exact error message, the test code, and the curl output showing it worked manually. Giving AI both the failing and working case helped it pinpoint the JSON serialization mismatch quickly instead of guessing.

## What I'd Do Differently
I would start by defining the API contract (OpenAPI spec or at least endpoint signatures) before writing any code. I let the AI generate boilerplate first and then adjusted, which led to several back-and-forth cycles fixing serialization and validation issues. A contract-first approach would have caught the camelCase/PascalCase mismatch and required fields