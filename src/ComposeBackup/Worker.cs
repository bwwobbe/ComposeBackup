using Microsoft.Extensions.Options;

namespace ComposeBackup;

public class Worker : BackgroundService
{
    readonly ILogger<Worker> _logger;
    readonly Settings _settings;
    public Worker(ILogger<Worker> logger, IOptions<Settings> settings)
    {
        _logger = logger;
        _settings = settings.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(_settings));
        while (!stoppingToken.IsCancellationRequested)
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
            }
            await Task.Delay(1000, stoppingToken);
        }
    }
}