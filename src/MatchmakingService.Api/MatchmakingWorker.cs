using Microsoft.Extensions.Hosting;
using MatchmakingService.Application.Services;

public class MatchmakingWorker : BackgroundService
{
    private readonly IMatchmakingService _service;

    public MatchmakingWorker(IMatchmakingService service)
    {
        _service = service;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            _service.RunMatchmaking();
            await Task.Delay(500, stoppingToken);
        }
    }
}