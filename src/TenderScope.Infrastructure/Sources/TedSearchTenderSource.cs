using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using TenderScope.Application.Contracts;
using TenderScope.Application.Models;
using TenderScope.Domain.Entities;

namespace TenderScope.Infrastructure.Sources;

public sealed class TedSearchTenderSource(HttpClient httpClient, ITenderNormalizer normalizer) : ITenderSource
{
    public string Key => "eu-ted-search";

    public async IAsyncEnumerable<Tender> FetchAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var from = DateTime.UtcNow.AddDays(-7).ToString("yyyyMMdd");
        var request = new
        {
            query = $"publication-date >= {from}",
            fields = new[] { "publication-number", "notice-title", "buyer-name", "buyer-country", "publication-date", "deadline-date-lot", "estimated-value-lot", "estimated-value-lot-currency", "classification-cpv", "links" },
            page = 1,
            limit = 100,
            scope = "ACTIVE",
            checkQuerySyntax = false,
            paginationMode = "PAGE_NUMBER"
        };

        using var response = await httpClient.PostAsJsonAsync("v3/notices/search", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);

        var root = document.RootElement;
        JsonElement notices;
        if (!root.TryGetProperty("notices", out notices) && !root.TryGetProperty("results", out notices)) yield break;
        if (notices.ValueKind != JsonValueKind.Array) yield break;

        foreach (var notice in notices.EnumerateArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            string? Read(string name)
            {
                if (!notice.TryGetProperty(name, out var value)) return null;
                if (value.ValueKind == JsonValueKind.Array) return value.EnumerateArray().FirstOrDefault().ToString();
                return value.ValueKind == JsonValueKind.Object ? value.EnumerateObject().FirstOrDefault().Value.ToString() : value.ToString();
            }

            var id = Read("publication-number") ?? Guid.NewGuid().ToString("N");
            var link = Read("links") ?? $"https://ted.europa.eu/en/notice/-/detail/{id}";
            var raw = new RawTenderRecord(Key, id, Read("notice-title") ?? "TED procurement notice", Read("buyer-name") ?? "Contracting authority", null, Read("buyer-country"), null, Read("classification-cpv"), Read("estimated-value-lot"), Read("estimated-value-lot-currency"), Read("publication-date"), Read("deadline-date-lot"), link);
            yield return normalizer.Normalize(raw);
        }
    }
}
