using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using TenderScope.Application.Contracts;
using TenderScope.Application.Models;
using TenderScope.Domain.Entities;

namespace TenderScope.Infrastructure.Normalization;

public sealed partial class TenderNormalizer(IDuplicateDetector duplicateDetector) : ITenderNormalizer
{
    private static readonly Dictionary<string, string> CountryAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["TURKEY"] = "TR", ["TÜRKIYE"] = "TR", ["TÜRKİYE"] = "TR", ["PORTUGAL"] = "PT",
        ["GERMANY"] = "DE", ["DEUTSCHLAND"] = "DE", ["FRANCE"] = "FR", ["SPAIN"] = "ES",
        ["ITALY"] = "IT", ["NETHERLANDS"] = "NL", ["BELGIUM"] = "BE", ["LUXEMBOURG"] = "LU"
    };

    public Tender Normalize(RawTenderRecord record)
    {
        var title = Clean(record.Title);
        var buyer = Clean(record.BuyerName);
        var description = string.IsNullOrWhiteSpace(record.Description) ? null : Clean(record.Description);
        var country = NormalizeCountry(record.Country);
        var currency = NormalizeCurrency(record.Currency);
        var value = ParseMoney(record.EstimatedValue);
        var published = ParseDate(record.PublishedAt) ?? DateTimeOffset.UtcNow;
        var deadline = ParseDate(record.DeadlineAt);
        var url = Uri.TryCreate(record.SourceUrl, UriKind.Absolute, out var parsedUrl) ? parsedUrl : new Uri("https://example.invalid");

        var tender = new Tender
        {
            ExternalId = record.ExternalId.Trim(), SourceKey = record.SourceKey.Trim(), Title = title,
            BuyerName = buyer, CountryCode = country, Region = CleanNullable(record.Region),
            Description = description, Category = NormalizeCategory(record.Category, title, description),
            EstimatedValue = value, Currency = currency, PublishedAt = published,
            DeadlineAt = deadline, SourceUrl = url
        };
        tender.MarkObserved(duplicateDetector.CreateFingerprint(record), DateTimeOffset.UtcNow);
        return tender;
    }

    private static string NormalizeCountry(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "XX";
        var candidate = Clean(value).ToUpperInvariant();
        if (candidate.Length == 2) return candidate;
        return CountryAliases.GetValueOrDefault(candidate, "XX");
    }

    private static string? NormalizeCurrency(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var candidate = value.Trim().ToUpperInvariant();
        return candidate switch { "€" or "EURO" or "EUROS" => "EUR", "$" or "USDOLLAR" => "USD", "₺" or "TL" or "TRY" => "TRY", "£" => "GBP", _ when candidate.Length == 3 => candidate, _ => null };
    }

    private static decimal? ParseMoney(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = MoneyNoise().Replace(value, string.Empty).Replace(" ", string.Empty);
        if (normalized.Count(x => x == ',') == 1 && !normalized.Contains('.')) normalized = normalized.Replace(',', '.');
        else normalized = normalized.Replace(",", string.Empty);
        return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var result) ? result : null;
    }

    private static DateTimeOffset? ParseDate(string? value) => DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date) ? date.ToUniversalTime() : null;
    private static string NormalizeCategory(string? category, params string?[] text)
    {
        if (!string.IsNullOrWhiteSpace(category)) return Clean(category);
        var haystack = string.Join(' ', text).ToLowerInvariant();
        if (haystack.Contains("software") || haystack.Contains("digital") || haystack.Contains("information system")) return "Software";
        if (haystack.Contains("construction") || haystack.Contains("building")) return "Construction";
        if (haystack.Contains("health") || haystack.Contains("hospital") || haystack.Contains("medical")) return "Healthcare";
        if (haystack.Contains("energy") || haystack.Contains("electric")) return "Energy";
        return "General";
    }

    private static string Clean(string value) => Whitespace().Replace(System.Net.WebUtility.HtmlDecode(value).Trim(), " ");
    private static string? CleanNullable(string? value) => string.IsNullOrWhiteSpace(value) ? null : Clean(value);

    [GeneratedRegex(@"[^0-9,\.\-]")]
    private static partial Regex MoneyNoise();
    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}

public sealed class DuplicateDetector : IDuplicateDetector
{
    public string CreateFingerprint(RawTenderRecord record)
    {
        var canonical = $"{Normalize(record.Title)}|{Normalize(record.BuyerName)}|{record.DeadlineAt}|{record.Country}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    public double Similarity(string left, string right)
    {
        var a = Normalize(left).Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        var b = Normalize(right).Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        if (a.Count == 0 || b.Count == 0) return 0;
        return (double)a.Intersect(b).Count() / a.Union(b).Count();
    }

    private static string Normalize(string value) => string.Concat(value.ToLowerInvariant().Normalize(NormalizationForm.FormD).Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark && (char.IsLetterOrDigit(c) || char.IsWhiteSpace(c))));
}
