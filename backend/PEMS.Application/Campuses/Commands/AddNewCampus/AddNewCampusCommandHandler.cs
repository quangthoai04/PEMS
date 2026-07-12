using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using PEMS.Application.Campuses.Common;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Security;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Campuses;
using PEMS.Domain.Entities.Departments;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.Campuses.Commands.AddNewCampus;

/// <summary>
/// UC-81 handler. Validates HO access + uniqueness, then in ONE transaction inserts the
/// campus (status ACTIVE, ic_head_user_id NULL — BR-81-01/02) and its default IC department
/// "Phòng Hợp tác Quốc tế" (BR-81-03), so a failure to create the department rolls back the
/// campus (BR-81-04 / AF-04). Writes an audit log.
/// </summary>
public sealed class AddNewCampusCommandHandler : IRequestHandler<AddNewCampusCommand, AddNewCampusResponse>
{
    private const string IcDepartmentName = "Phòng Hợp tác Quốc tế";

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IRoleAccessPolicy _accessPolicy;
    private readonly IDateTimeService _clock;

    public AddNewCampusCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IRoleAccessPolicy accessPolicy,
        IDateTimeService clock)
    {
        _db = db;
        _currentUser = currentUser;
        _accessPolicy = accessPolicy;
        _clock = clock;
    }

    public async Task<AddNewCampusResponse> Handle(AddNewCampusCommand request, CancellationToken cancellationToken)
    {
        if (!_accessPolicy.CanAccessCampusManagement(_currentUser))
        {
            throw new AuthBusinessException(
                CampusErrorCodes.CampusManagementForbidden,
                "Bạn không có quyền tạo campus.", 403);
        }

        // ── Normalize (00_COMMON_RULES §4.1) ──
        var code = CampusNormalization.Code(request.CampusCode);
        var name = CampusNormalization.Text(request.Name);
        var city = CampusNormalization.City(request.City);
        var address = CampusNormalization.Text(request.Address);
        var phone = CampusNormalization.PhoneDisplay(request.Phone);
        var email = CampusNormalization.Email(request.Email);

        // ── Duplicate check — only city may duplicate (BR-81-05) ──
        await CampusDuplicateGuard.EnsureUniqueAsync(
            _db, code, name, address, phone, email, excludeCampusId: null, cancellationToken);

        var actorId = _currentUser.UserId;
        var now = _clock.VietnamNow;

        var campus = new Campus
        {
            CampusCode = code,
            Name = name,
            City = city,
            Address = address,
            Phone = phone,
            Email = email,
            IcHeadUserId = null,
            Status = EntityStatuses.Active,
            CreatedAt = now,
            CreatedBy = actorId,
        };

        // ── Campus + IC department must commit atomically (BR-81-04 / AF-04) ──
        await using var transaction = await _db.BeginTransactionAsync(cancellationToken);
        Department icDepartment;
        try
        {
            _db.Campuses.Add(campus);
            await _db.SaveChangesAsync(cancellationToken); // populates campus.CampusId

            icDepartment = new Department
            {
                CampusId = campus.CampusId,
                Name = IcDepartmentName,
                DepartmentType = "IC",
                HeadUserId = null,
                Status = EntityStatuses.Active,
                CreatedAt = now,
                CreatedBy = actorId,
            };
            _db.Departments.Add(icDepartment);

            _db.AuditLogs.Add(new AuditLog
            {
                ActorUserId = actorId,
                CampusId = campus.CampusId,
                Action = "CREATE_CAMPUS",
                EntityType = "Campus",
                EntityId = campus.CampusId,
                Changes = new List<AuditLogChange>
                {
                    new AuditLogChange
                    {
                        FieldName = "Campus",
                        NewValueText = JsonSerializer.Serialize(new { code, name, city, address, phone, email })
                    }
                },
                CreatedAt = now,
            });

            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return new AddNewCampusResponse
        {
            CampusId = campus.CampusId,
            CampusCode = campus.CampusCode,
            Name = campus.Name,
            City = campus.City!,
            Address = campus.Address!,
            Phone = campus.Phone!,
            Email = campus.Email!,
            IcHeadUserId = null,
            Status = campus.Status,
            IcDepartment = new AddNewCampusResponse.IcDepartmentInfo
            {
                DepartmentId = icDepartment.DepartmentId,
                CampusId = campus.CampusId,
                Name = icDepartment.Name,
                DepartmentType = icDepartment.DepartmentType,
                Status = icDepartment.Status,
            },
        };
    }
}
