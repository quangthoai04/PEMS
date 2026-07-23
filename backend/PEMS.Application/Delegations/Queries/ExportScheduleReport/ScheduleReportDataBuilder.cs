using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.Services.VisitFormRead;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.Delegations.Queries.ExportScheduleReport;

/// <summary>
/// Builds the <see cref="ScheduleReportDto"/> for one campus instance. Pure data mapping (no PDF
/// drawing, no file-storage I/O for the logo bytes) so it can be unit tested against an InMemory
/// DbContext. Caller is responsible for the permission check and for loading the actual logo image
/// bytes from <see cref="ScheduleReportDto.PartnerLogoFileId"/>.
/// </summary>
public static class ScheduleReportDataBuilder
{
    private const string ExternalSupport = "EXTERNAL_SUPPORT";
    private const string DefaultLocation = "FPT University";

    public static async Task<ScheduleReportDto> BuildAsync(
        IApplicationDbContext db,
        IVisitFormReadService formReadService,
        VisitRequestCampus instance,
        CancellationToken cancellationToken)
    {
        var visit = instance.VisitRequest;

        // ── Delegation name / purpose / guest composition — always this campus's own detail ──
        // The report is per campus instance, so the content must be the one that campus owns. A request
        // row carries no form content at all now, and no sibling campus may stand in for this one.
        var content = await formReadService.ResolveCampusFormContentAsync(
            visit, new[] { instance.VisitInstanceId }, cancellationToken);
        var detail = content[instance.VisitInstanceId];

        var delegationName = detail.DelegationName;
        var purpose = detail.Purpose;

        var guestSide = new List<ScheduleReportPersonDto>();
        guestSide.AddRange(detail.Visitors.OrderBy(v => v.DisplayOrder)
            .Select(v => MapGuestPerson(v.FullName, v.Organization, "Khách mời")));
        guestSide.AddRange(detail.SupportMembers.OrderBy(v => v.DisplayOrder)
            .Select(v => MapGuestPerson(v.FullName, v.Organization, "Nhân sự hỗ trợ")));

        // ── FPT side: Host + accepted (non-host) participants — same rule as MinuteAutoFill ──
        var fptSide = new List<ScheduleReportPersonDto>();
        if (instance.CurrentHostUserId is ulong hostId)
        {
            var host = await db.Users.Include(u => u.Department)
                .FirstOrDefaultAsync(u => u.UserId == hostId, cancellationToken);
            if (host != null)
                fptSide.Add(MapFptPerson(host, "Host"));
        }

        var acceptedParticipants = await db.VisitParticipants
            .Where(p => p.VisitInstanceId == instance.VisitInstanceId && !p.IsHost
                && p.Status == ParticipantStatuses.Accepted)
            .OrderBy(p => p.ParticipantId)
            .ToListAsync(cancellationToken);

        if (acceptedParticipants.Count > 0)
        {
            var userIds = acceptedParticipants.Select(p => p.UserId).Distinct().ToList();
            var users = await db.Users.Include(u => u.Department)
                .Where(u => userIds.Contains(u.UserId)).ToListAsync(cancellationToken);
            var userById = users.ToDictionary(u => u.UserId);

            foreach (var p in acceptedParticipants)
            {
                if (p.UserId == instance.CurrentHostUserId) continue; // never double-list the host
                if (!userById.TryGetValue(p.UserId, out var u)) continue;
                fptSide.Add(MapFptPerson(u, RoleLabel(p.ParticipantRole)));
            }
        }

        // ── Agenda — exactly what the Host set up, "Party in Charge" is always FPT University ──
        var agenda = instance.Agendas
            .OrderBy(a => a.SequenceOrder).ThenBy(a => a.StartTime)
            .Select(a => new ScheduleReportAgendaRowDto
            {
                StartTime = a.StartTime,
                EndTime = a.EndTime,
                Title = a.Title,
                Description = a.Description,
                Venue = string.IsNullOrWhiteSpace(a.Location) ? DefaultLocation : a.Location!,
            })
            .ToList();

        return new ScheduleReportDto
        {
            DelegationName = delegationName,
            PlannedStartAt = instance.PlannedStartAt,
            PlannedEndAt = instance.PlannedEndAt,
            Location = DefaultLocation,
            Purpose = purpose,
            GuestSide = guestSide,
            FptSide = fptSide,
            Agenda = agenda,
            PartnerLogoFileId = visit.Partner?.LogoFileId,
            PartnerName = visit.Partner?.Name,
        };
    }

    private static ScheduleReportPersonDto MapGuestPerson(string fullName, string? organization, string roleLabel)
        => new() { FullName = fullName, Organization = organization, RoleLabel = roleLabel };

    private static ScheduleReportPersonDto MapFptPerson(User u, string roleLabel) => new()
    {
        FullName = u.FullName,
        Organization = u.Department?.Name ?? DefaultLocation,
        RoleLabel = roleLabel,
    };

    private static string RoleLabel(string participantRole) => participantRole switch
    {
        ParticipantRoles.IcHost => "IC Host",
        ParticipantRoles.IcSupport => "Cán bộ IC",
        ParticipantRoles.DeptSupport => "Cán bộ phòng ban",
        ParticipantRoles.Student => "Sinh viên hỗ trợ",
        _ => participantRole,
    };
}
