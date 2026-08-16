using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PEMS.Application.Common.DTOs;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Options;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Policies;

using PEMS.Application.Delegations.Common;
namespace PEMS.Application.Delegations.Commands.VisitAmendments;

/// <summary>Shared guards for the amendment handlers.</summary>
internal static class AmendmentGuards
{
    public static ulong EnsureAuthenticated(
        PerCampusFormV2WriteOptions writeFlag, ICurrentUserService currentUser)
    {
        if (!writeFlag.Enabled)
            throw new NotFoundException("Không tìm thấy.");
        if (!currentUser.IsAuthenticated || currentUser.UserId is null)
            throw new ForbiddenException();
        return currentUser.UserId.Value;
    }

    /// <summary>
    /// Requester side of ONE campus: the registrant of the request, or the confirmed operational
    /// contact of THAT campus.
    ///
    /// <para>
    /// It takes the campus rather than the request because an amendment always changes exactly one
    /// campus. Under the old request-level contact the check could not tell the difference, so the
    /// person who confirmed campus A could propose changes to campus B — a campus they were never
    /// invited to and whose Staff Leader has never heard of them.
    /// </para>
    /// </summary>
    public static void EnsureRequesterSide(VisitRequest visit, VisitRequestCampus instance, ulong actorId)
    {
        if (!VisitRequestOwnership.IsGuestSide(visit, instance, actorId))
            throw new ForbiddenException("Bạn không có quyền thao tác đề xuất của cơ sở này.");
    }

    /// <summary>
    /// Decision side: ONLY the campus's CURRENT Host.
    ///
    /// <para>
    /// Once a Staff Leader approves a campus they name its Host in the same action, and from that
    /// moment the Host owns it: they run the visit, they hold the schedule and the room, and they are
    /// the person the requester is already talking to. So they decide proposals about it. It used to be
    /// the Staff Leader, which meant every "could we start an hour later" had to go back to somebody who
    /// had handed the campus over days earlier and had no way to know whether the change was workable.
    /// </para>
    /// <para>
    /// "Current" is the whole point of the check. Authority travels with the role, so a handover moves
    /// it immediately: a proposal filed while A was Host is decided by B if B holds the campus when the
    /// decision is taken, and A — still a Staff member, possibly still the Staff Leader — is refused.
    /// </para>
    /// <para>
    /// A campus in this state with no Host at all is an invariant violation, not a case to fall back
    /// from: approve assigns a Host or it does not commit. Letting the Staff Leader decide "because
    /// nobody else can" would legitimise the corrupt row instead of surfacing it.
    /// </para>
    /// </summary>
    public static void EnsureCurrentHost(ulong? currentHostUserId, ulong actorId)
    {
        if (currentHostUserId != actorId)
            throw new ForbiddenException(
                "Chỉ Host đang phụ trách cơ sở này mới được quyết định đề xuất thay đổi.",
                VisitMutationErrorCodes.NotCurrentHost);
    }

    public static VisitAmendmentDto ToDto(
        VisitInstanceAmendment a, string? requestedByName, string? decidedByName)
        => new(
            a.AmendmentId, a.VisitRequestId, a.VisitInstanceId, a.AmendmentNo, a.Status,
            a.BaseFormRevision, a.BaseApprovalRevision,
            a.RequestedBy, requestedByName, a.RequestedAt, a.Reason,
            a.DecidedBy, decidedByName, a.DecidedAt, a.DecisionNote, a.ExpiresAt,
            a.Changes.OrderBy(c => c.DisplayOrder)
                .Select(c => new VisitAmendmentChangeDto(c.FieldPath, c.ChangeClass, c.OldValueJson, c.NewValueJson))
                .ToList());
}

