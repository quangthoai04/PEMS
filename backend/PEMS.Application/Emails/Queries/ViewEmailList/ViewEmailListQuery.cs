using MediatR;
using PEMS.Application.Common.Models;

namespace PEMS.Application.Emails.Queries.ViewEmailList;

public class ViewEmailListQuery : IRequest<ViewEmailListResponse>
{
    public string MailBox { get; set; } = "all"; // all, sent, received
    public string? Keyword { get; set; }
    public string? Status { get; set; } // process status
    public string? RelatedType { get; set; } // VISIT_REQUEST, GENERAL
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}
