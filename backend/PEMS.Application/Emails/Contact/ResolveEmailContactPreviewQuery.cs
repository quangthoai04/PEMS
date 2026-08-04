using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Common;

namespace PEMS.Application.Emails.Contact;

/// <summary>
/// Re-resolves the reply contact for a message being composed, and nothing else.
///
/// <para>
/// It exists so changing the contact does not re-render the message. The full preview endpoint returns a
/// subject and a body, and calling it again to refresh a contact would hand the screen a fresh copy of
/// both — overwriting whatever the host had written, every time they picked a different colleague. This
/// returns only the panel, so the editor is never touched.
/// </para>
/// <para>
/// Nothing is stored and nothing is sent. The resolution is thrown away; the send does it again, from the
/// database, through the same resolver.
/// </para>
/// </summary>
public sealed class ResolveEmailContactPreviewQuery : IRequest<EmailContactPreviewResult>
{
    public string TemplateCode { get; set; } = string.Empty;

    public string? Language { get; set; }

    /// <summary>The PER-CAMPUS visit id. A request id would resolve another campus's Host.</summary>
    public ulong? VisitInstanceId { get; set; }

    public ulong? CampusId { get; set; }

    public ulong? DepartmentId { get; set; }

    /// <summary>What the sender is currently asking for. Null means "show me the policy's answer".</summary>
    public EmailContactOverrideInput? ContactOverride { get; set; }
}

public sealed class ResolveEmailContactPreviewQueryHandler
    : IRequestHandler<ResolveEmailContactPreviewQuery, EmailContactPreviewResult>
{
    private readonly ICurrentUserService _currentUser;
    private readonly IEmailContactResolver _resolver;
    private readonly IEmailContactPolicyStore? _policies;

    public ResolveEmailContactPreviewQueryHandler(
        ICurrentUserService currentUser,
        IEmailContactResolver resolver,
        IEmailContactPolicyStore? policies = null)
    {
        _currentUser = currentUser;
        _resolver = resolver;
        _policies = policies;
    }

    public async Task<EmailContactPreviewResult> Handle(
        ResolveEmailContactPreviewQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is not { } actorId)
            throw new ForbiddenException();

        if (string.IsNullOrWhiteSpace(request.TemplateCode))
            throw new ValidationException("Thiếu mã template email.");

        // The registry check that the renderer would make, made here too: an unregistered code must not
        // reach the policy store and come back as a plausible-looking shipped default.
        var code = request.TemplateCode.Trim();
        _ = SystemEmailTemplates.Find(code)
            ?? throw new NotFoundException(
                $"Mã template email '{code}' không nằm trong danh mục hệ thống.",
                EmailErrorCodes.TemplateNotFound);

        return await EmailContactPreview.BuildAsync(
            _resolver,
            _policies,
            new EmailContactRequest(
                code,
                EmailLanguages.Normalize(request.Language),
                request.VisitInstanceId,
                request.CampusId,
                request.DepartmentId,
                // The sender is the signed-in account, always. Taking it from the body would let a caller
                // preview — and then send — a message whose "Sent by" line names somebody else.
                actorId),
            request.ContactOverride,
            actorId,
            cancellationToken);
    }
}

/// <summary>
/// The people the signed-in user may name as the reply contact on one message.
///
/// <para>
/// Server-side search, deliberately: the alternative is shipping a directory to the browser and filtering
/// it there, which both discloses every account to anybody who can open the compose screen and puts the
/// scope rule in the one place it cannot be enforced.
/// </para>
/// </summary>
public sealed class SearchEmailContactCandidatesQuery : IRequest<IReadOnlyList<EmailContactCandidateDto>>
{
    public string TemplateCode { get; set; } = string.Empty;
    public string? Language { get; set; }
    public string? Term { get; set; }
    public ulong? VisitInstanceId { get; set; }
    public ulong? CampusId { get; set; }
    public ulong? DepartmentId { get; set; }
    public int Take { get; set; } = 10;
}

public sealed class SearchEmailContactCandidatesQueryHandler
    : IRequestHandler<SearchEmailContactCandidatesQuery, IReadOnlyList<EmailContactCandidateDto>>
{
    private readonly ICurrentUserService _currentUser;
    private readonly IEmailContactCandidateService _candidates;

    public SearchEmailContactCandidatesQueryHandler(
        ICurrentUserService currentUser, IEmailContactCandidateService candidates)
    {
        _currentUser = currentUser;
        _candidates = candidates;
    }

    public async Task<IReadOnlyList<EmailContactCandidateDto>> Handle(
        SearchEmailContactCandidatesQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is not { } actorId)
            throw new ForbiddenException();

        if (string.IsNullOrWhiteSpace(request.TemplateCode))
            throw new ValidationException("Thiếu mã template email.");

        var code = request.TemplateCode.Trim();

        // A template that cannot carry the block has nobody to offer, and answering with a list of
        // colleagues would suggest otherwise.
        if (!EmailContactCapabilities.Supports(code))
            throw new ValidationException(
                $"Mẫu email '{code}' không dùng khối thông tin liên hệ.",
                EmailErrorCodes.ContactOverrideNotAllowed);

        return await _candidates.SearchAsync(
            new EmailContactRequest(
                code,
                EmailLanguages.Normalize(request.Language),
                request.VisitInstanceId,
                request.CampusId,
                request.DepartmentId,
                actorId),
            actorId,
            request.Term,
            request.Take,
            cancellationToken);
    }
}
