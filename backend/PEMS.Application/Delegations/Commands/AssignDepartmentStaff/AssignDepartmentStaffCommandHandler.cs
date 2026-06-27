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
        if (_currentUser.RoleCode != RoleCodes.Department || _currentUser.SubRole != UserSubRoles.Leader)
            throw new ForbiddenException("Chỉ Department Leader mới được giao việc xuống Staff.");

        var participantUser = await _db.Users.FirstOrDefaultAsync(u => u.UserId == leaderParticipant.UserId, cancellationToken);
        if (participantUser?.DepartmentId != _currentUser.DepartmentId)
            throw new ForbiddenException("Bạn chỉ có thể giao nhiệm vụ thuộc phòng ban của mình.");

        // if (leaderParticipant.ParticipantRole != ParticipantRoles.DeptSupport)
        //     throw new ConflictException("Chỉ có thể giao nhiệm vụ từ vai trò DEPT_SUPPORT.");

        var targetStaff = await _db.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.UserId == request.DepartmentStaffUserId, cancellationToken)
            ?? throw new NotFoundException("User", request.DepartmentStaffUserId);

        // Kiểm tra target user cũng phải là DEPT và cùng department
        if (targetStaff.Role?.RoleCode != RoleCodes.Department || targetStaff.DepartmentId != _currentUser.DepartmentId)
            throw new ConflictException("Người được phân công phải thuộc cùng phòng ban.");

        var now = _clock.UtcNow;

        var existingParticipant = await _db.VisitParticipants
            .FirstOrDefaultAsync(p => p.VisitInstanceId == leaderParticipant.VisitInstanceId && p.UserId == targetStaff.UserId, cancellationToken);

        VisitParticipant assignedParticipant;
        if (existingParticipant != null)
        {
            // Nếu đã tham gia, cập nhật role và status nếu cần thiết
            existingParticipant.ParticipantRole = ParticipantRoles.DeptSupport;
            existingParticipant.Status = ParticipantStatuses.Assigned;
            existingParticipant.AssignedBy = userId;
            existingParticipant.AssignedAt = now;
            existingParticipant.UpdatedAt = now;
            existingParticipant.UpdatedBy = userId;
            assignedParticipant = existingParticipant;
        }
        else
        {
            // Tạo participant mới cho Staff
            assignedParticipant = new VisitParticipant
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

            _db.VisitParticipants.Add(assignedParticipant);
        }

        leaderParticipant.Status = ParticipantStatuses.Assigned;
        leaderParticipant.UpdatedAt = now;
        leaderParticipant.UpdatedBy = userId;

        if (existingParticipant != null)
        {
            await PEMS.Application.EmailActions.EmailTokenInvalidationHelper.InvalidatePendingEmailActionTokensAsync(
                _db, EmailActionTargetTypes.VisitParticipant, existingParticipant.ParticipantId, "Thành phần tham gia đã được phân công trực tiếp.", now, cancellationToken);
        }

        await PEMS.Application.EmailActions.EmailTokenInvalidationHelper.InvalidatePendingEmailActionTokensAsync(
            _db, EmailActionTargetTypes.VisitParticipant, leaderParticipant.ParticipantId, "Thành phần tham gia đã được phân công trực tiếp.", now, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);

        return assignedParticipant.ParticipantId;
    }
}
