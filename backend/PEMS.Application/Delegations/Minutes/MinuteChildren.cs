using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;

namespace PEMS.Application.Delegations.Minutes;

/// <summary>
/// Loads the child collections of a minutes record (participants + action items) and maps them to
/// DTOs. Entities are materialised then mapped in-memory (the derived <c>ParticipantKind</c> is
/// computed here, never queried) to keep the SQL simple for the Pomelo MySQL provider.
/// </summary>
internal static class MinuteChildren
{
    public static async Task LoadInto(
        IApplicationDbContext db, MinuteDto dto, ulong minutesId, CancellationToken ct)
    {
        var participants = await db.MinuteParticipants
            .Where(p => p.MinutesId == minutesId)
            .OrderBy(p => p.DisplayOrder).ThenBy(p => p.MinuteParticipantId)
            .ToListAsync(ct);

        dto.Participants = participants.Select(p => new MinuteParticipantDto
        {
            MinuteParticipantId = p.MinuteParticipantId,
            MinutesId = p.MinutesId,
            UserId = p.UserId,
            GuestMemberId = p.GuestMemberId,
            FullNameSnapshot = p.FullNameSnapshot,
            RoleSnapshot = p.RoleSnapshot,
            OrganizationSnapshot = p.OrganizationSnapshot,
            EmailSnapshot = p.EmailSnapshot,
            AttendanceStatus = p.AttendanceStatus,
            AttendanceNote = p.AttendanceNote,
            CheckedAt = p.CheckedAt,
            CheckedBy = p.CheckedBy,
            DisplayOrder = p.DisplayOrder,
            ParticipantKind = MinuteParticipantDto.KindOf(p.UserId, p.GuestMemberId),
        }).ToList();

        var actionItems = await db.MinuteActionItems
            .Where(a => a.MinutesId == minutesId)
            .OrderBy(a => a.DisplayOrder).ThenBy(a => a.ActionItemId)
            .ToListAsync(ct);

        dto.ActionItems = actionItems.Select(a => new MinuteActionItemDto
        {
            ActionItemId = a.ActionItemId,
            MinutesId = a.MinutesId,
            Title = a.Title,
            Note = a.Note,
            DueDate = a.DueDate,
            Status = a.Status,
            CompletedAt = a.CompletedAt,
            DisplayOrder = a.DisplayOrder,
        }).ToList();
    }
}
