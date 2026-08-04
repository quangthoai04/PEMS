using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Common;
using PEMS.Domain.Enums;

namespace PEMS.Application.Emails.Contact;

/// <summary>
/// Turns "this template, this visit, this sender" into a reply contact and the block that shows it.
///
/// <para>
/// <b>Campus scoping is the rule the rest hangs off.</b> The Host is read from
/// <c>visit_request_campuses.current_host_user_id</c> for the specific instance, so a multi-campus request
/// can never hand a guest the wrong campus's Host. There is no fallback that widens the search to the
/// parent request — a missing Host falls to the campus default, which is at least true, rather than to a
/// different campus's Host, which is confidently wrong.
/// </para>
/// <para>
/// <b>Fail closed, but only where the words promise something.</b> A REQUIRED policy that resolves nothing
/// throws; an OPTIONAL one renders no block and the mail goes out. That asymmetry is the whole design: it
/// is worth blocking an invitation that says "contact the host" and cannot say how, and not worth blocking
/// a report whose text never mentions contacting anybody.
/// </para>
/// </summary>
public sealed class EmailContactResolver : IEmailContactResolver
{
    private readonly IApplicationDbContext _db;
    private readonly IEmailContactPolicyStore _policies;
    private readonly EmailSupportContactOptions _support;
    private readonly IEmailContactCandidateService? _candidates;

    public EmailContactResolver(
        IApplicationDbContext db,
        IEmailContactPolicyStore policies,
        IOptions<EmailSupportContactOptions> support,
        IEmailContactCandidateService? candidates = null)
    {
        _db = db;
        _policies = policies;
        _support = support.Value;
        _candidates = candidates;
    }

    public Task<EmailContactResolution> ResolveAsync(
        EmailContactRequest request, CancellationToken cancellationToken = default)
        => ResolveAsync(request, overrideInput: null, actorUserId: null, cancellationToken);

    public async Task<EmailContactResolution> ResolveAsync(
        EmailContactRequest request,
        EmailContactOverrideInput? overrideInput,
        ulong? actorUserId,
        CancellationToken cancellationToken = default)
    {
        var policy = await _policies.ResolveAsync(
            request.TemplateCode, request.CampusId, request.DepartmentId, cancellationToken);

        // Capability outranks configuration, at the send as well as on the screen. A stored row that
        // switched the block on for a credential-bearing mail — written before the settings endpoint
        // refused such a write, or by hand — must not be able to put a contact card on a message whose
        // whole content is a one-time link. Reported as NONE rather than refused: a send is the wrong
        // place to discover a policy row is wrong, and the mail without the block is exactly the mail
        // this template is supposed to be.
        var capability = EmailContactCapabilities.For(request.TemplateCode);
        if (!capability.Supported)
            policy = policy with { Requirement = EmailContactRequirement.NONE };

        // Shape first, then whether this template will take it. Both run before anything is looked up, so
        // a refused override costs one policy read and touches nothing else.
        var over = EmailContactOverrideValidator.Normalize(overrideInput);
        EmailContactOverrideValidator.AssertAllowed(
            over, request.TemplateCode, capability, policy.Requirement);

        if (!policy.RendersBlock)
            return new EmailContactResolution(policy, null, string.Empty, null);

        // Asked for, allowed (AssertAllowed has already refused it on a REQUIRED template), and the
        // message goes out without the card. Reported as hidden rather than as "nothing resolved" so the
        // audit row and the preview can tell the two apart.
        if (over?.HideForThisEmail == true)
            return new EmailContactResolution(policy, null, string.Empty, null)
            {
                Mode = over.Mode,
                HiddenForThisEmail = true,
            };

        var contact = over is null || over.Mode == EmailContactOverrideModes.TemplateDefault
            ? await LookUpAsync(policy.ContactSource, request, cancellationToken)
            : await OverriddenAsync(over, request, actorUserId, cancellationToken);

        // A chosen or hand-entered contact is the sender's decision, and the fallbacks below exist to
        // rescue a policy that resolved nobody — not to quietly replace a person somebody named. So they
        // are skipped, and an override that produces nothing usable is refused instead.
        if (over is not null && over.Mode != EmailContactOverrideModes.TemplateDefault)
            return await FinishOverriddenAsync(policy, contact, over, request, cancellationToken);

        // Last resort, and only for a template that promises a contact: the system support address. It is
        // the bottom of the priority list in the decision record, and it is what keeps "the campus row has
        // no address filled in" from blocking an account notice the recipient genuinely needs to act on.
        // Deliberately NOT applied to an OPTIONAL template — there, showing support instead of the person
        // the text is about would be a downgrade nobody asked for.
        if ((contact is null || !contact.IsReachable)
            && policy.Requirement == EmailContactRequirement.REQUIRED
            && policy.ContactSource != EmailContactSource.SUPPORT_CONTACT)
        {
            contact = Support();
        }

        if (contact is null || !contact.IsReachable)
        {
            if (policy.Requirement == EmailContactRequirement.REQUIRED)
                throw new BusinessRuleException(
                    $"Template email '{request.TemplateCode}' bắt buộc hiển thị thông tin liên hệ nhưng "
                    + $"không tìm được đầu mối khả dụng theo nguồn {policy.ContactSource}.",
                    EmailErrorCodes.ContactRequiredButNotFound);

            // OPTIONAL: nothing to show, nothing to block. The mail is still true without it.
            return new EmailContactResolution(policy, null, string.Empty, null);
        }

        var senderName = policy.ShowSender
            ? await DisplayNameOfAsync(request.SenderUserId, cancellationToken)
            : null;

        var html = EmailContactHtmlRenderer.Render(contact, policy, request.Language, senderName);

        var replyTo = await ResolveReplyToAsync(
            policy, contact, request, over?.ReplyToMode, cancellationToken);

        return new EmailContactResolution(policy, contact, html, replyTo)
        {
            Mode = EmailContactOverrideModes.TemplateDefault,
        };
    }

