using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using MatchmakingService.Tests.Utils;

namespace MatchmakingService.Tests.Integration
{
    [Collection("Docker")]
    public class MatchmakingIntegrationTests
    {
        private readonly HttpClient _client;

        public MatchmakingIntegrationTests(DockerFixture fixture)
        {
            _client = fixture.Client ?? throw new InvalidOperationException("Docker fixture client is null");

        }

        // === Ping ===

        [Fact(Timeout = 30000)]
        public async Task Ping_ShouldReturnOk()
        {
            var response = await _client.GetAsync("/matchmaking/ping");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("pong");
        }

        // === Queue ===

        [Fact(Timeout = 30000)]
        public async Task Enqueue_ShouldReturnOk()
        {
            var player = new { playerId = $"queue_{Guid.NewGuid():N}", latencyMs = 50, gameId = "TestGame" };

            var response = await PostAsync("/matchmaking/queue", player);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("queued");
        }

        [Fact(Timeout = 30000)]
        public async Task Enqueue_InvalidPlayer_ShouldReturnBadRequest()
        {
            var response = await PostAsync("/matchmaking/queue", new { playerId = "", latencyMs = 50, gameId = "TestGame" });

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        // === Dequeue ===

        [Fact(Timeout = 30000)]
        public async Task Dequeue_ShouldReturnOk()
        {
            var playerId = $"dequeue_{Guid.NewGuid():N}";
            await PostAsync("/matchmaking/queue", new { playerId, latencyMs = 50, gameId = "TestGame" });

            var response = await _client.DeleteAsync($"/matchmaking/queue/{playerId}");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        // === Sessions ===

        [Fact(Timeout = 30000)]
        public async Task GetSessions_ShouldReturnOk()
        {
            var response = await _client.GetAsync("/matchmaking/sessions");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        // === JoinOrQueue ===

        [Fact(Timeout = 30000)]
        public async Task JoinOrQueue_ShouldReturnOkWithStatus()
        {
            var player = new { playerId = $"joq_{Guid.NewGuid():N}", latencyMs = 50, gameId = "TestGame" };

            var response = await PostAsync("/matchmaking/join-or-queue", player);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Match(c => c.Contains("queued") || c.Contains("joined"));
        }

        // === Status ===

        [Fact(Timeout = 30000)]
        public async Task GetPlayerStatus_WhenNeverQueued_ShouldReturnWaiting()
        {
            var response = await _client.GetAsync($"/matchmaking/status/nonexistent_{Guid.NewGuid():N}");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("waiting");
        }

        [Fact(Timeout = 30000)]
        public async Task GetPlayerStatus_WhenQueued_ShouldReturnValidStatus()
        {
            var playerId = $"status_{Guid.NewGuid():N}";
            await PostAsync("/matchmaking/queue", new { playerId, latencyMs = 50, gameId = "TestGame" });

            await Task.Delay(3000);

            var response = await _client.GetAsync($"/matchmaking/status/{playerId}");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var content = await response.Content.ReadAsStringAsync();
            content.Should().NotContain("waiting");
        }

        // === Full matchmaking flow ===

        [Fact(Timeout = 30000)]
        public async Task FullFlow_4PlayersQueue_AllShouldBeInSession()
        {
            var playerIds = new List<string>();
            for (int i = 0; i < 4; i++)
            {
                var playerId = $"flow_{Guid.NewGuid():N}";
                playerIds.Add(playerId);
                var response = await PostAsync("/matchmaking/queue", new { playerId, latencyMs = 30 + i * 10, gameId = "TestGame" });
                response.StatusCode.Should().Be(HttpStatusCode.OK);
            }

            await Task.Delay(3000);
            foreach (var playerId in playerIds)
            {
                var statusResponse = await _client.GetAsync($"/matchmaking/status/{playerId}");
                statusResponse.StatusCode.Should().Be(HttpStatusCode.OK);
                var content = await statusResponse.Content.ReadAsStringAsync();
                content.Should().NotContain("waiting",
                    because: $"{playerId} should be in a session after matchmaking");
            }
        }

        [Fact(Timeout = 30000)]
        public async Task FullFlow_PlayerJoinsViaJoinOrQueue_ShouldNotBeWaiting()
        {
            for (int i = 0; i < 4; i++)
            {
                await PostAsync("/matchmaking/queue", new { playerId = $"setup_{Guid.NewGuid():N}", latencyMs = 50, gameId = "TestGame" });
            }
            await Task.Delay(3000);

            var newPlayerId = $"joiner_{Guid.NewGuid():N}";
            var joinResponse = await PostAsync("/matchmaking/join-or-queue", new { playerId = newPlayerId, latencyMs = 45, gameId = "TestGame" });
            joinResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var joinContent = await joinResponse.Content.ReadAsStringAsync();
            joinContent.Should().Match(c => c.Contains("joined") || c.Contains("queued"));
        }

        // === Helpers ===

        private async Task<HttpResponseMessage> PostAsync(string url, object body)
        {
            var json = JsonSerializer.Serialize(body);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            return await _client.PostAsync(url, content);
        }
    }
}