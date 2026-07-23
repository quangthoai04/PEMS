using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.VisitPhotos.FaceScans.Common;

namespace PEMS.Application.Delegations.VisitPhotos.FaceScans.Queries.GetTaggableGuests;

public sealed class GetTaggableGuestsQueryHandler
    : IRequestHandler<GetTaggableGuestsQuery, List<TaggableGuestDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetTaggableGuestsQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<List<TaggableGuestDto>> Handle(
        GetTaggableGuestsQuery request, CancellationToken cancellationToken)
    {
        await VisitPhotoFaceScanAccess.ResolveStaffAsync(_db, _currentUser, request.VisitInstanceId, cancellationToken);

        // visit_instance_guest_members (visit_instance_id, guest_member_id) only — never a campus
        // instance the caller didn't ask for, never another delegation's guests.
        return await _db.VisitInstanceGuestMembers.AsNoTracking()
            .Where(l => l.VisitInstanceId == request.VisitInstanceId)
            .OrderBy(l => l.DisplayOrder)
            .Select(l => new TaggableGuestDto
            {
                GuestMemberId = l.GuestMember.GuestMemberId,
                FullName = l.GuestMember.FullName,
                MemberType = l.GuestMember.MemberType,
                Organization = l.GuestMember.Organization,
                JobTitle = l.GuestMember.JobTitle,
                Nationality = l.GuestMember.Nationality,
            })
            .ToListAsync(cancellationToken);
    }
}