    // ── Per-message override ────────────────────────────────────────────────

    /// <summary>
    /// The contact a <c>SYSTEM_USER</c> or <c>MANUAL</c> override asks for.
    ///
    /// <para>
    /// The two branches differ in exactly one way, and it is the important one: a chosen account's name,
    /// address, telephone, department and campus are READ FROM THE DATABASE and the client's copies of
    /// them are refused by the validator, while a hand-entered contact has nothing behind it and is taken
    /// as typed. So "the block shows what PEMS holds for this person" stays true in the first case, and
    /// the second is marked as hand-entered everywhere it is recorded.
    /// </para>
    /// </summary>
    private async Task<EmailContactInformation?> OverriddenAsync(
        NormalizedContactOverride over,
        EmailContactRequest request,
        ulong? actorUserId,
        CancellationToken ct)
    {
        if (over.IsManual)
            return new EmailContactInformation(
                // MANUAL is not one of the configured sources and must not be able to impersonate one:
                // reporting it as HOST would put "Người phụ trách tiếp đón" beside a name nobody verified.
                EmailContactSource.SENDER,
                over.DisplayName!,
                RoleLabel: over.RoleLabel,
                DepartmentName: over.DepartmentName,
                CampusName: over.CampusName,
                Email: over.Email,
                Phone: over.Phone);

        // SYSTEM_USER. Both refusals below are the same sentence to the caller — see
        // ContactOverrideUserNotAllowed for why "no such user" and "not yours" are not distinguished.
        if (_candidates is null || actorUserId is not { } actor)
            throw new ForbiddenException(
                "Không xác định được người thực hiện, nên không thể đổi đầu mối liên hệ cho email này.");

        return await _candidates.ResolveChoiceAsync(request, actor, over.UserId!.Value, ct)
            ?? throw new ForbiddenException(
                "Người được chọn làm đầu mối liên hệ không hợp lệ hoặc ngoài phạm vi bạn được phép chọn.");
    }

