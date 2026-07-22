using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Partners.Common;
using PEMS.Domain.Entities.Partners;

namespace PEMS.Application.Partners.Contacts.Queries.GetPartnerContacts;

public sealed class GetPartnerContactsQueryHandler
    : IRequestHandler<GetPartnerContactsQuery, List<PartnerContactDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetPartnerContactsQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<List<PartnerContactDto>> Handle(
        GetPartnerContactsQuery request, CancellationToken cancellationToken)
    {
        var partner = await _db.Partners.AsNoTracking()
            .FirstOrDefaultAsync(p => p.PartnerId == request.PartnerId, cancellationToken)
            ?? throw new NotFoundException("Partner", request.PartnerId);

        if (!PartnerAccess.CanViewPartner(_currentUser, partner))
            throw new AuthBusinessException(PartnerErrorCodes.Forbidden,
                "Bạn không có quyền xem người liên hệ của đối tác này.", 403);

        // 1. Auto-backfill missing PartnerContacts for confirmed links that were linked previously without a PartnerContact record.
        var unlinkedConfirmedLinks = await _db.VisitGuestPartnerLinks
            .Where(l => l.PartnerId == request.PartnerId && l.MatchStatus == "CONFIRMED")
            .ToListAsync(cancellationToken);

        if (unlinkedConfirmedLinks.Any())
        {
            var existingContactNames = await _db.PartnerContacts
                .Where(c => c.PartnerId == request.PartnerId)
                .Select(c => c.FullName.ToLower())
                .ToListAsync(cancellationToken);

            var hasChanges = false;
            foreach (var link in unlinkedConfirmedLinks)
            {
                string? targetName = null;
                string? targetJobTitle = null;
                string? targetEmail = null;

                if (link.GuestMemberId is { } gid)
                {
                    var g = await _db.VisitGuestMembers.AsNoTracking()
                        .FirstOrDefaultAsync(x => x.GuestMemberId == gid, cancellationToken);
                    if (g != null)
                    {
                        targetName = g.FullName;
                        targetJobTitle = g.JobTitle;
                    }
                }
                else if (link.MinuteParticipantId is { } mid)
                {
                    var mp = await _db.MinuteParticipants.AsNoTracking()
                        .FirstOrDefaultAsync(x => x.MinuteParticipantId == mid, cancellationToken);
                    if (mp != null)
                    {
                        targetName = mp.FullNameSnapshot;
                        targetJobTitle = mp.RoleSnapshot;
                        targetEmail = mp.EmailSnapshot;
                    }
                }

                if (!string.IsNullOrWhiteSpace(targetName))
                {
                    var cleanName = targetName.Trim();
                    if (!existingContactNames.Contains(cleanName.ToLower()))
                    {
                        var newContact = new PartnerContact
                        {
                            PartnerId = request.PartnerId,
                            FullName = cleanName,
                            JobTitle = string.IsNullOrWhiteSpace(targetJobTitle) ? null : targetJobTitle.Trim(),
                            Email = string.IsNullOrWhiteSpace(targetEmail) ? null : targetEmail.Trim(),
                            SourceType = "MANUAL",
                            Status = "ACTIVE",
                            IsPrimary = false,
                            CreatedAt = link.CreatedAt,
                            CreatedBy = link.CreatedBy,
                        };
                        _db.PartnerContacts.Add(newContact);
                        existingContactNames.Add(cleanName.ToLower());
                        hasChanges = true;
                    }
                }
            }

            if (hasChanges)
            {
                await _db.SaveChangesAsync(cancellationToken);
            }
        }

        var query = _db.PartnerContacts.AsNoTracking()
            .Where(c => c.PartnerId == request.PartnerId);
        if (!request.IncludeInactive)
            query = query.Where(c => c.Status == "ACTIVE");

        return await query
            .OrderByDescending(c => c.IsPrimary)
            .ThenBy(c => c.FullName)
            .Select(c => new PartnerContactDto
            {
                ContactId = c.ContactId,
                PartnerId = c.PartnerId,
                FullName = c.FullName,
                Email = c.Email,
                Phone = c.Phone,
                JobTitle = c.JobTitle,
                DepartmentName = c.DepartmentName,
                Note = c.Note,
                SourceType = c.SourceType,
                ScannedCardFileId = c.ScannedCardFileId,
                AvatarFileId = c.AvatarFileId,
                AvatarUrl = c.AvatarFileId.HasValue ? $"/api/files/{c.AvatarFileId}/content" : null,
                OcrConfidence = c.OcrConfidence,
                IsPrimary = c.IsPrimary,
                Status = c.Status,
                CreatedAt = c.CreatedAt,
            })
            .ToListAsync(cancellationToken);
    }
}
