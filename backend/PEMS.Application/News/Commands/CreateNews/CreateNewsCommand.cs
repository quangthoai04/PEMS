using MediatR;

namespace PEMS.Application.News.Commands.CreateNews;

public sealed record CreateNewsCommand : IRequest<CreateNewsResponse>
{
    public ulong VisitInstanceId { get; init; }
    public ulong? CoverFileId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public IReadOnlyList<CreateNewsContentSectionDto> ContentSections { get; init; } = Array.Empty<CreateNewsContentSectionDto>();
}

public sealed record CreateNewsContentSectionDto
{
    public int SectionOrder { get; init; }
    public string SectionTitle { get; init; } = string.Empty;
    public string SectionBodyHtml { get; init; } = string.Empty;
    public IReadOnlyList<CreateNewsSectionFileDto>? SectionFiles { get; init; }
}

public sealed record CreateNewsSectionFileDto
{
    public ulong FileId { get; init; }
    public string UsageType { get; init; } = "ATTACHMENT";
    public int DisplayOrder { get; init; }
}
