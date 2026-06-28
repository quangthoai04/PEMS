using MediatR;

namespace PEMS.Application.News.Commands.EditNews;

public sealed record EditNewsCommand : IRequest<EditNewsResponse>
{
    public ulong   NewsId          { get; init; }
    public int     RowVersion      { get; init; }
    public ulong?  CoverFileId     { get; init; }
    public string  Title           { get; init; } = string.Empty;
    public string? Summary         { get; init; }
    public IReadOnlyList<EditNewsContentSectionDto> ContentSections { get; init; }
        = Array.Empty<EditNewsContentSectionDto>();
}

public sealed record EditNewsContentSectionDto
{
    public int     SectionOrder    { get; init; }
    public string  SectionTitle    { get; init; } = string.Empty;
    public string  SectionBodyHtml { get; init; } = string.Empty;
}