    /// <summary>
    /// Renders and answers Reply-To for an override, with no fallback of any kind.
    ///
    /// <para>
    /// A policy that resolves nobody falls through to the campus and then to system support, because there
    /// the system is guessing on the sender's behalf and a true second-best is better than a dead
    /// instruction. An override is not a guess — somebody named a person — so silently substituting a
    /// different one would show the recipient a contact the sender never chose and never saw in the
    /// preview they approved. It is refused instead, on OPTIONAL as well as REQUIRED templates.
    /// </para>
    /// </summary>
    private async Task<EmailContactResolution> FinishOverriddenAsync(
        EmailContactPolicyResolution policy,
        EmailContactInformation? contact,
        NormalizedContactOverride over,
        EmailContactRequest request,
        CancellationToken ct)
    {
        if (contact is null || !contact.IsReachable)
            throw new BusinessRuleException(
                "Đầu mối liên hệ được chọn cho email này không có email hoặc số điện thoại khả dụng.",
                EmailErrorCodes.ContactOverrideInvalid);

        var senderName = policy.ShowSender
            ? await DisplayNameOfAsync(request.SenderUserId, ct)
            : null;

        var html = EmailContactHtmlRenderer.Render(contact, policy, request.Language, senderName);

        // The policy's field toggles still apply — a template configured to hide telephone numbers keeps
        // hiding them for a chosen contact too — and a policy that leaves nothing showable produces no
        // block. Reported rather than sent silently: the sender picked somebody and would otherwise be
        // shown a preview with an empty space where they expected a card.
        if (string.IsNullOrEmpty(html))
            throw new BusinessRuleException(
                "Cấu hình khối liên hệ của mẫu email này không hiển thị trường nào của đầu mối được chọn.",
                EmailErrorCodes.ContactOverrideInvalid);

        var replyTo = await ResolveReplyToAsync(policy, contact, request, over.ReplyToMode, ct);

        return new EmailContactResolution(policy, contact, html, replyTo) { Mode = over.Mode };
    }

    // ── Sources ─────────────────────────────────────────────────────────────

    private Task<EmailContactInformation?> LookUpAsync(
        EmailContactSource source, EmailContactRequest request, CancellationToken ct)
        => source switch
        {
            EmailContactSource.HOST => HostAsync(request, ct),
            EmailContactSource.SENDER => SenderAsync(request, ct),
            EmailContactSource.HOST_THEN_SENDER => HostThenSenderAsync(request, ct),
            EmailContactSource.CAMPUS_DEFAULT => CampusAsync(request, ct),
            EmailContactSource.DEPARTMENT_DEFAULT => DepartmentAsync(request, ct),
            EmailContactSource.SUPPORT_CONTACT => Task.FromResult(Support()),
            _ => Task.FromResult<EmailContactInformation?>(null),
        };

    /// <summary>
    /// The Host of THIS campus instance. Falls through to the campus default rather than to another
    /// campus — a visit with no Host assigned yet is a normal state, not a reason to improvise a person.
    /// </summary>
    private async Task<EmailContactInformation?> HostAsync(EmailContactRequest request, CancellationToken ct)
    {
        if (request.VisitInstanceId is not { } instanceId) return await CampusAsync(request, ct);

        var row = await _db.VisitRequestCampuses
            .AsNoTracking()
            .Where(v => v.VisitInstanceId == instanceId)
            .Select(v => new { v.CurrentHostUserId, v.CampusId })
            .FirstOrDefaultAsync(ct);

        if (row?.CurrentHostUserId is not { } hostId)
            return await CampusAsync(request with { CampusId = request.CampusId ?? row?.CampusId }, ct);

        var host = await UserAsync(hostId, ct);

        // An inactive or unconfirmed account is not somebody a guest can reach; treat it as absent and let
        // the campus answer instead of printing a mailbox nobody reads.
        if (host is null)
            return await CampusAsync(request with { CampusId = request.CampusId ?? row.CampusId }, ct);

        var en = EmailLanguages.Normalize(request.Language) == EmailLanguages.En;

        return host with
        {
            Source = EmailContactSource.HOST,
            RoleLabel = en ? "Host for this visit" : "Người phụ trách tiếp đón",
        };
    }

