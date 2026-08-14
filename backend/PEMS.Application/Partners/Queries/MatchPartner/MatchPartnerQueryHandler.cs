using System.Threading;
using System.Threading.Tasks;
using MediatR;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Partners.Common;
using PEMS.Domain.Entities.Partners;

namespace PEMS.Application.Partners.Queries.MatchPartner;

public sealed class MatchPartnerQueryHandler : IRequestHandler<MatchPartnerQuery, PartnerMatchDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public MatchPartnerQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<PartnerMatchDto> Handle(MatchPartnerQuery request, CancellationToken cancellationToken)
    {
        if (!PartnerAccess.CanViewPartnerModule(_currentUser))
            throw new AuthBusinessException(PartnerErrorCodes.Forbidden,
                "Bạn không có quyền sử dụng chức năng đối chiếu đối tác.", 403);

        var result = await PartnerMatcher.MatchAsync(_db, request.Organization, request.Email, cancellationToken);

        // Flag which candidates the caller may actually CONFIRM a link to, and why not when they
        // can't, so the picker can explain the row instead of failing on click. The link policy is
        // profile-status aware — reading a rejected profile is fine, linking to it is not (PART-04).
        foreach (var c in result.Candidates)
        {
            var probe = new Partner
            {
                OwnerCampusId = c.OwnerCampusId,
                ProfileStatus = c.ProfileStatus,
                Visibility = c.Visibility,
            };
            var blockReason = PartnerAccess.LinkBlockReason(_currentUser, probe);
            c.CanLink = blockReason is null;
            c.BlockedReason = blockReason;
            c.RecommendedAction = PartnerAccess.RecommendedActionFor(blockReason);
            // Never hand back the rejection note of a profile the caller cannot even see.
            if (blockReason == PartnerLinkBlockReasons.OutOfScope) c.ReviewNote = null;
        }

        return result;
    }
}
