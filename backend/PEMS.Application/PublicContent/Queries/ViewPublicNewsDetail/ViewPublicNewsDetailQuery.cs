using MediatR;

namespace PEMS.Application.PublicContent.Queries.ViewPublicNewsDetail;

public sealed class ViewPublicNewsDetailQuery : IRequest<PublicNewsDetailDto>
{
    public ulong NewsId { get; init; }

    /// <summary>Preferred translation language; falls back to 'vi' then to any available translation.</summary>
    public string? LanguageCode { get; init; }

    public ViewPublicNewsDetailQuery(ulong newsId, string? languageCode = null)
    {
        NewsId = newsId;
        LanguageCode = languageCode;
    }
}
