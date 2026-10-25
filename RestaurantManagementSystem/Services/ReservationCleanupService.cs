namespace RestaurantManagementSystem.Services
{
    // Runs in the background while the app is running: when it starts, and then every minute, it closes
    // Pending requests nobody answered by 15 minutes before the booking time, and bookings from earlier days
    // that are still Confirmed / Seated (ReservationService.CloseOverdueAsync), so nothing stays "awaiting".
    public class ReservationCleanupService : BackgroundService
    {
        // Every minute, so a request is declined right at its 15-minute deadline
        private static readonly TimeSpan Every = TimeSpan.FromMinutes(1);

        private readonly IServiceScopeFactory _scopes;
        private readonly ILogger<ReservationCleanupService> _logger;

        public ReservationCleanupService(IServiceScopeFactory scopes, ILogger<ReservationCleanupService> logger)
        {
            _scopes = scopes;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // ReservationService uses the database, so it needs its own scope here
                    using var scope = _scopes.CreateScope();
                    var reservations = scope.ServiceProvider.GetRequiredService<ReservationService>();
                    int closed = await reservations.CloseOverdueAsync();
                    if (closed > 0)
                        _logger.LogInformation("Closed {Count} overdue reservation(s) from earlier days.", closed);
                }
                catch (Exception ex)
                {
                    // e.g. the database isn't reachable yet – try again next time
                    _logger.LogWarning(ex, "Could not close overdue reservations.");
                }

                try { await Task.Delay(Every, stoppingToken); }
                catch (TaskCanceledException) { break; }
            }
        }
    }
}
