namespace TenderScope.Domain.Entities;

public sealed class Institution
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public required string CanonicalName { get; init; }
    public required string CountryCode { get; init; }
    public string? Region { get; init; }
    public string? Website { get; init; }
    public string? RegistrationNumber { get; init; }
}
