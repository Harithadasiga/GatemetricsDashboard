using GatemetricsDashboard.DomainLayer;
using GatemetricsDashboard.RepositoryLayer;

namespace GateMetrics.Services
{
    public class GateSensorEventService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<GateSensorEventService> _logger;
        private readonly string[] _gates = ["Gate A", "Gate B", "Gate C"];
        private readonly string[] _types = ["enter", "leave"];
        private readonly Random _random = new();

        public GateSensorEventService(IServiceProvider serviceProvider, ILogger<GateSensorEventService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<GateMetricsDbContext>();

                    var gateEvent = new GateAccessEvent
                    {
                        Gate = _gates[_random.Next(_gates.Length)],
                        Timestamp = DateTime.UtcNow,
                        NumberOfPeople = _random.Next(1, 20),
                        Type = _types[_random.Next(_types.Length)]
                    };

                    db.GateAccessEvents.Add(gateEvent);
                    await db.SaveChangesAsync(stoppingToken);

                    _logger.LogInformation("Gate event added: {@GateEvent}", gateEvent);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while adding gate event.");
                }

                await Task.Delay(1000, stoppingToken); // Simulate 1 event per second
            }
        }
    }
}

