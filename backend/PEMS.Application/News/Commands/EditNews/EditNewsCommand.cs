using MediatR;

namespace PEMS.Application.News.Commands.EditNews;

public sealed record EditNewsCommand : IRequest<EditNewsResponse>
{
    public ulong   NewsId          { get; init; }
    public int     RowVersion      { get; init; }
    public ulong?  CoverFileId     { get; init; }
    public string  Title           { get; init; } = string.Empty;
    public string? Summary         { get; init; }
    /// <summary>Translation being edited; defaults to the Vietnamese original.</summary>
    public string  LanguageCode    { get; init; } = "vi";
    public IReadOnlyList<EditNewsContentSectionDto> ContentSections { get; init; }
        = Array.Empty<EditNewsContentSectionDto>();
}

public sealed record EditNewsContentSectionDto
{
    public int     SectionOrder    { get; init; }
    public string  SectionTitle    { get; init; } = string.Empty;
    public string  SectionBodyHtml { get; init; } = string.Empty;
    public IReadOnlyList<EditNewsSectionFileDto>? SectionFiles { get; init; }
}

public sealed record EditNewsSectionFileDto
{
    public ulong  FileId       { get; init; }
    public string UsageType    { get; init; } = "INLINE_IMAGE";
    public int    DisplayOrder { get; init; }
}
