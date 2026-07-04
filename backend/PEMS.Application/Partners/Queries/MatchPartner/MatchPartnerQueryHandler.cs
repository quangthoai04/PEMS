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

        // Flag which candidates the caller may actually link to (campus scope), so the picker
        // can disable out-of-scope rows instead of failing on click.
        foreach (var c in result.Candidates)
        {
            c.CanLink = PartnerAccess.CanViewPartner(_currentUser, new Partner
            {
                OwnerCampusId = c.OwnerCampusId,
                ProfileStatus = c.ProfileStatus,
                Visibility = c.Visibility,
            });
        }

        return result;
    }
}
