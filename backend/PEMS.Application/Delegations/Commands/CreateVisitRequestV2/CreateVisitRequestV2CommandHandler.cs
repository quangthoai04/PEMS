using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Options;
using PEMS.Application.Notifications.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;

namespace PEMS.Application.Delegations.Commands.CreateVisitRequestV2;

public sealed class CreateVisitRequestV2CommandHandler
    : IRequestHandler<CreateVisitRequestV2Command, CreateVisitRequestV2Response>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;
    private readonly IVisitRequestV2CreateService _createService;
    private readonly INotificationService _notificationService;
    private readonly IVisitContactClaimService _contactClaimService;
    private readonly ILogger<CreateVisitRequestV2CommandHandler> _logger;
    private readonly PerCampusFormV2Options _readFlag;
    private readonly PerCampusFormV2WriteOptions _writeFlag;

    public CreateVisitRequestV2CommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock,
        IVisitRequestV2CreateService createService,
        INotificationService notificationService,
        IVisitContactClaimService contactClaimService,
        ILogger<CreateVisitRequestV2CommandHandler> logger,
        PerCampusFormV2Options readFlag, PerCampusFormV2WriteOptions writeFlag)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _createService = createService;
        _notificationService = notificationService;
        _contactClaimService = contactClaimService;
        _logger = logger;
        _readFlag = readFlag;
        _writeFlag = writeFlag;
    }

    public async Task<CreateVisitRequestV2Response> Handle(
        CreateVisitRequestV2Command request, CancellationToken cancellationToken)
    {
        // ── Flag gate ──
        // Write OFF → the endpoint is inert (404); only the v1 create flow exists.
        if (!_writeFlag.Enabled)
            throw new NotFoundException("Không tìm thấy.");
        // Write ON but read OFF → invalid config: we would create v2 records that no read path can surface.
        if (!_readFlag.Enabled)
            throw new ConflictException(
                "Cấu hình không hợp lệ: bật ghi v2 nhưng chưa bật đọc v2.",
                CreateVisitRequestV2ErrorCodes.ReadRequired);

        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new ForbiddenException();
        var registrantUserId = _currentUser.UserId.Value;

        var form = request.Form;
        if (string.IsNullOrWhiteSpace(form.SubmissionId))
            throw new BusinessRuleException("Thiếu submissionId.", "SUBMISSION_ID_REQUIRED");

        // ── Idempotency (sequential): a retry with the same submissionId returns the same request. ──
        var existing = await FindBySubmissionAsync(form.SubmissionId, cancellationToken);
        if (existing is not null)
            return await ToResponseAsync(existing.Value.RequestId, cancellationToken, idempotent: true);

        var now = _clock.VietnamNow;
        VisitRequest created;
        await using (var tx = await _db.BeginTransactionAsync(cancellationToken))
        {
            try
            {
                created = await _createService.CreateV2Async(
                    form, registrantUserId, "VISITOR_SUBMITTED", now, cancellationToken);
                await tx.CommitAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                // Concurrent same-submission may have lost the unique-index race
                // (uq_visit_requests_submission_id): roll our own attempt back and, if a winner now exists,
                // return it — never two requests. Any other DB error is NOT swallowed.
                await tx.RollbackAsync(cancellationToken);
                var dup = await FindBySubmissionAsync(form.SubmissionId, cancellationToken);
                if (dup is not null)
                    return await ToResponseAsync(dup.Value.RequestId, cancellationToken, idempotent: true);
                throw;
            }
        }

        // ── Post-commit notifications + INITIAL_CLAIM invitation (only on the first successful create).
        //    Dispatched AFTER commit so a rollback never notifies/invites; a same-submission replay takes
        //    the idempotent return paths above and never re-sends. Best-effort; see V2CreateNotifier. ──
        await V2CreateNotifier.NotifyStaffLeadersAfterCommitAsync(
            _db, _notificationService, _logger, created, cancellationToken);
        await V2CreateNotifier.SendContactClaimInvitationAfterCommitAsync(
            _db, _contactClaimService, _logger, created, cancellationToken);

        return await ToResponseAsync(created.VisitRequestId, cancellationToken, idempotent: false);
    }

    private async Task<(ulong RequestId, string RequestCode)?> FindBySubmissionAsync(
        string submissionId, CancellationToken ct)
    {
        var row = await _db.VisitRequests.AsNoTracking()
            .Where(v => v.SubmissionId == submissionId && v.FormSchemaVersion >= FormSchemaVersions.PerCampus)
            .Select(v => new { v.VisitRequestId, v.RequestCode })
            .FirstOrDefaultAsync(ct);
        return row is null ? null : (row.VisitRequestId, row.RequestCode ?? string.Empty);
    }

    private async Task<CreateVisitRequestV2Response> ToResponseAsync(
        ulong visitRequestId, CancellationToken ct, bool idempotent)
    {
        var head = await _db.VisitRequests.AsNoTracking()
            .Where(v => v.VisitRequestId == visitRequestId)
            .Select(v => new { v.RequestCode, v.VisitScope, v.HasMixedCampusDetails, v.PrimaryContactAccessStatus, v.VisitorUserId })
            .FirstAsync(ct);
        var instances = await _db.VisitRequestCampuses.AsNoTracking()
            .Where(c => c.VisitRequestId == visitRequestId)
            .OrderBy(c => c.CampusId)
            .Select(c => new CreateVisitRequestV2CampusRef(c.VisitInstanceId, c.CampusId, c.Status))
            .ToListAsync(ct);
        return new CreateVisitRequestV2Response(
            visitRequestId, head.RequestCode ?? string.Empty, head.VisitScope, head.HasMixedCampusDetails,
            head.PrimaryContactAccessStatus,
            ContactClaimPending: head.VisitorUserId is null,
            instances, idempotent);
    }
}
