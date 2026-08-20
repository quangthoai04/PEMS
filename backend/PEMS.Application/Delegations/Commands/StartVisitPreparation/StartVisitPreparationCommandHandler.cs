using System.Linq;
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

namespace PEMS.Application.Delegations.Commands.StartVisitPreparation;

/// <summary>
/// ASSIGNED → BEFORE_VISIT, by the campus's current Host and nobody else.
///
/// <para>
/// Everything is re-derived from committed state: the campus really belongs to the request in the
/// route, the request and campus are alive, the confirmation gate is open, the campus really is
/// ASSIGNED, it really has a Host, and that Host really is the caller and still ACTIVE. None of it is
/// taken from the request body or a JWT claim.
/// </para>
/// <para>
/// Sibling campuses are untouched by construction: the only row written is this instance.
/// </para>
/// </summary>
public sealed class StartVisitPreparationCommandHandler
    : IRequestHandler<StartVisitPreparationCommand, StartVisitPreparationResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;

    public StartVisitPreparationCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<StartVisitPreparationResponse> Handle(
        StartVisitPreparationCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new ForbiddenException();

        var actorId = _currentUser.UserId.Value;

        await using var tx = await _db.BeginTransactionAsync(cancellationToken);

        // Fixed lock order request → campus, the same order every other campus mutation takes, so two
        // concurrent lifecycle actions on one request queue instead of deadlocking.
        var visit = await _db.VisitRequests
            .FromSqlRaw("SELECT * FROM visit_requests WHERE visit_request_id = {0} FOR UPDATE",
                request.VisitRequestId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("VisitRequest", request.VisitRequestId);

        var instance = await _db.VisitRequestCampuses
            .FromSqlRaw("SELECT * FROM visit_request_campuses WHERE visit_instance_id = {0} FOR UPDATE",
                request.VisitInstanceId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("VisitRequestCampus", request.VisitInstanceId);

        // The campus must belong to the request NAMED IN THE ROUTE. Without this a Host of campus X
        // could pass their own instance id under somebody else's request id and have every later check
        // pass — the checks below all read the instance, not the request.
        if (instance.VisitRequestId != request.VisitRequestId)
            throw new NotFoundException("VisitRequestCampus", request.VisitInstanceId);

        if (visit.Status == VisitRequestStatuses.Cancelled)
            throw new ConflictException("Đơn đã bị hủy nên không thể bắt đầu chuẩn bị.");
        if (visit.Status == VisitRequestStatuses.Rejected)
            throw new ConflictException("Đơn đã bị từ chối nên không thể bắt đầu chuẩn bị.");

        // The gate is a property of the whole request: a campus whose own contact confirmed still must
        // not move while a sibling campus has nobody.
        if (VisitRequestStatuses.IsBehindContactGate(visit.Status))
            throw new ConflictException(
                "Đơn chưa đủ đầu mối vận hành xác nhận nên chưa thể bắt đầu chuẩn bị.",
                OperationalContactErrorCodes.ContactConfirmationRequired);

        if (instance.Status == VisitInstanceStatuses.Cancelled)
            throw new ConflictException("Cơ sở này đã bị hủy nên không thể bắt đầu chuẩn bị.");
        if (instance.Status == VisitInstanceStatuses.Rejected)
            throw new ConflictException("Cơ sở này đã bị từ chối nên không thể bắt đầu chuẩn bị.");

        // ── Idempotent replay, decided BEFORE the ASSIGNED check ──────────────────────────────────
        // A double-click, a retried request or a refresh that fires twice must not read as an error to
        // the person who just legitimately started preparation. It is scoped as tightly as it can be:
        // the campus is exactly one step on, and the caller is the Host who owns it. Anyone else — and
        // any campus further along than BEFORE_VISIT — gets the conflict.
        if (instance.Status == VisitInstanceStatuses.BeforeVisit)
        {
            await tx.CommitAsync(cancellationToken);

            if (instance.CurrentHostUserId == actorId)
                return new StartVisitPreparationResponse(
                    instance.VisitRequestId, instance.VisitInstanceId, instance.Status,
                    instance.RowVersion, AlreadyStarted: true,
                    "Cơ sở này đã ở giai đoạn chuẩn bị.");

            throw new ConflictException(
                "Cơ sở này đã được bắt đầu chuẩn bị.",
                VisitRequestErrorCodes.VisitPreparationAlreadyStarted);
        }

        if (instance.Status != VisitInstanceStatuses.Assigned)
            throw new ConflictException(
                "Chỉ có thể bắt đầu chuẩn bị khi cơ sở vừa được duyệt và đã có Host phụ trách.",
                VisitRequestErrorCodes.VisitPreparationAlreadyStarted);

        // A campus at ASSIGNED always has a Host (approve writes both in one update). Checking anyway:
        // if that invariant ever broke, silently letting the caller "become" the Host would be worse
        // than a 409.
        if (instance.CurrentHostUserId is null)
            throw new ConflictException("Cơ sở này chưa có Host phụ trách.");

        if (instance.CurrentHostUserId != actorId)
            throw new ForbiddenException("Chỉ Host phụ trách cơ sở này mới được bắt đầu chuẩn bị.");

        // Being the Host on the row is not enough — the account has to still be usable. A Host whose
        // account was disabled or moved out of the campus must not be able to open preparation.
        var host = await _db.Users.AsNoTracking()
            .Where(u => u.UserId == actorId)
            .Select(u => new { u.Status, u.PrimaryCampusId })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new ForbiddenException("Tài khoản không hợp lệ.");
        if (!string.Equals(host.Status, UserStatuses.Active, System.StringComparison.OrdinalIgnoreCase))
            throw new ForbiddenException("Tài khoản của bạn không còn hoạt động.");
        if (host.PrimaryCampusId != instance.CampusId)
            throw new ForbiddenException("Cơ sở này không thuộc phạm vi phụ trách của bạn.");

        // Optimistic concurrency, only when the caller stated what they were looking at.
        if (request.ExpectedRowVersion is { } expected && expected != instance.RowVersion)
            throw new ConflictException(
                "Dữ liệu cơ sở đã thay đổi. Vui lòng tải lại và thử lại.",
                CampusScopeErrorCodes.ConcurrencyConflict);

        var now = _clock.VietnamNow;

        instance.Status = VisitInstanceStatuses.BeforeVisit;
        instance.UpdatedAt = now;
        instance.UpdatedBy = actorId;
        instance.RowVersion += 1;

        // The append-only history of what happened to this campus. Same shape as the approve/reject
        // decision audit, so one query over audit_logs still tells the whole campus story in order.
        // Action/SourceType are the shared lifecycle vocabulary (VisitLifecycleHistoryAudit) so this
        // writer and the two history readers cannot name the same transition two different ways.
        var lifecycleAudit = new AuditLog
        {
            ActorUserId = actorId,
            Action = VisitLifecycleHistoryAudit.PreparationStarted,
            EntityType = "VisitRequestCampus",
            EntityId = instance.VisitInstanceId,
            CampusId = instance.CampusId,
            VisitRequestId = instance.VisitRequestId,
            VisitInstanceId = instance.VisitInstanceId,
            SourceType = VisitLifecycleHistoryAudit.SourceType,
            Reason = $"fromStatus={VisitInstanceStatuses.Assigned};toStatus={VisitInstanceStatuses.BeforeVisit}",
            CreatedAt = now,
        };
        lifecycleAudit.Changes.Add(new AuditLogChange
        {
            FieldName = "visit_request_campuses.status",
            OldValueText = VisitInstanceStatuses.Assigned,
            NewValueText = VisitInstanceStatuses.BeforeVisit,
            CreatedAt = now,
        });
        _db.AuditLogs.Add(lifecycleAudit);

        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        return new StartVisitPreparationResponse(
            instance.VisitRequestId, instance.VisitInstanceId, instance.Status,
            instance.RowVersion, AlreadyStarted: false,
            "Đã bắt đầu giai đoạn chuẩn bị cho cơ sở này.");
    }
}
