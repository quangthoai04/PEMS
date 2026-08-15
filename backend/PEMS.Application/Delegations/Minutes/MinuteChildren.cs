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

        var guestMemberIds = participants
            .Where(p => p.GuestMemberId.HasValue)
            .Select(p => p.GuestMemberId!.Value)
            .Distinct()
            .ToList();
        var nationalityByGuestId = guestMemberIds.Count == 0
            ? new Dictionary<ulong, string>()
            : await db.VisitGuestMembers
                .Where(g => guestMemberIds.Contains(g.GuestMemberId))
                .Select(g => new { g.GuestMemberId, g.Nationality })
                .ToDictionaryAsync(g => g.GuestMemberId, g => g.Nationality, ct);

        // Who the campus's đầu mối is NOW. The stored flag is a fact about the moment a row was
        // written, and the role can move afterwards — so it is re-derived here as well as on save.
        // Without this, a biên bản nobody has re-saved since the handover goes on showing the badge
        // on the previous contact, which is the same wrong answer the save already learnt to avoid.
        // Derived into the DTO only: these entities are tracked, and quietly mutating them on a READ
        // would let an unrelated SaveChanges elsewhere persist a decision this method never made.
        var contactGuestMemberId = await MinuteContactBadge.CurrentContactMemberIdAsync(db, minutesId, ct);

        // EXCLUDED rows are returned too, so the editor can offer "khôi phục vào biên bản" — the
        // decision is only reversible if the client can see it was made. They are marked, not hidden:
        // every reader (the editor, the export, the attendance count) decides for itself, and the one
        // thing none of them can do is silently treat a set-aside person as present (MIN-03).
        dto.Participants = participants.Select(p =>
        {
            // Derived ONCE and used for both the badge and the kind: they answer the same question,
            // and reading one from the stored column while deriving the other would let the table say
            // "Nội bộ" beside "Đầu mối" — a pair that cannot both be true.
            var isContact = MinuteContactBadge.Resolve(
                p.GuestMemberId, p.IsOperationalContact, contactGuestMemberId);
            return new MinuteParticipantDto
            {
                MinuteParticipantId = p.MinuteParticipantId,
                MinutesId = p.MinutesId,
                UserId = p.UserId,
                GuestMemberId = p.GuestMemberId,
                SourceMemberType = p.SourceMemberType,
                IsOperationalContact = isContact,
                IsManual = p.UserId == null && p.GuestMemberId == null,
                SyncState = p.SyncState,
                FullNameSnapshot = p.FullNameSnapshot,
                RoleSnapshot = p.RoleSnapshot,
                OrganizationSnapshot = p.OrganizationSnapshot,
                EmailSnapshot = p.EmailSnapshot,
                AttendanceStatus = p.AttendanceStatus,
                AttendanceNote = p.AttendanceNote,
                CheckedAt = p.CheckedAt,
                CheckedBy = p.CheckedBy,
                DisplayOrder = p.DisplayOrder,
                ParticipantKind = MinuteParticipantDto.KindOf(
                    p.UserId, p.GuestMemberId, p.SourceMemberType, isContact),
                GuestNationality = p.GuestMemberId.HasValue
                    ? nationalityByGuestId.GetValueOrDefault(p.GuestMemberId.Value)
                    : null,
            };
        }).ToList();

        var actionItems = await db.MinuteActionItems
            .Where(a => a.MinutesId == minutesId)
            .OrderBy(a => a.DisplayOrder).ThenBy(a => a.ActionItemId)
            .ToListAsync(ct);

        var assigneeIds = actionItems
            .Where(a => a.AssignedToUserId.HasValue)
            .Select(a => a.AssignedToUserId!.Value)
            .Distinct()
            .ToList();
        var assigneeNameById = assigneeIds.Count == 0
            ? new Dictionary<ulong, string>()
            : await db.Users
                .Where(u => assigneeIds.Contains(u.UserId))
                .Select(u => new { u.UserId, u.FullName })
                .ToDictionaryAsync(u => u.UserId, u => u.FullName, ct);

        dto.ActionItems = actionItems.Select(a => new MinuteActionItemDto
        {
            ActionItemId = a.ActionItemId,
            MinutesId = a.MinutesId,
            Title = a.Title,
            Note = a.Note,
            AssignedToUserId = a.AssignedToUserId,
            AssignedToUserName = a.AssignedToUserId.HasValue
                ? assigneeNameById.GetValueOrDefault(a.AssignedToUserId.Value)
                : null,
            DueDate = a.DueDate,
            Status = a.Status,
            CompletedAt = a.CompletedAt,
            DisplayOrder = a.DisplayOrder,
        }).ToList();
    }
}
