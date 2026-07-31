namespace TenderScope.Application.Models;

public sealed record RawTenderRecord(
    string SourceKey,
    string ExternalId,
    string Title,
    string BuyerName,
    string? Description,
    string? Country,
    string? Region,
    string? Category,
    string? EstimatedValue,
    string? Currency,
    string? PublishedAt,
    string? DeadlineAt,
    string SourceUrl,
    IReadOnlyDictionary<string, string>? Metadata = null);
