using TechnicalScanner.Application.Models;

namespace TechnicalScanner.Application.Abstractions;

public interface IAlertChannel
{
    string ChannelName { get; }

    bool IsConfigured { get; }

    string Destination { get; }

    Task SendAsync(AlertNotification notification, CancellationToken cancellationToken);
}
