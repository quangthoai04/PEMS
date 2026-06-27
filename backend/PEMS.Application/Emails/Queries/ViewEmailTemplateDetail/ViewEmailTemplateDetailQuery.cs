using MediatR;

namespace PEMS.Application.Emails.Queries.ViewEmailTemplateDetail;

public class ViewEmailTemplateDetailQuery : IRequest<ViewEmailTemplateDetailDto>
{
    public ulong EmailTemplateId { get; set; }
}