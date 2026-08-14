using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Partners.Common;
using PEMS.Application.Partners.VisitLinks.Common;
using PEMS.Domain.Entities.Partners;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.Partners.VisitLinks.Commands.CreateOrUpdateVisitGuestPartnerLink;

public sealed class CreateOrUpdateVisitGuestPartnerLinkCommandHandler
    : IRequestHandler<CreateOrUpdateVisitGuestPartnerLinkCommand, VisitGuestPartnerLinkDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;

    public CreateOrUpdateVisitGuestPartnerLinkCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<VisitGuestPartnerLinkDto> Handle(
        CreateOrUpdateVisitGuestPartnerLinkCommand request, CancellationToken cancellationToken)
    {
        if (request.GuestMemberId is null && request.MinuteParticipantId is null)
            throw new BusinessRuleException(
                "Phải chỉ định khách trong đoàn hoặc người tham gia biên bản để liên kết đối tác.",
                PartnerErrorCodes.LinkTargetRequired);

        var instance = await VisitLinkSupport.LoadInstanceWithAccessAsync(
            _db, _currentUser, request.VisitInstanceId, cancellationToken);

        var partner = await _db.Partners.AsNoTracking()
            .FirstOrDefaultAsync(p => p.PartnerId == request.PartnerId, cancellationToken)
            ?? throw new NotFoundException("Partner", request.PartnerId);

        // Asserting "this person belongs to that organization" is a stronger act than reading the
        // profile, so it has its OWN policy: a REJECTED or DRAFT profile is never a valid link target
        // even for the campus staff who can see it (PART-04).
        var blockReason = PartnerAccess.LinkBlockReason(_currentUser, partner);
        if (blockReason is not null)
            throw blockReason switch
            {
                PartnerLinkBlockReasons.Rejected => new AuthBusinessException(
                    PartnerErrorCodes.RejectedCannotLink,
                    "Hồ sơ đối tác này đã bị từ chối nên không thể liên kết. Hãy chỉnh sửa và gửi duyệt lại.",
                    409),
                PartnerLinkBlockReasons.Draft => new AuthBusinessException(
                    PartnerErrorCodes.NotLinkable,
                    "Hồ sơ đối tác này còn là bản nháp, chưa thể liên kết.", 409),
                PartnerLinkBlockReasons.PendingOtherCampus => new AuthBusinessException(
                    PartnerErrorCodes.NotLinkable,
                    "Hồ sơ đối tác đang chờ duyệt ở cơ sở khác nên chưa thể liên kết.", 409),
                _ => new AuthBusinessException(PartnerErrorCodes.Forbidden,
                    "Bạn không có quyền liên kết tới đối tác này.", 403),
            };

        // The guest/participant must belong to THIS instance — no "exists anywhere" fallback (PART-08).
        await VisitLinkSupport.EnsureTargetsInInstanceAsync(
            _db, instance, request.GuestMemberId, request.MinuteParticipantId, cancellationToken);

        if (request.PartnerContactId is { } contactId)
        {
            var contactOk = await _db.PartnerContacts.AnyAsync(
                c => c.ContactId == contactId && c.PartnerId == partner.PartnerId, cancellationToken);
            if (!contactOk) throw new NotFoundException("PartnerContact", contactId);
        }

        var now = _clock.VietnamNow;
        var matchSource = string.IsNullOrWhiteSpace(request.MatchSource)
            ? PartnerLinkMatchSources.Manual
            : request.MatchSource!;
        if (Array.IndexOf(PartnerLinkMatchSources.All, matchSource) < 0)
            matchSource = PartnerLinkMatchSources.Manual;
        var matchStatus = request.MatchStatus == PartnerLinkMatchStatuses.Suggested
            ? PartnerLinkMatchStatuses.Suggested
            : PartnerLinkMatchStatuses.Confirmed;

        VisitGuestPartnerLink? link = null;
        if (request.LinkId is { } linkId)
        {
            // Scoped to the request AND this instance (legacy request-wide rows carry a null instance).
            link = await _db.VisitGuestPartnerLinks
                .FirstOrDefaultAsync(l => l.LinkId == linkId
                                          && l.VisitRequestId == instance.VisitRequestId
                                          && (l.VisitInstanceId == null
                                              || l.VisitInstanceId == instance.VisitInstanceId), cancellationToken)
                ?? throw new NotFoundException("VisitGuestPartnerLink", linkId);

            // A link id identifies ONE person's relationship. Re-pointing it at somebody else would
            // silently rewrite whose organization this is — that is a new link, not an update.
            var retargeted =
                (request.GuestMemberId is { } newGid && link.GuestMemberId is { } oldGid && newGid != oldGid)
                || (request.MinuteParticipantId is { } newMid && link.MinuteParticipantId is { } oldMid && newMid != oldMid)
                || (request.GuestMemberId is not null && link.MinuteParticipantId is not null && link.GuestMemberId is null)
                || (request.MinuteParticipantId is not null && link.GuestMemberId is not null && link.MinuteParticipantId is null);
            if (retargeted)
                throw new BusinessRuleException(
                    "Không thể đổi người được liên kết của một liên kết đã có. Hãy tạo liên kết mới cho người đó.",
                    PartnerErrorCodes.LinkTargetRequired);
        }
        else
        {
            // One target keeps at most one active link — reuse the existing row.
            link = await _db.VisitGuestPartnerLinks.FirstOrDefaultAsync(
                l => l.VisitRequestId == instance.VisitRequestId
                     && (l.VisitInstanceId == null || l.VisitInstanceId == instance.VisitInstanceId)
                     && ((request.GuestMemberId != null && l.GuestMemberId == request.GuestMemberId)
                         || (request.MinuteParticipantId != null && l.MinuteParticipantId == request.MinuteParticipantId)),
                cancellationToken);
        }

        if (link is null)
        {
            link = new VisitGuestPartnerLink
            {
                VisitRequestId = instance.VisitRequestId,
                VisitInstanceId = instance.VisitInstanceId,
                GuestMemberId = request.GuestMemberId,
                MinuteParticipantId = request.MinuteParticipantId,
                PartnerId = partner.PartnerId,
                CreatedAt = now,
                CreatedBy = _currentUser.UserId,
            };
            _db.VisitGuestPartnerLinks.Add(link);
        }
        else
        {
            link.UpdatedAt = now;
            link.UpdatedBy = _currentUser.UserId;
            link.PartnerId = partner.PartnerId;
            if (request.GuestMemberId is not null) link.GuestMemberId = request.GuestMemberId;
            if (request.MinuteParticipantId is not null) link.MinuteParticipantId = request.MinuteParticipantId;
        }

        // Auto-resolve or create a PartnerContact if PartnerContactId is null and linking is confirmed.
        var finalPartnerContactId = request.PartnerContactId;
        if (finalPartnerContactId is null && matchStatus == PartnerLinkMatchStatuses.Confirmed)
        {
            string? targetFullName = null;
            string? targetJobTitle = null;
            string? targetEmail = null;

            if (request.GuestMemberId is { } targetGid)
            {
                var g = await _db.VisitGuestMembers.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.GuestMemberId == targetGid, cancellationToken);
                if (g != null)
                {
                    targetFullName = g.FullName;
                    targetJobTitle = g.JobTitle;
                }
            }
            else if (request.MinuteParticipantId is { } targetMid)
            {
                var mp = await _db.MinuteParticipants.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.MinuteParticipantId == targetMid, cancellationToken);
                if (mp != null)
                {
                    targetFullName = mp.FullNameSnapshot;
                    targetJobTitle = mp.RoleSnapshot;
                    targetEmail = mp.EmailSnapshot;
                }
            }

            if (!string.IsNullOrWhiteSpace(targetFullName))
            {
                var cleanName = targetFullName.Trim();
                var cleanEmail = string.IsNullOrWhiteSpace(targetEmail) ? null : targetEmail.Trim();

                var existingContact = await _db.PartnerContacts
                    .FirstOrDefaultAsync(c => c.PartnerId == partner.PartnerId && c.Status == "ACTIVE"
                        && (c.FullName.ToLower() == cleanName.ToLower()
                            || (cleanEmail != null && c.Email != null && c.Email.ToLower() == cleanEmail.ToLower())),
                        cancellationToken);

                if (existingContact != null)
                {
                    finalPartnerContactId = existingContact.ContactId;
                }
                else
                {
                    var newContact = new PartnerContact
                    {
                        PartnerId = partner.PartnerId,
                        FullName = cleanName,
                        JobTitle = string.IsNullOrWhiteSpace(targetJobTitle) ? null : targetJobTitle.Trim(),
                        Email = cleanEmail,
                        SourceType = "MANUAL",
                        Status = "ACTIVE",
                        IsPrimary = false,
                        CreatedAt = now,
                        CreatedBy = _currentUser.UserId,
                    };
                    _db.PartnerContacts.Add(newContact);
                    await _db.SaveChangesAsync(cancellationToken);
                    finalPartnerContactId = newContact.ContactId;
                }
            }
        }

        link.PartnerId = partner.PartnerId;
        link.PartnerContactId = finalPartnerContactId;
        link.MatchSource = matchSource;
        link.MatchStatus = matchStatus;
        link.ConfidenceScore = request.ConfidenceScore;
        link.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = _currentUser.UserId,
            CampusId = instance.CampusId,
            Action = "LINK_VISIT_GUEST_PARTNER",
            EntityType = "VisitGuestPartnerLink",
            EntityId = link.LinkId == 0 ? null : link.LinkId,
            CreatedAt = now,
        });

        await _db.SaveChangesAsync(cancellationToken);

        return new VisitGuestPartnerLinkDto
        {
            LinkId = link.LinkId,
            VisitRequestId = link.VisitRequestId,
            VisitInstanceId = link.VisitInstanceId,
            GuestMemberId = link.GuestMemberId,
            MinuteParticipantId = link.MinuteParticipantId,
            PartnerId = link.PartnerId,
            PartnerName = partner.Name,
            PartnerProfileStatus = partner.ProfileStatus,
            PartnerContactId = link.PartnerContactId,
            MatchSource = link.MatchSource,
            MatchStatus = link.MatchStatus,
            ConfidenceScore = link.ConfidenceScore,
            Note = link.Note,
            CreatedAt = link.CreatedAt,
        };
    }
}
