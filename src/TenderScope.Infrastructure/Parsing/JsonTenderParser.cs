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
        var items = document.RootElement.ValueKind == JsonValueKind.Array
            ? document.RootElement.EnumerateArray()
            : document.RootElement.TryGetProperty("items", out var array) ? array.EnumerateArray() : [];

        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string? Read(params string[] names)
            {
                foreach (var name in names)
                    if (item.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null)
                        return value.ToString();
                return null;
            }

            var url = Read("url", "sourceUrl", "link");
            var id = Read("id", "externalId", "noticeId") ?? url ?? Guid.NewGuid().ToString("N");
            yield return new RawTenderRecord(sourceKey, id, Read("title", "name") ?? "Untitled notice", Read("buyerName", "buyer", "organisation") ?? "Unknown buyer", Read("description", "summary"), Read("country", "countryCode"), Read("region"), Read("category", "cpv"), Read("estimatedValue", "value"), Read("currency"), Read("publishedAt", "publicationDate"), Read("deadlineAt", "deadline"), url ?? "https://example.invalid");
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
            string? Read(params string[] names) => entry.Descendants().FirstOrDefault(x => names.Contains(x.Name.LocalName, StringComparer.OrdinalIgnoreCase))?.Value.Trim();
            var url = entry.Descendants().FirstOrDefault(x => x.Name.LocalName == "link")?.Attribute("href")?.Value ?? Read("link", "url");
            var id = Read("id", "guid", "notice-id", "publication-number") ?? url ?? Guid.NewGuid().ToString("N");
            yield return new RawTenderRecord(sourceKey, id, Read("title") ?? "Untitled notice", Read("buyer", "buyer-name", "author", "organisation") ?? "Unknown buyer", Read("description", "summary", "content"), Read("country", "country-code"), Read("region", "nuts"), Read("category", "cpv"), Read("value", "estimated-value"), Read("currency"), Read("published", "pubDate", "publication-date"), Read("deadline", "deadline-date"), url ?? "https://example.invalid");
        }
    }
}
