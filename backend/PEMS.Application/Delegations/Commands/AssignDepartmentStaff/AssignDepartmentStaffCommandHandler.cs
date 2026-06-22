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

        // Chá»‰ Department Leader Ä‘Æ°á»£c phÃ¢n cÃ´ng
        if (_currentUser.RoleCode != RoleCodes.Department || _currentUser.SubRole != UserSubRoles.Leader)
            throw new ForbiddenException("Chá»‰ Department Leader má»›i Ä‘Æ°á»£c giao viá»‡c xuá»‘ng Staff.");

        if (leaderParticipant.UserId != userId)
            throw new ForbiddenException("Báº¡n chá»‰ cÃ³ thá»ƒ giao nhiá»‡m vá»¥ tá»« lá»i má»i cá»§a chÃ­nh mÃ¬nh.");

        if (leaderParticipant.ParticipantRole != ParticipantRoles.DeptSupport)
            throw new ConflictException("Chá»‰ cÃ³ thá»ƒ giao nhiá»‡m vá»¥ tá»« vai trÃ² DEPT_SUPPORT.");

        var targetStaff = await _db.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.UserId == request.DepartmentStaffUserId, cancellationToken)
            ?? throw new NotFoundException("User", request.DepartmentStaffUserId);

        // Kiá»ƒm tra target user cÅ©ng pháº£i lÃ  DEPT vÃ  cÃ¹ng department
        if (targetStaff.Role?.RoleCode != RoleCodes.Department || targetStaff.DepartmentId != _currentUser.DepartmentId)
            throw new ConflictException("NgÆ°á»i Ä‘Æ°á»£c phÃ¢n cÃ´ng pháº£i thuá»™c cÃ¹ng phÃ²ng ban.");

        var now = _clock.UtcNow;

        // Táº¡o participant má»›i cho Staff
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
