using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.Services;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Users;
using PEMS.Shared;

namespace PEMS.Application.Delegations.Commands.UpdateRegistrantInfo;

public sealed class UpdateRegistrantInfoCommandHandler
    : IRequestHandler<UpdateRegistrantInfoCommand, UpdateRegistrantInfoResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;

    public UpdateRegistrantInfoCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<UpdateRegistrantInfoResponse> Handle(
        UpdateRegistrantInfoCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new ForbiddenException();

        var actorId = _currentUser.UserId.Value;

        var visit = await _db.VisitRequests
            .FirstOrDefaultAsync(v => v.VisitRequestId == request.VisitRequestId, cancellationToken)
            ?? throw new NotFoundException("VisitRequest", request.VisitRequestId);

        // A Visitor-submitted request's registrant IS the guest — never editable internally.
        if (visit.CreatedSource != CreatedSource.StaffCreated)
            throw new ForbiddenException(
                "Thông tin người đăng ký do khách tự gửi nên không thể chỉnh sửa.");

        // Internally-created request: only the creator may edit the registrant info.
        if (visit.CreatedBy != actorId)
            throw new ForbiddenException(
                "Chỉ người tạo đơn mới được chỉnh sửa thông tin người đăng ký.");

        // Cannot edit a finalized request.
        if (visit.Status == VisitRequestStatuses.Cancelled)
            throw new ConflictException("Đơn đã bị hủy nên không thể chỉnh sửa thông tin người đăng ký.");
        if (visit.Status == VisitRequestStatuses.Rejected)
            throw new ConflictException("Đơn đã bị từ chối nên không thể chỉnh sửa thông tin người đăng ký.");

        var now = _clock.VietnamNow;
        visit.RegistrantFullName = request.FullName.Trim();
        visit.RegistrantOrganization = request.Organization.Trim();
        visit.RegistrantJobTitle = (request.JobTitle ?? string.Empty).Trim();
        visit.RegistrantPhone = PhoneNumber.NormalizeOrOriginal(request.Phone.Trim());
        // Patch 5: persisted normalized (trim + lowercase) — this command is the one place a
        // staff-entered (non-Visitor-submitted) registrant's email CAN change at all; it does not
        // touch that ability, only how the resulting value is stored, matching every other write of
        // this same column and the value users.Email/partner_contacts.email already always hold.
        visit.RegistrantEmail = VisitRequestFingerprintBuilder.NormalizeEmail(request.Email);
        visit.UpdatedAt = now;
        visit.UpdatedBy = actorId;
        visit.RowVersion += 1;

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = actorId,
            Action = "UPDATE_REGISTRANT_INFO",
            EntityType = "VisitRequest",
            EntityId = visit.VisitRequestId,
            CreatedAt = now
        });

        await _db.SaveChangesAsync(cancellationToken);

        return new UpdateRegistrantInfoResponse(
            visit.VisitRequestId,
            visit.RegistrantFullName,
            visit.RegistrantOrganization,
            visit.RegistrantJobTitle,
            visit.RegistrantPhone,
            visit.RegistrantEmail,
            "Đã cập nhật thông tin người đăng ký.");
    }
}
