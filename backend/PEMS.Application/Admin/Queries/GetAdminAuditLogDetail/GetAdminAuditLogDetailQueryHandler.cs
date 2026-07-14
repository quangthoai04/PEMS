using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Admin.Common;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;

namespace PEMS.Application.Admin.Queries.GetAdminAuditLogDetail;

public sealed class GetAdminAuditLogDetailQueryHandler
    : IRequestHandler<GetAdminAuditLogDetailQuery, AdminAuditLogDetailDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetAdminAuditLogDetailQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<AdminAuditLogDetailDto> Handle(
        GetAdminAuditLogDetailQuery request, CancellationToken cancellationToken)
    {
        AdminAccess.EnsureAdmin(_currentUser);

        var log = await _db.AuditLogs.AsNoTracking()
            .Include(a => a.Changes)
            .Include(a => a.ActorUser).ThenInclude(u => u!.Role)
            .FirstOrDefaultAsync(a => a.AuditLogId == request.AuditLogId, cancellationToken)
            ?? throw new NotFoundException("AuditLog", request.AuditLogId);

        string? campusName = null;
        if (log.CampusId.HasValue)
        {
            campusName = await _db.Campuses.AsNoTracking()
                .Where(c => c.CampusId == log.CampusId.Value)
                .Select(c => c.Name)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return new AdminAuditLogDetailDto
        {
            AuditLogId = log.AuditLogId,
            ActorUserId = log.ActorUserId,
            ActorName = log.ActorUser?.FullName,
            ActorEmail = log.ActorUser?.Email,
            ActorRoleCode = log.ActorUser?.Role?.RoleCode,
            Action = log.Action,
            EntityType = log.EntityType,
            EntityId = log.EntityId,
            CampusId = log.CampusId,
            CampusName = campusName,
            IpAddress = log.IpAddress,
            UserAgent = log.UserAgent,
            RequestId = log.RequestId,
            CreatedAt = log.CreatedAt,
            Changes = log.Changes
                .OrderBy(c => c.AuditLogChangeId)
                .Select(c => new AdminAuditLogChangeDto
                {
                    AuditLogChangeId = c.AuditLogChangeId,
                    FieldName = c.FieldName,
                    OldValue = SensitiveDataMask.MaskValue(c.FieldName, c.OldValueText),
                    NewValue = SensitiveDataMask.MaskValue(c.FieldName, c.NewValueText),
                    IsMasked = SensitiveDataMask.IsSensitiveField(c.FieldName),
                })
                .ToList(),
        };
    }
}