    private async Task<EmailContactInformation?> HostThenSenderAsync(
        EmailContactRequest request, CancellationToken ct)
    {
        if (request.VisitInstanceId is { } instanceId)
        {
            var hostId = await _db.VisitRequestCampuses
                .AsNoTracking()
                .Where(v => v.VisitInstanceId == instanceId)
                .Select(v => v.CurrentHostUserId)
                .FirstOrDefaultAsync(ct);

            if (hostId is { } id && await UserAsync(id, ct) is { } host)
            {
                var en = EmailLanguages.Normalize(request.Language) == EmailLanguages.En;
                return host with
                {
                    Source = EmailContactSource.HOST,
                    RoleLabel = en ? "Host for this visit" : "Người phụ trách tiếp đón",
                };
            }
        }

        return await SenderAsync(request, ct) ?? await CampusAsync(request, ct);
    }

    private async Task<EmailContactInformation?> SenderAsync(EmailContactRequest request, CancellationToken ct)
    {
        if (request.SenderUserId is not { } senderId) return null;

        var sender = await UserAsync(senderId, ct);
        if (sender is null) return null;

        var en = EmailLanguages.Normalize(request.Language) == EmailLanguages.En;
        return sender with
        {
            Source = EmailContactSource.SENDER,
            RoleLabel = en ? "Sent by" : "Người thực hiện",
        };
    }

    /// <summary>
    /// The campus's own address, then its IC head. The campus mailbox is preferred because it outlives
    /// whoever currently holds the role.
    /// </summary>
    private async Task<EmailContactInformation?> CampusAsync(EmailContactRequest request, CancellationToken ct)
    {
        var campusId = request.CampusId;

        if (campusId is null && request.VisitInstanceId is { } instanceId)
            campusId = await _db.VisitRequestCampuses
                .AsNoTracking()
                .Where(v => v.VisitInstanceId == instanceId)
                .Select(v => (ulong?)v.CampusId)
                .FirstOrDefaultAsync(ct);

        if (campusId is not { } id) return null;

        var campus = await _db.Campuses
            .AsNoTracking()
            .Where(c => c.CampusId == id)
            .Select(c => new { c.Name, c.Email, c.Phone, c.IcHeadUserId })
            .FirstOrDefaultAsync(ct);

        if (campus is null) return null;

        var en = EmailLanguages.Normalize(request.Language) == EmailLanguages.En;

        if (!string.IsNullOrWhiteSpace(campus.Email) || !string.IsNullOrWhiteSpace(campus.Phone))
            return new EmailContactInformation(
                EmailContactSource.CAMPUS_DEFAULT,
                campus.Name,
                RoleLabel: en ? "Campus contact" : "Đầu mối cơ sở",
                CampusName: campus.Name,
                Email: campus.Email,
                Phone: campus.Phone);

        if (campus.IcHeadUserId is { } headId && await UserAsync(headId, ct) is { } head)
            return head with
            {
                Source = EmailContactSource.CAMPUS_DEFAULT,
                RoleLabel = en ? "Campus contact" : "Đầu mối cơ sở",
                CampusName = campus.Name,
            };

        return null;
    }

    /// <summary>The department head. Departments carry no address of their own in the schema.</summary>
    private async Task<EmailContactInformation?> DepartmentAsync(
        EmailContactRequest request, CancellationToken ct)
    {
        if (request.DepartmentId is not { } departmentId) return await CampusAsync(request, ct);

        var department = await _db.Departments
            .AsNoTracking()
            .Where(d => d.DepartmentId == departmentId)
            .Select(d => new { d.Name, d.HeadUserId, d.CampusId })
            .FirstOrDefaultAsync(ct);

        if (department is null) return await CampusAsync(request, ct);

        var en = EmailLanguages.Normalize(request.Language) == EmailLanguages.En;

        if (department.HeadUserId is { } headId && await UserAsync(headId, ct) is { } head)
            return head with
            {
                Source = EmailContactSource.DEPARTMENT_DEFAULT,
                RoleLabel = en ? "Department leader" : "Trưởng phòng phụ trách",
                DepartmentName = department.Name,
            };

        return await CampusAsync(request with { CampusId = request.CampusId ?? department.CampusId }, ct);
    }

    private EmailContactInformation? Support()
    {
        if (!_support.IsConfigured) return null;

        return new EmailContactInformation(
            EmailContactSource.SUPPORT_CONTACT,
            _support.Name!,
            Email: _support.Email,
            Phone: _support.Phone);
    }

    // ── Shared lookups ──────────────────────────────────────────────────────

