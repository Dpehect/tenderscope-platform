using System.Runtime.CompilerServices;
using System.Text.Json;
using TenderScope.Application.Contracts;
using TenderScope.Application.Models;
using TenderScope.Domain.Entities;

namespace TenderScope.Infrastructure.Sources;

public sealed class WorldBankProcurementSource(HttpClient httpClient, ITenderNormalizer normalizer) : ITenderSource
{
    public string Key => "world-bank-procurement";

    public async IAsyncEnumerable<Tender> FetchAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var publishedFrom = DateTime.UtcNow.AddDays(-14).ToString("yyyy-MM-dd");
        var url = $"api/procnotices?format=json&rows=100&os=0&fl=id,url,notice_type,publication_date,project_id,bid_description,procurement_category,procurement_method,deadline_date,country_code,country_name,region,sector&publication_date={Uri.EscapeDataString(publishedFrom)}";
        using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);

        foreach (var notice in EnumerateNotices(document.RootElement))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var id = Read(notice, "id", "project_id") ?? Guid.NewGuid().ToString("N");
            var description = Read(notice, "bid_description", "description") ?? "World Bank procurement notice";
            var countryCode = Read(notice, "country_code") ?? "INT";
            var countryName = Read(notice, "country_name") ?? "World Bank financed project";
            var link = Read(notice, "url") ?? $"https://projects.worldbank.org/en/projects-operations/procurement-detail/{id}";
            var category = Read(notice, "procurement_category", "sector", "notice_type");
            var raw = new RawTenderRecord(
                Key,
                id,
                description,
                countryName,
                $"{Read(notice, "notice_type")} {Read(notice, "procurement_method")}".Trim(),
                countryCode,
                Read(notice, "region"),
                category,
                null,
                null,
                Read(notice, "publication_date"),
                Read(notice, "deadline_date"),
                link);
            yield return normalizer.Normalize(raw);
        }
    }

    private static IEnumerable<JsonElement> EnumerateNotices(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array) return root.EnumerateArray().ToArray();
        foreach (var key in new[] { "procnotices", "notices", "results", "documents", "data" })
            if (root.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.Array)
                return value.EnumerateArray().ToArray();
        return [];
    }

    private static string? Read(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (!element.TryGetProperty(name, out var value)) continue;
            if (value.ValueKind == JsonValueKind.String) return value.GetString();
            if (value.ValueKind is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False) return value.ToString();
            if (value.ValueKind == JsonValueKind.Array)
            {
                var first = value.EnumerateArray().FirstOrDefault();
                if (first.ValueKind != JsonValueKind.Undefined) return first.ToString();
            }
        }
        return null;
    }
}
