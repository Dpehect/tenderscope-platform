using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Xml.Linq;
using TenderScope.Application.Contracts;
using TenderScope.Application.Models;

namespace TenderScope.Infrastructure.Parsing;

public sealed class JsonTenderParser : ITenderParser
{
    public string Format => "json";

    public async IAsyncEnumerable<RawTenderRecord> ParseAsync(Stream content, string sourceKey, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var document = await JsonDocument.ParseAsync(content, cancellationToken: cancellationToken);
        JsonElement collection;
        if (document.RootElement.ValueKind == JsonValueKind.Array) collection = document.RootElement;
        else if (document.RootElement.TryGetProperty("items", out var items)) collection = items;
        else if (document.RootElement.TryGetProperty("results", out var results)) collection = results;
        else if (document.RootElement.TryGetProperty("notices", out var notices)) collection = notices;
        else yield break;
        if (collection.ValueKind != JsonValueKind.Array) yield break;

        foreach (var item in collection.EnumerateArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            string? Read(params string[] names)
            {
                foreach (var name in names)
                    if (item.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null)
                        return value.ValueKind == JsonValueKind.Array ? value.EnumerateArray().FirstOrDefault().ToString() : value.ToString();
                return null;
            }

            var url = Read("url", "sourceUrl", "link", "links");
            var id = Read("id", "externalId", "noticeId", "publication-number") ?? url ?? Guid.NewGuid().ToString("N");
            yield return new RawTenderRecord(sourceKey, id, Read("title", "name", "notice-title") ?? "Untitled notice", Read("buyerName", "buyer", "organisation", "buyer-name") ?? "Unknown buyer", Read("description", "summary"), Read("country", "countryCode", "buyer-country"), Read("region"), Read("category", "cpv", "classification-cpv"), Read("estimatedValue", "value", "estimated-value-lot"), Read("currency", "estimated-value-lot-currency"), Read("publishedAt", "publicationDate", "publication-date"), Read("deadlineAt", "deadline", "deadline-date-lot"), url ?? "https://example.invalid");
        }
    }
}

public sealed class XmlTenderParser : ITenderParser
{
    public string Format => "xml";

    public async IAsyncEnumerable<RawTenderRecord> ParseAsync(Stream content, string sourceKey, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var document = await XDocument.LoadAsync(content, LoadOptions.None, cancellationToken);
        var entries = document.Descendants().Where(x => x.Name.LocalName is "item" or "entry" or "notice");
        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string? Read(params string[] names) => entry.Descendants().FirstOrDefault(x => names.Contains(x.Name.LocalName, StringComparer.OrdinalIgnoreCase))?.Value.Trim();
            var url = entry.Descendants().FirstOrDefault(x => x.Name.LocalName == "link")?.Attribute("href")?.Value ?? Read("link", "url");
            var id = Read("id", "guid", "notice-id", "publication-number") ?? url ?? Guid.NewGuid().ToString("N");
            yield return new RawTenderRecord(sourceKey, id, Read("title") ?? "Untitled notice", Read("buyer", "buyer-name", "author", "organisation") ?? "Unknown buyer", Read("description", "summary", "content"), Read("country", "country-code"), Read("region", "nuts"), Read("category", "cpv"), Read("value", "estimated-value"), Read("currency"), Read("published", "pubDate", "publication-date"), Read("deadline", "deadline-date"), url ?? "https://example.invalid");
        }
    }
}
