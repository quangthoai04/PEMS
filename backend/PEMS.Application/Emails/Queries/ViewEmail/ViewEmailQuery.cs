using MediatR;

namespace PEMS.Application.Emails.Queries.ViewEmail;

public class ViewEmailQuery : IRequest<ViewEmailDto>
{
    public ulong Id { get; set; }
    public string SourceType { get; set; } = "ALL";
}