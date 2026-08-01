using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Common;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Entities.Users;
using PEMS.Shared;

namespace PEMS.Application.Delegations.Commands.SendVisitAgendaEmail;

public sealed class SendVisitAgendaEmailCommandHandler
    : IRequestHandler<SendVisitAgendaEmailCommand, SendVisitAgendaEmailResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;
    private readonly ISystemEmailDispatcher _dispatcher;

    public SendVisitAgendaEmailCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock, ISystemEmailDispatcher dispatcher)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _dispatcher = dispatcher;
    }

    public async Task<SendVisitAgendaEmailResponse> Handle(
        SendVisitAgendaEmailCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new ForbiddenException();

        var actorId = _currentUser.UserId.Value;

        var instance = await _db.VisitRequestCampuses
            .FirstOrDefaultAsync(c => c.VisitInstanceId == request.VisitInstanceId
                                      && c.VisitRequestId == request.VisitRequestId, cancellationToken)
            ?? throw new NotFoundException("VisitRequestCampus", request.VisitInstanceId);

        // Same guards as SaveVisitAgendaCommandHandler — sending is part of the same Host/prep-window job.
        if (instance.CurrentHostUserId != actorId)
            throw new ForbiddenException("Chỉ Host phụ trách cơ sở này mới được gửi lịch trình.");
        if (instance.Status == VisitInstanceStatus.Cancelled)
            throw new ConflictException("Cơ sở này đã bị hủy nên không thể gửi lịch trình.");
        if (instance.Status == VisitInstanceStatus.Closed)
            throw new ConflictException("Cơ sở này đã đóng đoàn nên không thể gửi lịch trình.");
        if (instance.Status != VisitInstanceStatus.Assigned && instance.Status != VisitInstanceStatus.BeforeVisit)
            throw new ConflictException("Chỉ có thể gửi lịch trình trong giai đoạn chuẩn bị (trước tiếp khách).");

        var agenda = await _db.VisitAgendas
            .Where(a => a.VisitInstanceId == instance.VisitInstanceId)
            .OrderBy(a => a.SequenceOrder).ThenBy(a => a.StartTime)
            .ToListAsync(cancellationToken);
        if (agenda.Count == 0)
            throw new ConflictException("Chưa có lịch trình để gửi.");

        var formDetail = await _db.VisitInstanceFormDetails
            .Where(f => f.VisitInstanceId == instance.VisitInstanceId)
            .Select(f => new { f.OperationalContactFullName, f.OperationalContactEmail })
            .FirstOrDefaultAsync(cancellationToken);
        var contactEmail = formDetail?.OperationalContactEmail;
        if (string.IsNullOrWhiteSpace(contactEmail))
            throw new ConflictException("Cơ sở này chưa có email đầu mối liên hệ để gửi lịch trình.");

        var host = await _db.Users
            .Where(u => u.UserId == actorId)
            .Select(u => new { u.FullName, u.Email })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("User", actorId);

        var campusName = await _db.Campuses
            .Where(c => c.CampusId == instance.CampusId)
            .Select(c => c.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? "FPT University";

        var delegationName = (await Services.VisitFormRead.VisitInstanceEffectiveName
            .ForInstancesAsync(_db, new[] { instance.VisitInstanceId }, cancellationToken))
            .GetValueOrDefault(instance.VisitInstanceId) ?? "Đoàn khách";

        var agendaHtml = EmailComposition.AgendaListBlock(
            agenda.Select(a => (
                Time: FormatAgendaTime(a.StartTime, a.EndTime),
                Title: a.Title,
                Location: a.Location,
                Responsible: a.ResponsibleName))
            .ToList());

        var hostRecipient = new EmailRecipient(host.Email, host.FullName);
        // The Host and the operational contact can be the same person (e.g. a Staff who self-hosts their
        // own registration) — CC-ing them on a message they're already the TO of is both redundant and a
        // hard validation error (EmailRecipientValidator refuses the same address in two envelope groups).
        var isHostAlsoTheContact = string.Equals(host.Email, contactEmail, StringComparison.OrdinalIgnoreCase);
        var cc = isHostAlsoTheContact ? Array.Empty<EmailRecipient>() : new[] { hostRecipient };

        var result = await _dispatcher.SendAsync(
            new SystemEmailRequest(
                SystemEmailTemplates.VisitAgendaProposal,
                new EmailRecipient(contactEmail!, formDetail?.OperationalContactFullName),
                new Dictionary<string, string>
                {
                    ["contactFullName"] = formDetail?.OperationalContactFullName ?? "",
                    ["delegationName"] = delegationName,
                    ["campusName"] = campusName,
                    ["plannedTime"] = FormatWindow(instance.PlannedStartAt, instance.PlannedEndAt),
                    ["hostName"] = host.FullName,
                    ["hostEmail"] = host.Email,
                },
                TrustedBlocks: new Dictionary<string, string>
                {
                    [EmailTrustedBlocks.AgendaBlock] = agendaHtml,
                },
                RelatedType: "VISIT_INSTANCE",
                RelatedId: instance.VisitInstanceId,
                SentBy: actorId,
                Cc: cc,
                ReplyTo: hostRecipient),
            cancellationToken);

        var now = _clock.VietnamNow;
        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = actorId,
            Action = "SEND_VISIT_AGENDA_EMAIL",
            EntityType = "VisitRequestCampus",
            EntityId = instance.VisitInstanceId,
            CampusId = instance.CampusId,
            VisitRequestId = instance.VisitRequestId,
            VisitInstanceId = instance.VisitInstanceId,
            Reason = $"status={result.NotificationStatus}; items={agenda.Count}",
            CreatedAt = now,
        });
        await _db.SaveChangesAsync(cancellationToken);

        var message = result.NotificationStatus switch
        {
            "SENT" => "Đã gửi lịch trình.",
            "SKIPPED" => "Đã ghi nhận gửi lịch trình; email chưa được gửi (SMTP đang tắt).",
            _ => "Gửi lịch trình thất bại.",
        };

        return new SendVisitAgendaEmailResponse(result.NotificationStatus, now, message);
    }

    private static string FormatWindow(DateTime start, DateTime end)
        => $"{start:HH:mm dd/MM/yyyy} - {end:HH:mm dd/MM/yyyy}";

    private static string FormatAgendaTime(DateTime start, DateTime? end)
        => end.HasValue ? $"{start:HH:mm} - {end:HH:mm}" : $"{start:HH:mm}";
}
