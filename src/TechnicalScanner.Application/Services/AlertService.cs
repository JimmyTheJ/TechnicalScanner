using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TechnicalScanner.Application.Abstractions;
using TechnicalScanner.Application.Alerts;
using TechnicalScanner.Application.Models;
using TechnicalScanner.Application.Options;
using TechnicalScanner.Domain.Entities;

namespace TechnicalScanner.Application.Services;

public sealed class AlertService(
    ITechnicalScannerDbContext dbContext,
    IEnumerable<IAlertChannel> channels,
    IOptions<AlertsOptions> options,
    ILogger<AlertService> logger)
{
    public async Task SendTestNotificationAsync(CancellationToken cancellationToken)
    {
        var configuredChannels = channels.Where(c => c.IsConfigured).ToList();
        if (configuredChannels.Count == 0)
        {
            throw new InvalidOperationException(
                "No alert channels are configured. Copy appsettings.Local.json.example to appsettings.Local.json and set your email credentials.");
        }

        var notification = new AlertNotification(
            "Technical Scanner test alert",
            "If you received this message, alert delivery is configured correctly.",
            [
                new AlertNotificationItem(
                    "Test",
                    "System",
                    "test",
                    "Technical Scanner test alert",
                    "Test notification from Technical Scanner.")
            ]);

        foreach (var channel in configuredChannels)
        {
            await channel.SendAsync(notification, cancellationToken);
            logger.LogInformation("Test alert sent via {Channel} to {Destination}.", channel.ChannelName, channel.Destination);
        }
    }

    public async Task<int> ProcessTransitionsAsync(long scanRunId, CancellationToken cancellationToken)
    {
        var alertOptions = options.Value;
        if (!alertOptions.Enabled)
        {
            logger.LogInformation("Alerts are disabled; skipping transition processing for scan {ScanRunId}.", scanRunId);
            return 0;
        }

        var previousScanRunId = dbContext.ScanRuns
            .Where(r => r.Id < scanRunId && r.Status == "Completed")
            .OrderByDescending(r => r.Id)
            .Select(r => (long?)r.Id)
            .FirstOrDefault();

        if (previousScanRunId is null)
        {
            logger.LogInformation("No prior completed scan found; skipping alerts for first run {ScanRunId}.", scanRunId);
            return 0;
        }

        var transitions = DetectAllTransitions(scanRunId, previousScanRunId.Value, alertOptions);
        if (transitions.Count == 0)
        {
            logger.LogInformation("No alert transitions detected for scan {ScanRunId}.", scanRunId);
            return 0;
        }

        var deduped = FilterDuplicates(transitions, alertOptions.MinMinutesBetweenDuplicates);
        if (deduped.Count == 0)
        {
            logger.LogInformation("All {Count} transitions were deduplicated for scan {ScanRunId}.", transitions.Count, scanRunId);
            return 0;
        }

        var detectedUtc = DateTimeOffset.UtcNow;
        var events = deduped.Select(transition => new AlertEvent
        {
            ScanRunId = scanRunId,
            EventType = transition.EventType,
            SubjectType = transition.SubjectType,
            SubjectKey = transition.SubjectKey,
            Title = transition.Title,
            Message = transition.Message,
            DetailsJson = transition.DetailsJson,
            DetectedUtc = detectedUtc
        }).ToList();

        foreach (var alertEvent in events)
        {
            dbContext.Add(alertEvent);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var notification = BuildNotification(events);
        await DispatchAsync(notification, events, cancellationToken);

        logger.LogInformation(
            "Processed {Count} alert transitions for scan {ScanRunId} ({Deduped} after dedup).",
            transitions.Count,
            scanRunId,
            deduped.Count);

        return deduped.Count;
    }

    private IReadOnlyList<AlertTransition> DetectAllTransitions(
        long currentScanRunId,
        long previousScanRunId,
        AlertsOptions alertOptions)
    {
        var currentSignals = LoadSignalSnapshots(currentScanRunId);
        var previousSignals = LoadSignalSnapshots(previousScanRunId);
        var currentScores = LoadScoreSnapshots(currentScanRunId);
        var previousScores = LoadScoreSnapshots(previousScanRunId);
        var currentBreadth = LoadBreadthSnapshots(currentScanRunId);
        var previousBreadth = LoadBreadthSnapshots(previousScanRunId);

        return
        [
            ..AlertTransitionDetector.DetectSignalTransitions(currentSignals, previousSignals, alertOptions),
            ..AlertTransitionDetector.DetectScoreTransitions(currentScores, previousScores, alertOptions),
            ..AlertTransitionDetector.DetectProfileTransitions(currentScores, previousScores, alertOptions),
            ..AlertTransitionDetector.DetectBreadthTransitions(currentBreadth, previousBreadth, alertOptions)
        ];
    }

    private List<SignalSnapshot> LoadSignalSnapshots(long scanRunId)
        => dbContext.SignalResults
            .Where(r => r.ScanRunId == scanRunId)
            .Join(
                dbContext.Tickers,
                result => result.TickerId,
                ticker => ticker.Id,
                (result, ticker) => new { result, ticker })
            .Join(
                dbContext.SignalDefinitions,
                x => x.result.SignalDefinitionId,
                signal => signal.Id,
                (x, signal) => new SignalSnapshot(
                    x.ticker.Symbol,
                    signal.Code,
                    signal.Name,
                    x.result.IsMatch))
            .ToList();

    private List<ScoreSnapshot> LoadScoreSnapshots(long scanRunId)
        => dbContext.ScanScores
            .Where(s => s.ScanRunId == scanRunId)
            .Join(
                dbContext.Tickers,
                score => score.TickerId,
                ticker => ticker.Id,
                (score, ticker) => new ScoreSnapshot(ticker.Symbol, score.ScoreCode, score.Score))
            .ToList();

    private List<BreadthSnapshotView> LoadBreadthSnapshots(long scanRunId)
        => dbContext.BreadthSnapshots
            .Where(b => b.ScanRunId == scanRunId && b.UniverseName == "ALL")
            .Select(b => new BreadthSnapshotView(b.MetricCode, b.MatchPct, b.MatchCount, b.TotalValidStocks))
            .ToList();

    private List<AlertTransition> FilterDuplicates(IReadOnlyList<AlertTransition> transitions, int minMinutes)
    {
        if (minMinutes <= 0)
        {
            return transitions.ToList();
        }

        var cutoff = DateTimeOffset.UtcNow.AddMinutes(-minMinutes);
        var recentKeys = dbContext.AlertEvents
            .Where(e => e.DetectedUtc >= cutoff)
            .Select(e => new { e.EventType, e.SubjectType, e.SubjectKey })
            .AsEnumerable()
            .Select(e => (e.EventType, e.SubjectType, e.SubjectKey))
            .ToHashSet();

        return transitions
            .Where(t => !recentKeys.Contains((t.EventType, t.SubjectType, t.SubjectKey)))
            .ToList();
    }

    private static AlertNotification BuildNotification(IReadOnlyList<AlertEvent> events)
    {
        var title = events.Count == 1
            ? events[0].Title
            : $"Technical Scanner: {events.Count} alerts";

        var message = string.Join(
            Environment.NewLine,
            events.Select(e => $"• {e.Message}"));

        var items = events
            .Select(e => new AlertNotificationItem(
                e.EventType,
                e.SubjectType,
                e.SubjectKey,
                e.Title,
                e.Message))
            .ToList();

        return new AlertNotification(title, message, items);
    }

    private async Task DispatchAsync(
        AlertNotification notification,
        IReadOnlyList<AlertEvent> events,
        CancellationToken cancellationToken)
    {
        var configuredChannels = channels.Where(c => c.IsConfigured).ToList();
        if (configuredChannels.Count == 0)
        {
            logger.LogWarning("No alert channels are configured; {Count} events were stored only.", events.Count);
            return;
        }

        foreach (var channel in configuredChannels)
        {
            string status;
            string? error = null;
            try
            {
                await channel.SendAsync(notification, cancellationToken);
                status = "Sent";
            }
            catch (Exception ex)
            {
                status = "Failed";
                error = ex.Message;
                logger.LogError(ex, "Failed to send alerts via {Channel}.", channel.ChannelName);
            }

            var attemptedUtc = DateTimeOffset.UtcNow;
            foreach (var alertEvent in events)
            {
                dbContext.Add(new AlertDelivery
                {
                    AlertEventId = alertEvent.Id,
                    Channel = channel.ChannelName,
                    Destination = channel.Destination,
                    Status = status,
                    ErrorMessage = error,
                    AttemptedUtc = attemptedUtc
                });
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
