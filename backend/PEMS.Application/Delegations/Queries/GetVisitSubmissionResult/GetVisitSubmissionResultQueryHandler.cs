using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;

namespace PEMS.Application.Delegations.Queries.GetVisitSubmissionResult;

/// <summary>
/// Resolves a submit intent to its outcome. Read-only: it never verifies an OTP, never creates
/// anything and never changes a challenge's state, so a visitor may poll it safely while deciding
/// whether their request went through.
/// </summary>
public sealed class GetVisitSubmissionResultQueryHandler
    : IRequestHandler<GetVisitSubmissionResultQuery, VisitSubmissionResultDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeService _clock;

    public GetVisitSubmissionResultQueryHandler(IApplicationDbContext db, IDateTimeService clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<VisitSubmissionResultDto> Handle(
        GetVisitSubmissionResultQuery request, CancellationToken cancellationToken)
    {
        var submissionId = request.SubmissionId?.Trim() ?? string.Empty;
        if (submissionId.Length == 0)
            return new VisitSubmissionResultDto(VisitSubmissionStates.NotFound, null, null, null, null, null);

        // A created request is the definitive answer and outranks whatever the pending row says:
        // the snapshot is consumed in the SAME transaction as the create, so a committed request
        // always coexists with a consumed pending row.
        var created = await _db.VisitRequests.AsNoTracking()
            .Where(v => v.SubmissionId == submissionId)
            .Select(v => new { v.VisitRequestId, v.RequestCode, v.Status, v.SubmittedAt })
            .FirstOrDefaultAsync(cancellationToken);

        if (created is not null)
        {
            var campusCount = await _db.VisitRequestCampuses.AsNoTracking()
                .CountAsync(c => c.VisitRequestId == created.VisitRequestId, cancellationToken);

            return new VisitSubmissionResultDto(
                VisitSubmissionStates.Completed,
                created.VisitRequestId,
                created.RequestCode ?? string.Empty,
                created.Status,
                created.SubmittedAt.ToString("yyyy-MM-ddTHH:mm:ss"),
                campusCount);
        }

        var pending = await _db.VisitRequestPendingForms.AsNoTracking()
            .Where(p => p.SubmissionId == submissionId)
            .Select(p => new { p.ConsumedAt, p.ExpiresAt })
            .FirstOrDefaultAsync(cancellationToken);

        if (pending is null)
            return new VisitSubmissionResultDto(VisitSubmissionStates.NotFound, null, null, null, null, null);

        // Consumed but no request: the duplicate guard consumed the snapshot and refused the create.
        // Re-verifying cannot succeed, so this must not be reported as "still pending".
        var state = pending.ConsumedAt is not null || pending.ExpiresAt <= _clock.VietnamNow
            ? VisitSubmissionStates.Failed
            : VisitSubmissionStates.Pending;

        return new VisitSubmissionResultDto(state, null, null, null, null, null);
    }
}
