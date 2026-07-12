using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Campuses.Common;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Security;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.Campuses.Commands.UpdateCampus;

/// <summary>
/// UC-85 handler. Updates ONLY campus master data (code/name/city/address/phone/email);
/// status, ic_head_user_id, created_* and the IC department are preserved (BR-85-02..04).
/// Duplicate checks exclude the current campus (§10); writes updated_by/at + an audit log.
/// </summary>
public sealed class UpdateCampusCommandHandler : IRequestHandler<UpdateCampusCommand, UpdateCampusResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IRoleAccessPolicy _accessPolicy;
    private readonly IDateTimeService _clock;

    public UpdateCampusCommandHandler(
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

    public async Task<UpdateCampusResponse> Handle(UpdateCampusCommand request, CancellationToken cancellationToken)
    {
        if (!_accessPolicy.CanAccessCampusManagement(_currentUser))
        {
            throw new AuthBusinessException(
                CampusErrorCodes.CampusManagementForbidden,
                "Bạn không có quyền chỉnh sửa campus.", 403);
        }

        var campus = await _db.Campuses
            .FirstOrDefaultAsync(c => c.CampusId == request.CampusId, cancellationToken)
            ?? throw new NotFoundException("Campus", request.CampusId);

        // ── Normalize (00_COMMON_RULES §4.1) ──
        var code = CampusNormalization.Code(request.CampusCode);
        var name = CampusNormalization.Text(request.Name);
        var city = CampusNormalization.City(request.City);
        var address = CampusNormalization.Text(request.Address);
        var phone = CampusNormalization.PhoneDisplay(request.Phone);
        var email = CampusNormalization.Email(request.Email);

        // ── Duplicate check excluding the current campus (BR-85-05, §10) ──
        await CampusDuplicateGuard.EnsureUniqueAsync(
            _db, code, name, address, phone, email, excludeCampusId: campus.CampusId, cancellationToken);

        var before = new { campus.CampusCode, campus.Name, campus.City, campus.Address, campus.Phone, campus.Email };
        var actorId = _currentUser.UserId;
        var now = _clock.VietnamNow;

        campus.CampusCode = code;
        campus.Name = name;
        campus.City = city;
        campus.Address = address;
        campus.Phone = phone;
        campus.Email = email;
        campus.UpdatedAt = now;
        campus.UpdatedBy = actorId;

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = actorId,
            CampusId = campus.CampusId,
            Action = "UPDATE_CAMPUS",
            EntityType = "Campus",
            EntityId = campus.CampusId,
            Changes = new List<AuditLogChange>
            {
                new AuditLogChange
                {
                    FieldName = "Campus",
                    OldValueText = JsonSerializer.Serialize(before),
                    NewValueText = JsonSerializer.Serialize(new { code, name, city, address, phone, email })
                }
            },
            CreatedAt = now,
        });

        await _db.SaveChangesAsync(cancellationToken);

        return new UpdateCampusResponse
        {
            CampusId = campus.CampusId,
            CampusCode = campus.CampusCode,
            Name = campus.Name,
            City = campus.City!,
            Address = campus.Address!,
            Phone = campus.Phone!,
            Email = campus.Email!,
            Status = campus.Status,
            UpdatedAt = now,
            UpdatedBy = actorId,
        };
    }
}
