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

    public EmailContactResolver(
        IApplicationDbContext db,
        IEmailContactPolicyStore policies,
        IOptions<EmailSupportContactOptions> support)
    {
        _db = db;
        _policies = policies;
        _support = support.Value;
    }

    public async Task<EmailContactResolution> ResolveAsync(
        EmailContactRequest request, CancellationToken cancellationToken = default)
    {
        var policy = await _policies.ResolveAsync(
            request.TemplateCode, request.CampusId, request.DepartmentId, cancellationToken);

        if (!policy.RendersBlock)
            return new EmailContactResolution(policy, null, string.Empty, null);

        var contact = await LookUpAsync(policy.ContactSource, request, cancellationToken);

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

        var replyTo = await ResolveReplyToAsync(policy, contact, request, cancellationToken);

        return new EmailContactResolution(policy, contact, html, replyTo);
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
    private async Task<EmailContactAddress?> ResolveReplyToAsync(
        EmailContactPolicyResolution policy,
        EmailContactInformation contact,
        EmailContactRequest request,
        CancellationToken ct)
    {
        var (email, name) = policy.ReplyToSource switch
        {
            EmailReplyToSource.CONTACT => (contact.Email, contact.DisplayName),
            EmailReplyToSource.SENDER => await SenderAddressAsync(request, ct),
            _ => (null, null),
        };

        if (string.IsNullOrWhiteSpace(email)) return null;

        if (!EmailRecipientValidator.IsWellFormed(email!))
            throw new BusinessRuleException(
                $"Địa chỉ Reply-To lấy từ nguồn {policy.ReplyToSource} cho template "
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
