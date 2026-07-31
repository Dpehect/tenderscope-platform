using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using TenderScope.Application.Contracts;
using TenderScope.Domain.Entities;

namespace TenderScope.Infrastructure.Sources;

public sealed class DemoTenderSource : ITenderSource
{
    public string Key => "demo-open-source";

    public async IAsyncEnumerable<Tender> FetchAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var items = new[]
        {
            ("TS-001", "Municipal digital services platform", "City of Example", "TR", "Software", 125000m, "EUR"),
            ("TS-002", "Hospital information security assessment", "Regional Health Authority", "PT", "Cybersecurity", 80000m, "EUR"),
            ("TS-003", "Public transport data dashboard", "Metropolitan Transit Agency", "DE", "Data", 210000m, "EUR")
        };

        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var tender = new Tender
            {
                ExternalId = item.Item1,
                SourceKey = Key,
                Title = item.Item2,
                BuyerName = item.Item3,
                CountryCode = item.Item4,
                Category = item.Item5,
                EstimatedValue = item.Item6,
                Currency = item.Item7,
                Description = "Deterministic seed record used to validate the complete ingestion pipeline.",
                PublishedAt = DateTimeOffset.UtcNow.Date.AddDays(-1),
                DeadlineAt = DateTimeOffset.UtcNow.Date.AddDays(30),
                SourceUrl = new Uri($"https://example.org/tenders/{item.Item1.ToLowerInvariant()}")
            };
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{tender.Title}|{tender.BuyerName}|{tender.DeadlineAt:O}")));
            tender.MarkObserved(hash, DateTimeOffset.UtcNow);
            yield return tender;
            await Task.Yield();
        }
    }
}
