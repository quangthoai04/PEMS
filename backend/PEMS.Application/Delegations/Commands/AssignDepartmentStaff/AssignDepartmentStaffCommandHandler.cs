using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;

namespace PEMS.Application.Delegations.Commands.AssignDepartmentStaff;

public sealed class AssignDepartmentStaffCommandHandler : IRequestHandler<AssignDepartmentStaffCommand, ulong>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;

    public AssignDepartmentStaffCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<ulong> Handle(AssignDepartmentStaffCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new ForbiddenException();

        var userId = _currentUser.UserId.Value;

        var leaderParticipant = await _db.VisitParticipants
            .FirstOrDefaultAsync(p => p.ParticipantId == request.ParticipantId, cancellationToken)
            ?? throw new NotFoundException("VisitParticipant", request.ParticipantId);

        // Chỉ Department Leader được phân công
        if (_currentUser.RoleCode != RoleCodes.Department || _currentUser.SubRole != SubRoles.Leader)
            throw new ForbiddenException("Chỉ Department Leader mới được giao việc xuống Staff.");

        if (leaderParticipant.UserId != userId)
            throw new ForbiddenException("Bạn chỉ có thể giao nhiệm vụ từ lời mời của chính mình.");

        if (leaderParticipant.ParticipantRole != ParticipantRoles.DeptSupport)
            throw new ConflictException("Chỉ có thể giao nhiệm vụ từ vai trò DEPT_SUPPORT.");

        var targetStaff = await _db.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.UserId == request.DepartmentStaffUserId, cancellationToken)
            ?? throw new NotFoundException("User", request.DepartmentStaffUserId);

        // Kiểm tra target user cũng phải là DEPT và cùng department
        if (targetStaff.Role?.RoleCode != RoleCodes.Department || targetStaff.DepartmentId != _currentUser.DepartmentId)
            throw new ConflictException("Người được phân công phải thuộc cùng phòng ban.");

        var now = _clock.UtcNow;

        // Tạo participant mới cho Staff
        var staffParticipant = new VisitParticipant
        {
            VisitInstanceId = leaderParticipant.VisitInstanceId,
            UserId = targetStaff.UserId,
            ParticipantRole = ParticipantRoles.DeptSupport,
            IsHost = false,
            Status = ParticipantStatuses.Assigned,
            AssignedBy = userId,
            AssignedAt = now,
            Note = request.Note,
            CreatedAt = now,
            CreatedBy = userId
        };

        _db.VisitParticipants.Add(staffParticipant);
        await _db.SaveChangesAsync(cancellationToken);

        return staffParticipant.ParticipantId;
    }
}
