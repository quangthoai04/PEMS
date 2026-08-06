using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;

namespace PEMS.Application.Delegations.SetupProgressEmail;

/// <summary>
/// The default TO/CC/BCC of a setup-progress email, plus anything the Host should be told about how it
/// was arrived at. <see cref="Warnings"/> is display text, never a control signal — whether the draft is
/// sendable is decided by whether <see cref="To"/> has anything in it.
/// </summary>
public sealed record SetupProgressRecipients(
    IReadOnlyList<EmailRecipient> To,
    IReadOnlyList<EmailRecipient> Cc,
    IReadOnlyList<EmailRecipient> Bcc,
    IReadOnlyList<string> Warnings);

/// <summary>
/// Works out who a setup-progress email goes to, from the instance's own data.
///
/// <para>
/// It is a backend service and not a frontend computation for one reason: the compose screen shows the
/// participants it happens to have loaded, and "who is on this mail by default" must not depend on what
/// a screen had in memory. The Host may then add, remove and move anybody — that is their decision, made
/// visibly — but the starting list is derived here, once, from the database.
/// </para>
/// </summary>
public interface IVisitSetupProgressRecipientResolver
{
    Task<SetupProgressRecipients> ResolveAsync(VisitRequestCampus instance, CancellationToken cancellationToken);
}

/// <inheritdoc cref="IVisitSetupProgressRecipientResolver"/>
public sealed class VisitSetupProgressRecipientResolver : IVisitSetupProgressRecipientResolver
{
    private readonly IApplicationDbContext _db;

    public VisitSetupProgressRecipientResolver(IApplicationDbContext db) => _db = db;

    public async Task<SetupProgressRecipients> ResolveAsync(
        VisitRequestCampus instance, CancellationToken cancellationToken)
    {
        var visit = instance.VisitRequest
            ?? await _db.VisitRequests.FirstAsync(v => v.VisitRequestId == instance.VisitRequestId, cancellationToken);

        var warnings = new List<string>();

        // ── Guest side → TO ───────────────────────────────────────────────────
        //
        // "The guest" here means the two people PEMS actually holds an address for: THIS campus's
        // operational contact and the registrant. visit_guest_members — the named delegation roster —
        // carries name, organisation, job title and nationality and NO email column, so there is
        // nobody else to write to. Inventing one from a name is the one thing this resolver must
        // never do.
        //
        // The contact comes from this instance's own form detail. A sibling campus's contact is a
        // different person running a different day, and copying them onto this campus's preparation
        // update would tell them about a visit that is not theirs.
        var detail = instance.FormDetail
            ?? await _db.VisitInstanceFormDetails
                .FirstOrDefaultAsync(d => d.VisitInstanceId == instance.VisitInstanceId, cancellationToken);

        var guests = new List<EmailRecipient>();
        Add(guests, detail?.OperationalContactEmail, detail?.OperationalContactFullName);
        Add(guests, visit.RegistrantEmail, visit.RegistrantFullName);

        // ── FPT side → CC ─────────────────────────────────────────────────────
        //
        // ACCEPTED only. An INVITED person has not answered, DECLINED has said no, and an ASSIGNED
        // department contact has not confirmed — copying any of them onto a message to the guest would
        // present them to the guest as part of a reception they have not agreed to join. The Host is
        // excluded because the Host is the sender.
        var acceptedUserIds = await _db.VisitParticipants
            .Where(p => p.VisitInstanceId == instance.VisitInstanceId
                        && p.Status == ParticipantStatuses.Accepted
                        && !p.IsHost
                        && p.UserId != instance.CurrentHostUserId)
            .OrderBy(p => p.ParticipantId)
            .Select(p => p.UserId)
            .ToListAsync(cancellationToken);

        var internals = new List<EmailRecipient>();
        if (acceptedUserIds.Count > 0)
        {
            // Addresses come from users, not from a participant snapshot: somebody whose account email
            // changed after they accepted should be written to at the address they have now.
            var users = await _db.Users
                .Where(u => acceptedUserIds.Contains(u.UserId))
                .Select(u => new { u.UserId, u.Email, u.FullName })
                .ToListAsync(cancellationToken);
            var byId = users.ToDictionary(u => u.UserId);

            foreach (var id in acceptedUserIds)
            {
                if (byId.TryGetValue(id, out var u)) Add(internals, u.Email, u.FullName);
            }
        }

        // ── Assemble, de-duplicating across the whole envelope ────────────────
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var to = new List<EmailRecipient>();
        var cc = new List<EmailRecipient>();

        if (guests.Count > 0)
        {
            foreach (var g in guests) TakeInto(to, g, seen);
            foreach (var i in internals) TakeInto(cc, i, seen);
        }
        else if (internals.Count > 0)
        {
            // No address for the guest side at all. Rather than produce a draft with an empty TO — which
            // cannot be sent and gives the Host nothing to work from — the first accepted participant
            // becomes the primary recipient and the rest are copied. The warning says so, because a mail
            // addressed to a colleague is not the mail the Host set out to send.
            TakeInto(to, internals[0], seen);
            foreach (var i in internals.Skip(1)) TakeInto(cc, i, seen);
            warnings.Add(
                "Chưa có địa chỉ email nào của phía khách (đầu mối vận hành cơ sở này / người đăng ký). " +
                "Em đã tạm đặt một thành phần tham gia vào mục Đến — anh/chị vui lòng kiểm tra lại người nhận trước khi gửi.");
        }
        else
        {
            warnings.Add(
                "Chưa tìm được người nhận nào cho chuyến thăm này. " +
                "Vui lòng nhập ít nhất một địa chỉ ở mục Đến trước khi xem trước hoặc gửi.");
        }

        // A trimmed-but-malformed address is dropped rather than passed on: the send would refuse it
        // anyway, and refusing at compose time is where the Host can still do something about it.
        return new SetupProgressRecipients(to, cc, Array.Empty<EmailRecipient>(), warnings);
    }

    private static void Add(List<EmailRecipient> into, string? email, string? name)
    {
        var trimmed = email?.Trim();
        if (string.IsNullOrEmpty(trimmed) || !EmailRecipientValidator.IsWellFormed(trimmed)) return;
        into.Add(new EmailRecipient(trimmed, string.IsNullOrWhiteSpace(name) ? null : name.Trim()));
    }

    /// <summary>
    /// Places a recipient in its group unless the address is already somewhere in the envelope.
    /// TO is filled first, so an address that is both the guest contact and an accepted participant
    /// stays a primary recipient instead of being demoted to a copy.
    /// </summary>
    private static void TakeInto(List<EmailRecipient> group, EmailRecipient recipient, HashSet<string> seen)
    {
        if (!seen.Add(recipient.Email)) return;
        group.Add(recipient);
    }
}
