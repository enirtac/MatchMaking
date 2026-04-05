using System;
using System.Net.Http;
using System.Text;
using NBomber.CSharp;
using Xunit;
using MatchmakingService.Tests.Utils;

namespace MatchmakingService.Tests.Load
{
    [Collection("Docker")]
    public class LoadTest
    {
        private readonly string _baseUrl;
        private static readonly string ReportFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            "load-results");

        public LoadTest(DockerFixture fixture)
        {
            _baseUrl = fixture.BaseUrl;
        }

        [Fact]
        public void Queue_ShouldHandle100ConcurrentPlayers()
        {
            using var client = new HttpClient();

            var scenario = Scenario.Create("queue_players", async context =>
            {
                var playerId = $"load_{context.ScenarioInfo.InstanceNumber}_{context.InvocationNumber}";
                var body = $"{{\"playerId\":\"{playerId}\",\"gameId\":\"flow-test\",\"latencyMs\":{Random.Shared.Next(10, 100)}}}";

                var content = new StringContent(body, Encoding.UTF8, "application/json");
                var response = await client.PostAsync($"{_baseUrl}/matchmaking/queue", content);

                return response.IsSuccessStatusCode
                    ? Response.Ok()
                    : Response.Fail();
            })
            .WithLoadSimulations(
                Simulation.RampingInject(rate: 10, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(10)),
                Simulation.Inject(rate: 50, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(20)),
                Simulation.RampingInject(rate: 0, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(5))
            );

            NBomberRunner
                .RegisterScenarios(scenario)
                .WithReportFolder(ReportFolder)
                .Run();
        }

        [Fact]
        public void FullFlow_ShouldHandleLoad()
        {
            using var client = new HttpClient();

            var queueScenario = Scenario.Create("queue", async context =>
            {
                var playerId = $"q_{context.ScenarioInfo.InstanceNumber}_{context.InvocationNumber}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
                var body = $"{{\"playerId\":\"{playerId}\",\"gameId\":\"flow-test\",\"latencyMs\":{Random.Shared.Next(10, 100)}}}";

                var content = new StringContent(body, Encoding.UTF8, "application/json");
                var response = await client.PostAsync($"{_baseUrl}/matchmaking/queue", content);

                return response.IsSuccessStatusCode ? Response.Ok() : Response.Fail();
            })
            .WithLoadSimulations(
                Simulation.Inject(rate: 20, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(30))
            );

            var statusScenario = Scenario.Create("check_status", async context =>
            {
                var playerId = $"q_{context.ScenarioInfo.InstanceNumber}_0_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";

                var response = await client.GetAsync($"{_baseUrl}/matchmaking/status/{playerId}");

                return response.IsSuccessStatusCode ? Response.Ok() : Response.Fail();
            })
            .WithLoadSimulations(
                Simulation.Inject(rate: 30, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(30))
            );

            NBomberRunner
                .RegisterScenarios(queueScenario, statusScenario)
                .WithReportFolder(ReportFolder)
                .Run();
        }
    }
}