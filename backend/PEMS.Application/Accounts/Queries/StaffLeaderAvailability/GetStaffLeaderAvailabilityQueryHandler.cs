using MediatR;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
// Alias the resolver: this query's own namespace (…Queries.StaffLeaderAvailability) otherwise
// shadows the Common.StaffLeaderAvailability type name.
using AvailabilityResolver = PEMS.Application.Accounts.Common.StaffLeaderAvailability;

namespace PEMS.Application.Accounts.Queries.StaffLeaderAvailability;

/// <summary>
/// UC-96 — Staff Leader availability pre-check. HO-only; campus is whatever HO picked in the
/// modal. Delegates the case-matrix evaluation to <see cref="StaffLeaderAvailability"/> so the
/// hint can never disagree with the write-side check in CreateAccount.
/// </summary>
public sealed class GetStaffLeaderAvailabilityQueryHandler
    : IRequestHandler<GetStaffLeaderAvailabilityQuery, StaffLeaderAvailabilityDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetStaffLeaderAvailabilityQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<StaffLeaderAvailabilityDto> Handle(
        GetStaffLeaderAvailabilityQuery request, CancellationToken cancellationToken)
    {
        // Only HO creates Staff Leaders, so only HO needs this pre-check (BR-SL-01/22).
        if (_currentUser.RoleCode != RoleCodes.Ho)
            throw new ForbiddenException("Chỉ Head Office mới được kiểm tra khả năng tạo Trưởng phòng IC.");

        if (request.CampusId == 0)
            throw new ValidationException("Vui lòng chọn cơ sở.");

        var result = await AvailabilityResolver.ResolveAsync(_db, request.CampusId, cancellationToken);

        return new StaffLeaderAvailabilityDto
        {
            CampusId = result.CampusId,
            CampusName = result.CampusName,
            CanCreateStaffLeader = result.CanCreate,
            IcDepartmentId = result.IcDepartmentId,
            IcDepartmentName = result.IcDepartmentName,
            ExistingLeader = result.Leader is null ? null : new ExistingLeaderDto
            {
                UserId = result.Leader.UserId,
                FullName = result.Leader.FullName,
                Email = result.Leader.Email,
                Status = result.Leader.Status,
            },
            BlockingReason = result.BlockingReason,
            Message = result.Message,
        };
    }
}
