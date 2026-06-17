namespace TechnicalScanner.Application.Models;

public sealed record AlertNotification(
    string Title,
    string Message,
    IReadOnlyList<AlertNotificationItem> Events);

public sealed record AlertNotificationItem(
    string EventType,
    string SubjectType,
    string SubjectKey,
    string Title,
    string Message);
