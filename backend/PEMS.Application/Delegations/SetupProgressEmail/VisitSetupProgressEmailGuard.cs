using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Shared;

namespace PEMS.Application.Delegations.SetupProgressEmail;

/// <summary>
/// The one rule every setup-progress endpoint applies: this instance belongs to this request, the
/// caller IS its current host, and the visit is still in the window where the host may change what the
/// mail describes.
///
/// <para>
/// It is applied on EVERY call, not just when the draft is created. Between composing a draft and
/// pressing send a host handover can happen, the campus can be cancelled, or the visit can start — and a
/// permission flag the browser read minutes ago knows about none of them. Re-deriving it here is what
/// makes "old host cannot send" true rather than merely displayed.
/// </para>
/// </summary>
public static class VisitSetupProgressEmailGuard
{
    /// <summary>Template code of the mail this whole flow is about. Nothing else may be sent through it.</summary>
    public const string TemplateCode = PEMS.Application.Emails.Common.SystemEmailTemplates.VisitSetupProgressUpdate;

    /// <summary>
    /// Loads the campus instance (with its request, partner and agendas, as the report render needs) and
    /// refuses unless the caller is its current host inside the preparation window.
    /// </summary>
    /// <exception cref="NotFoundException">
    /// The pair (request, instance) does not identify a row. A mismatched pair is reported the same way
    /// as a missing one on purpose: telling a caller "that instance exists, just not under that request"
    /// answers a question they were not entitled to ask.
    /// </exception>
    /// <exception cref="ForbiddenException">The caller is not the current host.</exception>
    /// <exception cref="ConflictException">The visit has left the preparation window.</exception>
    public static async Task<VisitRequestCampus> ResolveHostInstanceAsync(
        IApplicationDbContext db,
        ulong visitRequestId,
        ulong visitInstanceId,
        ulong currentUserId,
        CancellationToken cancellationToken)
    {
        var instance = await db.VisitRequestCampuses
            .Include(c => c.VisitRequest).ThenInclude(v => v.Partner)
            .Include(c => c.Agendas)
            .FirstOrDefaultAsync(c => c.VisitInstanceId == visitInstanceId
                                      && c.VisitRequestId == visitRequestId, cancellationToken)
            ?? throw new NotFoundException("VisitRequestCampus", visitInstanceId);

        if (instance.CurrentHostUserId != currentUserId)
            throw new ForbiddenException(
                "Chỉ người phụ trách tiếp đón hiện tại của chuyến thăm này mới được gửi cập nhật chuẩn bị.");

        var visit = instance.VisitRequest;
        bool isCancelled = visit.Status == VisitRequestStatuses.Cancelled
            || instance.Status == VisitInstanceStatus.Cancelled;
        if (isCancelled)
            throw new ConflictException("Chuyến thăm đã bị hủy — không thể gửi cập nhật chuẩn bị.");

        if (instance.Status == VisitInstanceStatus.Rejected)
            throw new ConflictException("Chuyến thăm đã bị từ chối — không thể gửi cập nhật chuẩn bị.");

        // Same window as CanEditBeforeVisit: once the visit is under way the "chuẩn bị" the mail reports
        // on is no longer something the host can revise, so sending an update on it would misdescribe it.
        bool prepWindowOpen = instance.Status == VisitInstanceStatus.Assigned
            || instance.Status == VisitInstanceStatus.BeforeVisit;
        if (!prepWindowOpen)
            throw new ConflictException(
                "Chuyến thăm đã qua giai đoạn chuẩn bị — không thể gửi cập nhật chuẩn bị.");

        return instance;
    }

    /// <summary>
    /// The template variables for one instance. Times are rendered as Vietnam wall-clock text because
    /// that is what the database stores and what the guest reads; nothing here converts to UTC.
    /// </summary>
    /// <remarks>
    /// The Host's ADDRESS is deliberately not here. It arrives through
    /// <c>{{contactInformationBlock}}</c>, which resolves it from the visit instance together with the
    /// role and telephone number — so it stays correct when the Host changes, and a guest is never shown
    /// a campus's Host other than their own.
    /// </remarks>
    public static System.Collections.Generic.Dictionary<string, string> BuildVariables(
        VisitRequestCampus instance, string delegationName, string campusName, string hostName)
        => new(StringComparer.Ordinal)
        {
            ["delegationName"] = delegationName,
            ["campusName"] = campusName,
            ["plannedStart"] = FormatMoment(instance.PlannedStartAt),
            ["plannedEnd"] = FormatMoment(instance.PlannedEndAt),
            ["hostName"] = hostName,
        };

    private static string FormatMoment(DateTime? value)
        => value is { } d ? d.ToString("HH:mm dd/MM/yyyy") : "—";
}
