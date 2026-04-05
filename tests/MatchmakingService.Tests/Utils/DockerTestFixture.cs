using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace MatchmakingService.Tests.Utils
{
    public class DockerFixture : IAsyncLifetime
    {
        public HttpClient Client { get; private set; } = null!;
        public string BaseUrl { get; } = "http://localhost:8080";
        private const int MaxRetries = 30;

        public async Task InitializeAsync()
        {
            Client = new HttpClient
            {
                BaseAddress = new Uri(BaseUrl),
                Timeout = TimeSpan.FromSeconds(5)
            };
            Console.WriteLine("Making sure that container is rebuilt.");
            RunCommand("docker-compose", "down");

            Console.WriteLine("Starting container...");
            RunCommand("docker-compose", "up -d --build");
            await WaitForApiAsync();
        }

        public async Task DisposeAsync()
        {
            Client?.Dispose();
            RunCommand("docker-compose", "down");
            await Task.CompletedTask;
        }

        private async Task<bool> IsApiRunning()
        {
            try
            {
                var response = await Client!.GetAsync("/matchmaking/ping");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        private async Task WaitForApiAsync()
        {
            for (int i = 0; i < MaxRetries; i++)
            {
                if (await IsApiRunning())
                {
                    Console.WriteLine($"API started after {i + 1} seconds");
                    return;
                }
                Console.WriteLine($"Attempt {i + 1}: waiting...");
                await Task.Delay(1000);
            }

            throw new Exception($"API did not start within {MaxRetries} seconds");
        }

        private void RunCommand(string command, string arguments)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = command,
                    Arguments = arguments,
                    WorkingDirectory = GetProjectRoot(),
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                }
            };

            process.Start();
            process.WaitForExit(120000);
        }

        private static string GetProjectRoot()
        {
            var dir = AppContext.BaseDirectory;
            while (dir != null && !File.Exists(Path.Combine(dir, "docker-compose.yml")))
            {
                dir = Directory.GetParent(dir)?.FullName;
            }
            return dir ?? throw new Exception("Could not find project root with docker-compose.yml");
        }
    }

    [CollectionDefinition("Docker")]
    public class DockerCollection : ICollectionFixture<DockerFixture> { }
}