    /// <summary>
    /// A user as a contact, or null when they are not somebody a recipient could usefully be sent to.
    ///
    /// <para>
    /// Anything other than ACTIVE is treated as absent. A locked or not-yet-confirmed account has a
    /// mailbox on paper but nobody reading it, and printing it under "please contact" produces exactly the
    /// dead end this block exists to remove.
    /// </para>
    /// </summary>
    private async Task<EmailContactInformation?> UserAsync(ulong userId, CancellationToken ct)
    {
        var user = await _db.Users
            .AsNoTracking()
            .Where(u => u.UserId == userId && u.Status == "ACTIVE")
            .Select(u => new
            {
                u.FullName,
                u.Email,
                u.Phone,
                DepartmentName = u.Department != null ? u.Department.Name : null,
                CampusName = u.PrimaryCampus != null ? u.PrimaryCampus.Name : null,
            })
            .FirstOrDefaultAsync(ct);

        if (user is null) return null;

        return new EmailContactInformation(
            EmailContactSource.SENDER,   // overwritten by the caller that knows the real branch
            user.FullName,
            DepartmentName: user.DepartmentName,
            CampusName: user.CampusName,
            Email: user.Email,
            Phone: user.Phone);
    }

    private async Task<string?> DisplayNameOfAsync(ulong? userId, CancellationToken ct)
        => userId is { } id ? (await UserAsync(id, ct))?.DisplayName : null;

    /// <summary>
    /// Resolves Reply-To, validating the address before it becomes a header.
    ///
    /// <para>
    /// An unusable address is refused rather than dropped: a policy that says "replies go to the Host" and
    /// silently sends them to the system mailbox instead is the same class of quiet lie as a contact line
    /// with no address in it.
    /// </para>
    /// </summary>
    /// <param name="replyToMode">
    /// The sender's per-message choice, or null/POLICY_DEFAULT to use the configured source. It can only
    /// ever select between the SAME four outcomes the policy chooses from — there is no mode that accepts
    /// an address, because a client-supplied Reply-To is a header a recipient would trust and nobody
    /// verified.
    /// </param>
    private async Task<EmailContactAddress?> ResolveReplyToAsync(
        EmailContactPolicyResolution policy,
        EmailContactInformation contact,
        EmailContactRequest request,
        string? replyToMode,
        CancellationToken ct)
    {
        var source = replyToMode switch
        {
            EmailContactReplyToModes.Contact => EmailReplyToSource.CONTACT,
            EmailContactReplyToModes.Sender => EmailReplyToSource.SENDER,
            EmailContactReplyToModes.None => EmailReplyToSource.NONE,
            _ => policy.ReplyToSource,
        };

        var (email, name) = source switch
        {
            EmailReplyToSource.CONTACT => (contact.Email, contact.DisplayName),
            EmailReplyToSource.SENDER => await SenderAddressAsync(request, ct),
            _ => (null, null),
        };

        // "Replies go to the contact" and a contact with no address is a contradiction the sender chose,
        // not a configuration that drifted — so it is named as their choice rather than reported as an
        // invalid policy. The validator catches the manual case; this catches a chosen account whose
        // record has only a telephone number.
        if (string.IsNullOrWhiteSpace(email)
            && source == EmailReplyToSource.CONTACT
            && replyToMode == EmailContactReplyToModes.Contact)
            throw new BusinessRuleException(
                "Reply-To được đặt về đầu mối liên hệ nhưng đầu mối này không có email.",
                EmailErrorCodes.ContactOverrideInvalid);

        if (string.IsNullOrWhiteSpace(email)) return null;

        if (!EmailRecipientValidator.IsWellFormed(email!))
            throw new BusinessRuleException(
                $"Địa chỉ Reply-To lấy từ nguồn {source} cho template "
                + $"'{request.TemplateCode}' không hợp lệ.",
                EmailErrorCodes.ReplyToInvalid);

        return new EmailContactAddress(email!.Trim(), name);
    }

    private async Task<(string? Email, string? Name)> SenderAddressAsync(
        EmailContactRequest request, CancellationToken ct)
    {
        if (request.SenderUserId is not { } id) return (null, null);
        var sender = await UserAsync(id, ct);
        return (sender?.Email, sender?.DisplayName);
    }
}