/// <summary>Requester submits a per-campus amendment; nothing active mutates (plan §16.6).</summary>
public sealed class SubmitVisitAmendmentCommandHandler
    : IRequestHandler<SubmitVisitAmendmentCommand, VisitAmendmentDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;
    private readonly IVisitAmendmentService _amendments;
    private readonly PEMS.Application.Notifications.Common.INotificationService _notificationService;
    private readonly ILogger<SubmitVisitAmendmentCommandHandler> _logger;
    private readonly PerCampusFormV2WriteOptions _writeFlag;

    public SubmitVisitAmendmentCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock,
        IVisitAmendmentService amendments,
        PEMS.Application.Notifications.Common.INotificationService notificationService,
        ILogger<SubmitVisitAmendmentCommandHandler> logger,
        PerCampusFormV2WriteOptions writeFlag)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _amendments = amendments;
        _notificationService = notificationService;
        _logger = logger;
        _writeFlag = writeFlag;
    }

    public async Task<VisitAmendmentDto> Handle(
        SubmitVisitAmendmentCommand request, CancellationToken cancellationToken)
    {
        var actorId = AmendmentGuards.EnsureAuthenticated(_writeFlag, _currentUser);
        var now = _clock.VietnamNow;

        var visit = await _db.VisitRequests
            .Include(v => v.CampusInstances).ThenInclude(c => c.FormDetail)
            .Include(v => v.CampusInstances).ThenInclude(c => c.GuestMemberLinks)
            .Include(v => v.GuestMembers)
            .FirstOrDefaultAsync(v => v.VisitRequestId == request.VisitRequestId, cancellationToken)
            ?? throw new NotFoundException("Đơn đăng ký tham quan", request.VisitRequestId);
        var instance = visit.CampusInstances.FirstOrDefault(c => c.VisitInstanceId == request.VisitInstanceId)
            ?? throw new NotFoundException("Lịch thăm tại cơ sở", request.VisitInstanceId);
        AmendmentGuards.EnsureRequesterSide(visit, instance, actorId);

        // ── Self-approval (§13/§14). When the person proposing the change IS the campus's current Host,
        //    there is nobody to wait for: the requester side and the decision side are the same person,
        //    and making them file a proposal, reload, and approve their own proposal is ceremony that
        //    teaches users to click through a review that reviews nothing.
        //
        //    It skips the WAIT, never a RULE. The amendment row is still created with its change rows,
        //    still validated (cutoff, lifecycle, concurrency, base revision, schedule), and still
        //    decided — requested_by and decided_by both name the actor, and the audit says
        //    self-approved. So the campus's history reads the same whether or not the two roles happened
        //    to be one person.
        //
        //    The test is the RELATION, not the role. It used to be `RoleCode == STAFF`, which approved
        //    on behalf of any staff account that could reach the endpoint — including one that was
        //    merely the registrant of the request and had no authority over the campus at all. ──
        var selfApproves = VisitRequestOwnership.IsCurrentHost(instance, actorId);

        VisitInstanceAmendment amendment;
        await using (var tx = await _db.BeginTransactionAsync(cancellationToken))
        {
            amendment = await _amendments.SubmitAsync(
                visit, instance, request.Proposal, actorId, now, cancellationToken);

            if (selfApproves)
                await _amendments.ApproveAsync(
                    amendment, actorId, note: null, now, cancellationToken, selfApproval: true);

            await tx.CommitAsync(cancellationToken);
        }

        // Post-commit: tell the person who has to DECIDE — the campus's current Host. The campus Staff
        // Leader is no longer told: they are not the decision authority after approval, and an
        // action-required notification to someone with no action is noise that trains people to ignore
        // the channel. A self-approved amendment notifies nobody: "you have a proposal waiting for you"
        // is not something to send to the person who just approved it.
        if (!selfApproves)
        {
            try
            {
                if (instance.CurrentHostUserId is { } host)
                    await _notificationService.CreateAsync(
                        new PEMS.Application.Notifications.Common.CreateNotificationRequest(
                            RecipientUserId: host,
                            Title: "Có đề xuất thay đổi nội dung chuyến thăm",
                            Message: $"Đơn {visit.RequestCode}: khách đề xuất thay đổi nội dung tại cơ sở bạn phụ trách. Vui lòng xem và quyết định.",
                            NotificationType: PEMS.Application.Notifications.Common.NotificationTypes.VisitRequestSubmitted,
                            RelatedType: PEMS.Application.Notifications.Common.NotificationRelatedTypes.VisitInstance,
                            RelatedId: instance.VisitInstanceId,
                            ActorUserId: actorId,
                            Category: PEMS.Application.Notifications.Common.NotificationCategories.Visit,
                            IsActionRequired: true,
                            VisitRequestId: visit.VisitRequestId,
                            VisitInstanceId: instance.VisitInstanceId,
                            CampusId: instance.CampusId,
                            ActionType: PEMS.Application.Notifications.Common.NotificationActionTypes.OpenVisitDetail,
                            ActionUrl: $"/dashboard/visit?visitRequestId={visit.VisitRequestId}"),
                        cancellationToken);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "amendment submit notification failed for {AmendmentId}", amendment.AmendmentId);
            }
        }

        var requesterName = await _db.Users.AsNoTracking()
            .Where(u => u.UserId == actorId).Select(u => u.FullName).FirstOrDefaultAsync(cancellationToken);
        return AmendmentGuards.ToDto(amendment, requesterName, null);
    }
}

