using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Partners.Common;
using PEMS.Application.Partners.VisitLinks.Common;

namespace PEMS.Application.Partners.VisitLinks.Queries.GetVisitGuestPartnerLinks;

public sealed class GetVisitGuestPartnerLinksQueryHandler
    : IRequestHandler<GetVisitGuestPartnerLinksQuery, List<VisitGuestPartnerLinkDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetVisitGuestPartnerLinksQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<List<VisitGuestPartnerLinkDto>> Handle(
        GetVisitGuestPartnerLinksQuery request, CancellationToken cancellationToken)
    {
        var instance = await VisitLinkSupport.LoadInstanceWithAccessAsync(
            _db, _currentUser, request.VisitInstanceId, cancellationToken);

        // Links saved for this instance OR request-wide links (visit_instance_id NULL).
        var rows = await _db.VisitGuestPartnerLinks.AsNoTracking()
            .Where(l => l.VisitRequestId == instance.VisitRequestId
                        && (l.VisitInstanceId == null || l.VisitInstanceId == instance.VisitInstanceId))
            .OrderByDescending(l => l.LinkId)
            .ToListAsync(cancellationToken);
        if (rows.Count == 0) return new List<VisitGuestPartnerLinkDto>();

        var partnerIds = rows.Select(r => r.PartnerId).Distinct().ToList();
        var partners = await _db.Partners.AsNoTracking()
            .Where(p => partnerIds.Contains(p.PartnerId))
            .Select(p => new { p.PartnerId, p.Name, p.ProfileStatus })
            .ToDictionaryAsync(p => p.PartnerId, cancellationToken);

        var contactIds = rows.Where(r => r.PartnerContactId != null)
            .Select(r => r.PartnerContactId!.Value).Distinct().ToList();
        var contacts = contactIds.Count == 0
            ? new Dictionary<ulong, string>()
            : await _db.PartnerContacts.AsNoTracking()
                .Where(c => contactIds.Contains(c.ContactId))
                .Select(c => new { c.ContactId, c.FullName })
                .ToDictionaryAsync(c => c.ContactId, c => c.FullName, cancellationToken);

        return rows.Select(l => new VisitGuestPartnerLinkDto
        {
            LinkId = l.LinkId,
            VisitRequestId = l.VisitRequestId,
            VisitInstanceId = l.VisitInstanceId,
            GuestMemberId = l.GuestMemberId,
            MinuteParticipantId = l.MinuteParticipantId,
            PartnerId = l.PartnerId,
            PartnerName = partners.TryGetValue(l.PartnerId, out var p) ? p.Name : string.Empty,
            PartnerProfileStatus = partners.TryGetValue(l.PartnerId, out var p2) ? p2.ProfileStatus : string.Empty,
            PartnerContactId = l.PartnerContactId,
            PartnerContactName = l.PartnerContactId is { } cid && contacts.TryGetValue(cid, out var cn) ? cn : null,
            MatchSource = l.MatchSource,
            MatchStatus = l.MatchStatus,
            ConfidenceScore = l.ConfidenceScore,
            Note = l.Note,
            CreatedAt = l.CreatedAt,
        }).ToList();
    }
}
