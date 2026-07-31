namespace TenderScope.Infrastructure.Crawling;

public sealed class RobotsPolicy
{
    public bool IsAllowed(Uri target, string? robotsText)
    {
        if (string.IsNullOrWhiteSpace(robotsText)) return true;

        var path = target.AbsolutePath;
        var appliesToAll = false;
        foreach (var rawLine in robotsText.Split('\n'))
        {
            var line = rawLine.Split('#')[0].Trim();
            if (line.StartsWith("User-agent:", StringComparison.OrdinalIgnoreCase))
            {
                appliesToAll = line["User-agent:".Length..].Trim() == "*";
                continue;
            }

            if (!appliesToAll || !line.StartsWith("Disallow:", StringComparison.OrdinalIgnoreCase)) continue;
            var disallowed = line["Disallow:".Length..].Trim();
            if (!string.IsNullOrEmpty(disallowed) && path.StartsWith(disallowed, StringComparison.OrdinalIgnoreCase)) return false;
        }

        return true;
    }
}
