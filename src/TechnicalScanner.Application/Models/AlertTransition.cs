namespace TechnicalScanner.Application.Models;

public sealed record AlertTransition(
    string EventType,
    string SubjectType,
    string SubjectKey,
    string Title,
    string Message,
    string? DetailsJson);
