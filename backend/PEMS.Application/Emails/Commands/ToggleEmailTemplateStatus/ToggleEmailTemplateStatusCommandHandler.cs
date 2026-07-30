using System.Threading;
using System.Threading.Tasks;
using MediatR;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Emails.Common;

namespace PEMS.Application.Emails.Commands.ToggleEmailTemplateStatus;

/// <summary>
/// Refuses. A system template's status is not an operator's to change (G11-I).
///
/// <para>
/// Deactivating a system template does not turn a feature off — it breaks one. The renderer refuses a
/// template whose status is not ACTIVE with <c>EMAIL_TEMPLATE_INACTIVE</c>, so switching off, say,
/// <c>ACCOUNT_EMAIL_CONFIRMATION</c> would not stop accounts being created; it would stop the
/// confirmation email going out, and every new account would sit unconfirmed with nothing in the UI
/// explaining why. The previous implementation offered that as a one-click toggle on each row of a
/// table, with no confirmation and no warning.
/// </para>
///
/// <para>
/// Which templates are active is decided by the code release that ships their callers, and is applied
/// by the canonical seed and the sync script. The route stays so an old client receives a stable code
/// rather than a 404.
/// </para>
/// </summary>
public sealed class ToggleEmailTemplateStatusCommandHandler
    : IRequestHandler<ToggleEmailTemplateStatusCommand, ToggleEmailTemplateStatusResponse>
{
    public Task<ToggleEmailTemplateStatusResponse> Handle(
        ToggleEmailTemplateStatusCommand request, CancellationToken cancellationToken)
        => throw new ConflictException(
            "Không thể bật/tắt mẫu email hệ thống. Trạng thái của mẫu do phiên bản phần mềm quyết định — " +
            "tắt một mẫu sẽ làm hỏng chức năng đang dùng nó chứ không tắt chức năng đó.",
            EmailErrorCodes.TemplateCatalogFixed);
}
