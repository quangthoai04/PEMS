namespace PEMS.Application.Partners.Queries.SearchPublicPartnerOptions;

public sealed class PublicPartnerOptionDto
{
    public ulong PartnerId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? ShortName { get; init; }
    public string? Country { get; init; }
    public string? City { get; init; }
    public string PartnerType { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
}