/// <summary>Scoped read of an instance's ACTIVE (pending) amendment.</summary>
public sealed class GetActiveVisitAmendmentQueryHandler
    : IRequestHandler<GetActiveVisitAmendmentQuery, VisitAmendmentDto?>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly PerCampusFormV2WriteOptions _writeFlag;

    public GetActiveVisitAmendmentQueryHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, PerCampusFormV2WriteOptions writeFlag)
    {
        _db = db;
        _currentUser = currentUser;
        _writeFlag = writeFlag;
    }

    public async Task<VisitAmendmentDto?> Handle(
        GetActiveVisitAmendmentQuery request, CancellationToken cancellationToken)
    {
        var actorId = AmendmentGuards.EnsureAuthenticated(_writeFlag, _currentUser);

        var row = await _db.VisitRequestCampuses.AsNoTracking()
            .Where(c => c.VisitInstanceId == request.VisitInstanceId
                        && c.VisitRequestId == request.VisitRequestId)
            .Select(c => new
            {
                c.CampusId,
                c.CurrentHostUserId,
                c.OperationalContactUserId,
                c.VisitRequest!.RegistrantUserId,
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Lịch thăm tại cơ sở", request.VisitInstanceId);

        // Scope: requester side OF THIS CAMPUS, the current Host, or HO (read-only). The contact is
        // read from the campus row — a sibling's contact is a different person with no business here.
        var allowed = row.RegistrantUserId == actorId
            || row.OperationalContactUserId == actorId
            || row.CurrentHostUserId == actorId
            || _currentUser.RoleCode == RoleCodes.Ho;
        if (!allowed)
            throw new ForbiddenException("Bạn không có quyền xem đề xuất của cơ sở này.");

        var amendment = await _db.VisitInstanceAmendments.AsNoTracking()
            .Include(a => a.Changes)
            .Where(a => a.VisitInstanceId == request.VisitInstanceId
                        && a.Status == AmendmentStatuses.PendingApproval)
            .FirstOrDefaultAsync(cancellationToken);
        if (amendment is null) return null;

        var names = await _db.Users.AsNoTracking()
            .Where(u => u.UserId == amendment.RequestedBy)
            .Select(u => u.FullName).FirstOrDefaultAsync(cancellationToken);
        return AmendmentGuards.ToDto(amendment, names, null);
    }
}

/// <summary>Approve / reject (current campus Staff Leader) and withdraw (requester side).</summary>
public sealed class DecideVisitAmendmentCommandHandlers :
    IRequestHandler<ApproveVisitAmendmentCommand, VisitAmendmentDecisionResponse>,
    IRequestHandler<RejectVisitAmendmentCommand, VisitAmendmentDecisionResponse>,
    IRequestHandler<WithdrawVisitAmendmentCommand, VisitAmendmentDecisionResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;
    private readonly IVisitAmendmentService _amendments;
    private readonly PEMS.Application.Notifications.Common.INotificationService _notificationService;
    private readonly ILogger<DecideVisitAmendmentCommandHandlers> _logger;
    private readonly PerCampusFormV2WriteOptions _writeFlag;

    public DecideVisitAmendmentCommandHandlers(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock,
        IVisitAmendmentService amendments,
        PEMS.Application.Notifications.Common.INotificationService notificationService,
        ILogger<DecideVisitAmendmentCommandHandlers> logger,
        PerCampusFormV2WriteOptions writeFlag)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _amendments = amendments;
        _notificationService = notificationService;
        _logger = logger;
        _writeFlag = writeFlag;
    }

    public async Task<VisitAmendmentDecisionResponse> Handle(
        ApproveVisitAmendmentCommand request, CancellationToken cancellationToken)
    {
        var actorId = AmendmentGuards.EnsureAuthenticated(_writeFlag, _currentUser);
        // Read the host INSIDE the request, then check it: "current" has to mean current at the moment
        // of the decision, so a handover that completed while this proposal sat in the queue moves the
        // authority before this call is answered, not after.
        AmendmentGuards.EnsureCurrentHost(
            await CurrentHostOfInstanceAsync(request.VisitInstanceId, cancellationToken), actorId);

        VisitAmendmentDecisionResponse result;
        await using (var tx = await _db.BeginTransactionAsync(cancellationToken))
        {
            var amendment = await LockOwnAsync(request.AmendmentId, request.VisitInstanceId, cancellationToken);
            result = await _amendments.ApproveAsync(amendment, actorId, request.Note, _clock.VietnamNow, cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }
        await NotifyDecisionAsync(request.VisitInstanceId, result, "được DUYỆT và áp dụng", approved: true, actorId, cancellationToken);
        return result;
    }

    public async Task<VisitAmendmentDecisionResponse> Handle(
        RejectVisitAmendmentCommand request, CancellationToken cancellationToken)
    {
        var actorId = AmendmentGuards.EnsureAuthenticated(_writeFlag, _currentUser);
        // Reject is the other outcome of the same decision, so it answers to the same authority.
        AmendmentGuards.EnsureCurrentHost(
            await CurrentHostOfInstanceAsync(request.VisitInstanceId, cancellationToken), actorId);

        VisitAmendmentDecisionResponse result;
        await using (var tx = await _db.BeginTransactionAsync(cancellationToken))
        {
            var amendment = await LockOwnAsync(request.AmendmentId, request.VisitInstanceId, cancellationToken);
            result = await _amendments.RejectAsync(amendment, actorId, request.Note, _clock.VietnamNow, cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }
        await NotifyDecisionAsync(request.VisitInstanceId, result, "bị TỪ CHỐI (nội dung hiện tại giữ nguyên)", approved: false, actorId, cancellationToken);
        return result;
    }

    public async Task<VisitAmendmentDecisionResponse> Handle(
        WithdrawVisitAmendmentCommand request, CancellationToken cancellationToken)
    {
        var actorId = AmendmentGuards.EnsureAuthenticated(_writeFlag, _currentUser);
        // Withdrawing names the campus whose proposal it closes, so the check is that campus's:
        // the registrant, or the person who holds it.
        var head = await _db.VisitRequestCampuses.AsNoTracking()
            .Where(c => c.VisitInstanceId == request.VisitInstanceId
                        && c.VisitRequestId == request.VisitRequestId)
            .Select(c => new { c.OperationalContactUserId, c.VisitRequest!.RegistrantUserId })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Lịch thăm tại cơ sở", request.VisitInstanceId);
        if (head.RegistrantUserId != actorId && head.OperationalContactUserId != actorId)
            throw new ForbiddenException("Bạn không có quyền thao tác đề xuất của cơ sở này.");

        VisitAmendmentDecisionResponse result;
        await using (var tx = await _db.BeginTransactionAsync(cancellationToken))
        {
            var amendment = await LockOwnAsync(request.AmendmentId, request.VisitInstanceId, cancellationToken);
            if (amendment.VisitRequestId != request.VisitRequestId)
                throw new NotFoundException("Đề xuất thay đổi", request.AmendmentId);
            result = await _amendments.WithdrawAsync(amendment, actorId, _clock.VietnamNow, cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }
        return result;
    }

    /// <summary>
    /// The campus's current Host, or null when it has none. A missing campus 404s; a campus with no
    /// host returns null and the caller refuses — that state is only reachable before approval, and a
    /// campus with no decision has no amendment to decide either.
    /// </summary>
    private async Task<ulong?> CurrentHostOfInstanceAsync(ulong visitInstanceId, CancellationToken ct)
    {
        var row = await _db.VisitRequestCampuses.AsNoTracking()
            .Where(c => c.VisitInstanceId == visitInstanceId)
            .Select(c => new { c.CurrentHostUserId })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Lịch thăm tại cơ sở", visitInstanceId);
        return row.CurrentHostUserId;
    }

    private async Task<VisitInstanceAmendment> LockOwnAsync(
        ulong amendmentId, ulong visitInstanceId, CancellationToken ct)
    {
        var amendment = await _amendments.LockAmendmentAsync(amendmentId, ct)
            ?? throw new NotFoundException("Đề xuất thay đổi", amendmentId);
        if (amendment.VisitInstanceId != visitInstanceId)
            throw new NotFoundException("Đề xuất thay đổi", amendmentId);
        return amendment;
    }

    private async Task NotifyDecisionAsync(
        ulong visitInstanceId, VisitAmendmentDecisionResponse result, string outcome, bool approved,
        ulong actorId, CancellationToken ct)
    {
        try
        {
            var row = await _db.VisitInstanceAmendments.AsNoTracking()
                .Where(a => a.AmendmentId == result.AmendmentId)
                .Select(a => new { a.RequestedBy, a.VisitRequestId })
                .FirstAsync(ct);
            var host = await _db.VisitRequestCampuses.AsNoTracking()
                .Where(c => c.VisitInstanceId == visitInstanceId)
                .Select(c => c.CurrentHostUserId)
                .FirstOrDefaultAsync(ct);
            var recipients = new System.Collections.Generic.HashSet<ulong> { row.RequestedBy };
            if (host is { } h) recipients.Add(h);
            recipients.Remove(actorId);
            if (recipients.Count == 0) return;
            var notifications = recipients.Select(id =>
                new PEMS.Application.Notifications.Common.CreateNotificationRequest(
                    RecipientUserId: id,
                    Title: "Đề xuất thay đổi đã được xử lý",
                    Message: $"Đề xuất thay đổi của cơ sở đã {outcome}.",
                    NotificationType: PEMS.Application.Notifications.Common.NotificationTypes.VisitRequestSubmitted,
                    RelatedType: PEMS.Application.Notifications.Common.NotificationRelatedTypes.VisitInstance,
                    RelatedId: visitInstanceId,
                    ActorUserId: actorId,
                    Category: PEMS.Application.Notifications.Common.NotificationCategories.Visit,
                    IsActionRequired: false,
                    VisitRequestId: row.VisitRequestId,
                    VisitInstanceId: visitInstanceId,
                    ActionType: PEMS.Application.Notifications.Common.NotificationActionTypes.OpenVisitDetail,
                    ActionUrl: $"/dashboard/visit?visitRequestId={row.VisitRequestId}",
                    MetadataJson: PEMS.Application.Notifications.Common.NotificationMessageKeys.BuildMetadata(
                        approved
                            ? PEMS.Application.Notifications.Common.NotificationMessageKeys.AmendmentApproved
                            : PEMS.Application.Notifications.Common.NotificationMessageKeys.AmendmentRejected,
                        new { }))).ToList();
            await _notificationService.CreateManyAsync(notifications, ct);
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "amendment decision notification failed for {AmendmentId}", result.AmendmentId);
        }
    }
}
