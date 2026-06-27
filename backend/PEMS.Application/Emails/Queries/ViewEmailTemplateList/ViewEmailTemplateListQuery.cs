using MediatR;

namespace PEMS.Application.Emails.Queries.ViewEmailTemplateList;

public class ViewEmailTemplateListQuery : IRequest<ViewEmailTemplateListDto>
{
    public string? Mode { get; set; }
    public string? Keyword { get; set; }
    public string? Status { get; set; }
    public string? Purpose { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}