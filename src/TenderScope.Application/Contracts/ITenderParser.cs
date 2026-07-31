using TenderScope.Application.Models;

namespace TenderScope.Application.Contracts;

public interface ITenderParser
{
    string Format { get; }
    IAsyncEnumerable<RawTenderRecord> ParseAsync(Stream content, string sourceKey, CancellationToken cancellationToken);
}

public interface ITenderNormalizer
{
    TenderScope.Domain.Entities.Tender Normalize(RawTenderRecord record);
}

public interface IDuplicateDetector
{
    string CreateFingerprint(RawTenderRecord record);
    double Similarity(string left, string right);
}
