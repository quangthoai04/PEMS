namespace PEMS.Application.BusinessCardOcr.Services;

/// <summary>
/// Deterministic (non-generative) parser that splits raw OCR text into business
/// card fields — regex + keyword heuristics only (02 prompt §7.4).
/// </summary>
public interface IBusinessCardTextParser
{
    BusinessCardParsedFields Parse(string rawText);
}

public sealed class BusinessCardParsedFields
{
    public string? FullName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? JobTitle { get; set; }
    public string? DepartmentName { get; set; }
    public string? Organization { get; set; }
    public string? WebsiteUrl { get; set; }
    public string? Address { get; set; }
}
