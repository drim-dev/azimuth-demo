using Common.Time;
using MediatR;
using Microsoft.Extensions.Options;
using Payments.Features.Captures.Options;

namespace Payments.Features.Captures;

public sealed class CaptureSettlementWorker(
    IServiceScopeFactory scopes,
    IOptions<CaptureSettlementOptions> options,
    CaptureSettlementState state,
    Clock clock,
    ILogger<CaptureSettlementWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            return;
        }

        using var timer = new PeriodicTimer(options.Value.Interval);
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var sender = scope.ServiceProvider.GetRequiredService<ISender>();
                await sender.Send(new DispatchCaptures.Request(), stoppingToken);
                state.RecordSuccess(clock.Now);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                // A failed cycle must leave the worker alive so the overdue detector can expose
                // the persistent failure instead of turning one bad intent into silent shutdown.
                logger.LogError(exception, "Capture settlement cycle failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
