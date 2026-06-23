namespace PlateformePFA.API.Services
{
    /// <summary>
    /// Runs the escalation sweep at startup and every 15 minutes. Skipped under
    /// the Testing environment so it never races test fixtures.
    /// ponytail: a fixed 15-min timer, not a configurable schedule — add config
    /// only if a real SLA cadence is required.
    /// </summary>
    public class CaseEscalationWorker : BackgroundService
    {
        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);
        private readonly IServiceScopeFactory _scopes;
        private readonly IHostEnvironment _env;
        private readonly ILogger<CaseEscalationWorker> _logger;

        public CaseEscalationWorker(IServiceScopeFactory scopes, IHostEnvironment env, ILogger<CaseEscalationWorker> logger)
        {
            _scopes = scopes; _env = env; _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (_env.IsEnvironment("Testing")) return;

            using var timer = new PeriodicTimer(Interval);
            try
            {
                do
                {
                    try
                    {
                        using var scope = _scopes.CreateScope();
                        var svc = scope.ServiceProvider.GetRequiredService<CaseEscalationService>();
                        await svc.SweepAsync(DateTime.UtcNow, stoppingToken);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        _logger.LogWarning(ex, "Échec du balayage d'escalade automatique.");
                    }
                } while (await timer.WaitForNextTickAsync(stoppingToken));
            }
            catch (OperationCanceledException) { /* shutdown */ }
        }
    }
}