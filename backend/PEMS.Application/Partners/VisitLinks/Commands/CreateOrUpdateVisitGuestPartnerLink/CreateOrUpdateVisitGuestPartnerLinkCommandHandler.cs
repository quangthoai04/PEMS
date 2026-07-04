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

        // Confirming a link to a foreign-campus partner is only allowed once that partner
        // is APPROVED and not PRIVATE (scope rule 8.3).
        if (!PartnerAccess.CanViewPartner(_currentUser, partner))
            throw new AuthBusinessException(PartnerErrorCodes.Forbidden,
                "Bạn không có quyền liên kết tới đối tác này.", 403);

        // Validate the guest/participant really belongs to this visit request.
        if (request.GuestMemberId is { } gid)
        {
            var belongs = await _db.VisitGuestMembers.AnyAsync(
                g => g.GuestMemberId == gid && g.VisitRequestId == instance.VisitRequestId, cancellationToken);
            if (!belongs) throw new NotFoundException("VisitGuestMember", gid);
        }
        if (request.MinuteParticipantId is { } mid)
        {
            var belongs = await (
                from mp in _db.MinuteParticipants
                join m in _db.Minutes on mp.MinutesId equals m.MinutesId
                where mp.MinuteParticipantId == mid && m.VisitInstanceId == instance.VisitInstanceId
                select mp.MinuteParticipantId).AnyAsync(cancellationToken);
            if (!belongs) throw new NotFoundException("MinuteParticipant", mid);
        }

        if (request.PartnerContactId is { } contactId)
        {
            var contactOk = await _db.PartnerContacts.AnyAsync(
                c => c.ContactId == contactId && c.PartnerId == partner.PartnerId, cancellationToken);
            if (!contactOk) throw new NotFoundException("PartnerContact", contactId);
        }

        var now = _clock.UtcNow;
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
            link = await _db.VisitGuestPartnerLinks
                .FirstOrDefaultAsync(l => l.LinkId == linkId
                                          && l.VisitRequestId == instance.VisitRequestId, cancellationToken)
                ?? throw new NotFoundException("VisitGuestPartnerLink", linkId);
        }
        else
        {
            // One target keeps at most one active link — reuse the existing row.
            link = await _db.VisitGuestPartnerLinks.FirstOrDefaultAsync(
                l => l.VisitRequestId == instance.VisitRequestId
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
                CreatedAt = now,
                CreatedBy = _currentUser.UserId,
            };
            _db.VisitGuestPartnerLinks.Add(link);
        }
        else
        {
            link.UpdatedAt = now;
            link.UpdatedBy = _currentUser.UserId;
            if (request.GuestMemberId is not null) link.GuestMemberId = request.GuestMemberId;
            if (request.MinuteParticipantId is not null) link.MinuteParticipantId = request.MinuteParticipantId;
        }

        link.PartnerId = partner.PartnerId;
        link.PartnerContactId = request.PartnerContactId;
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
