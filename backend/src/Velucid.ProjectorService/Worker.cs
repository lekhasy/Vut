/// <summary>
/// Bare-minimum background worker for the projector service.
/// Keeps the pod alive in K3s until real projection logic is added.
/// </summary>
public sealed class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;

    public Worker(ILogger<Worker> logger) => _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Projector service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogDebug("Projector service heartbeat at {Time}", DateTimeOffset.UtcNow);
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }

        _logger.LogInformation("Projector service stopping");
    }
}
