using System.Threading;
using System.Threading.Tasks;
using MediatR;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Emails.Common;

namespace PEMS.Application.Emails.Commands.CreateEmailTemplate;

/// <summary>
/// Refuses. The system template catalog is fixed in code (G11-I; UC-45 deprecated).
///
/// <para>
/// A template code exists because some caller in a release sends it. A code invented through the API is
/// a row nothing can ever address: no handler references it, no dispatcher resolves it, and it will
/// never reach a recipient. Worse, the previous implementation stored whatever it was given — including
/// <c>variables_text</c>, which the renderer validates real sends against — so the catalog could grow
/// entries that looked authoritative in the management screen and were inert everywhere else. That is
/// how it previously accumulated nine dead codes.
/// </para>
///
/// <para>
/// The route and the command are kept rather than deleted, so a caller that still posts to them gets a
/// stable business error it can display instead of a 404 or 405 it has to interpret. The refusal lives
/// in the handler and not only in the controller or the screen: the requirement is that NO path creates
/// a template, and a check that sits in one caller is not that.
/// </para>
/// </summary>
public sealed class CreateEmailTemplateCommandHandler
    : IRequestHandler<CreateEmailTemplateCommand, CreateEmailTemplateResponse>
{
    public Task<CreateEmailTemplateResponse> Handle(
        CreateEmailTemplateCommand request, CancellationToken cancellationToken)
        => throw new ConflictException(
            "Danh mục mẫu email là cố định của hệ thống. Không thể tạo mẫu mới; " +
            "bạn chỉ có thể cập nhật nội dung của các mẫu có sẵn.",
            EmailErrorCodes.TemplateCatalogFixed);
}
