using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using MediatR;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
using PEMS.Application.Accounts.Common;
// Alias the resolver: the sibling namespace …Queries.StaffLeaderAvailability shadows the type name.
using AvailabilityResolver = PEMS.Application.Accounts.Common.StaffLeaderAvailability;

namespace PEMS.Application.Accounts.Queries.StaffLeaderReplacementPreview;

/// <summary>
/// Replace Staff Leader preview. HO-only. Reuses <see cref="StaffLeaderAvailability"/> to evaluate
/// the campus/IC/leader state so the modal hint matches the write-side replace check, then lists
/// the eligible IC-Staff candidates (STAFF/STAFF, ACTIVE, same campus + IC dept).
/// </summary>
public sealed class GetStaffLeaderReplacementPreviewQueryHandler
    : IRequestHandler<GetStaffLeaderReplacementPreviewQuery, StaffLeaderReplacementPreviewDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetStaffLeaderReplacementPreviewQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<StaffLeaderReplacementPreviewDto> Handle(
        GetStaffLeaderReplacementPreviewQuery request, CancellationToken cancellationToken)
    {
        if (_currentUser.RoleCode != RoleCodes.Ho)
            throw new ForbiddenException("Bạn không có quyền thay thế Staff Leader.");

        if (request.CampusId == 0)
            throw new ValidationException("Vui lòng chọn cơ sở.");

        var avail = await AvailabilityResolver.ResolveAsync(_db, request.CampusId, cancellationToken);
        var canReplace = AvailabilityResolver.IsReplaceable(avail);

        // Blocking reason/message for the non-replaceable states (mirrors EnsureReplaceable, no throw).
        string? blockingReason = null;
        string message = "Có thể thay thế Staff Leader cho cơ sở này.";
        if (!canReplace)
        {
            blockingReason = avail.Kind == AvailabilityResolver.Outcome.CanCreate
                ? AccountErrorCodes.CampusHasNoStaffLeader
                : avail.BlockingReason;
            message = avail.Kind == AvailabilityResolver.Outcome.CanCreate
                ? "Cơ sở này chưa có Staff Leader. Vui lòng dùng chức năng tạo Staff Leader."
                : avail.Message;
        }

        // Eligible replacement candidates: IC Staff (STAFF/STAFF, ACTIVE) of this campus + IC dept,
        // excluding the current leader. Only meaningful when there is a replaceable leader.
        var candidates = new List<ReplacementCandidateDto>();
        if (canReplace && avail.IcDepartmentId is not null)
        {
            var currentLeaderId = avail.Leader!.UserId;
            candidates = await _db.Users.AsNoTracking()
                .Where(u => u.PrimaryCampusId == request.CampusId
                         && u.DepartmentId == avail.IcDepartmentId
                         && u.Role.RoleCode == RoleCodes.Staff
                         && u.SubRole == UserSubRoles.Staff
                         && u.Status == UserStatuses.Active
                         && u.UserId != currentLeaderId)
                .OrderBy(u => u.FullName)
                .Select(u => new ReplacementCandidateDto
                {
                    UserId = u.UserId,
                    FullName = u.FullName,
                    Email = u.Email,
                    Status = u.Status,
                    RoleCode = u.Role.RoleCode,
                    SubRole = u.SubRole,
                })
                .ToListAsync(cancellationToken);
        }

        return new StaffLeaderReplacementPreviewDto
        {
            CampusId = avail.CampusId,
            CampusName = avail.CampusName,
            CampusStatus = avail.Kind switch
            {
                AvailabilityResolver.Outcome.CampusNotFound => null,
                AvailabilityResolver.Outcome.CampusInactive => EntityStatuses.Inactive,
                _ => EntityStatuses.Active,
            },
            IcDepartmentId = avail.IcDepartmentId,
            IcDepartmentName = avail.IcDepartmentName,
            CurrentLeader = avail.Leader is null ? null : new ReplacementLeaderDto
            {
                UserId = avail.Leader.UserId,
                FullName = avail.Leader.FullName,
                Email = avail.Leader.Email,
                Status = avail.Leader.Status,
                RoleCode = RoleCodes.Staff,
                SubRole = UserSubRoles.Leader,
            },
            EligibleCandidates = candidates,
            CanReplace = canReplace,
            BlockingReason = blockingReason,
            Message = message,
        };
    }
}
