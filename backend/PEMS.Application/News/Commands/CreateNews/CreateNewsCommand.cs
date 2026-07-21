using MediatR;

namespace PEMS.Application.News.Commands.CreateNews;

public sealed record CreateNewsCommand : IRequest<CreateNewsResponse>
{
    /// <summary>
    /// Đoàn tiếp khách gắn với bài viết. Bắt buộc với Student (được kiểm tra ở handler vì
    /// validator không có quyền truy cập role hiện tại); tùy chọn với Staff thường — Staff
    /// có thể tạo tin không gắn đoàn nào.
    /// </summary>
    public ulong? VisitInstanceId { get; init; }
    public ulong? CoverFileId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public IReadOnlyList<CreateNewsContentSectionDto> ContentSections { get; init; } = Array.Empty<CreateNewsContentSectionDto>();

    /// <summary>
    /// Bản dịch tiếng Anh tùy chọn được tạo đồng thời với bản tiếng Việt (cùng transaction).
    /// Khi có mặt, EnglishContentSections cũng phải được cung cấp.
    /// </summary>
    public string? EnglishTitle { get; init; }
    public string? EnglishSummary { get; init; }
    public IReadOnlyList<CreateNewsContentSectionDto>? EnglishContentSections { get; init; }
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
