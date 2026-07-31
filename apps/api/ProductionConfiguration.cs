namespace TenderScope.Api;

public static class ProductionConfiguration
{
    public static void Validate(IConfiguration configuration, IHostEnvironment environment)
    {
        var errors = new List<string>();
        Require(configuration, errors, "ConnectionStrings:Postgres");
        Require(configuration, errors, "Jwt:Secret", minimumLength: 32);
        Require(configuration, errors, "Jwt:Issuer");
        Require(configuration, errors, "Jwt:Audience");

        var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
        if (origins is null || origins.Length == 0)
            errors.Add("Cors:AllowedOrigins must contain at least one explicit origin.");
        else if (environment.IsProduction() && origins.Any(x => x.Contains("localhost", StringComparison.OrdinalIgnoreCase) || x == "*"))
            errors.Add("Production CORS origins cannot contain localhost or wildcard origins.");

        if (environment.IsProduction() && configuration.GetValue("Jwt:AllowHttp", false))
            errors.Add("Jwt:AllowHttp must be false in production.");

        if (errors.Count > 0)
            throw new InvalidOperationException("Invalid production configuration: " + string.Join(" | ", errors));
    }

    private static void Require(IConfiguration configuration, ICollection<string> errors, string key, int minimumLength = 1)
    {
        var value = configuration[key];
        if (string.IsNullOrWhiteSpace(value) || value.Length < minimumLength)
            errors.Add($"{key} is missing or shorter than {minimumLength} characters.");
    }
}
