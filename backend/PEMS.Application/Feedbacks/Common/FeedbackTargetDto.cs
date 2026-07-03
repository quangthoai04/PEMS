using System.Collections.Generic;

namespace PEMS.Application.Feedbacks.Common;

/// <summary>
/// One rateable business target shown as a compact row on the feedback screen.
/// The id columns mirror the `feedbacks` target columns 1:1 so the client echoes
/// them back unchanged on submit (no fake/aggregate targets are ever emitted).
/// </summary>
public sealed class FeedbackTargetDto
{
    /// <summary>Stable client key, e.g. "PARTICIPANT:12" — for React list keys only.</summary>
    public string TargetKey { get; set; } = default!;

    public string FeedbackType { get; set; } = default!;   // VISITOR_OVERALL | HOST_PARTICIPANT | HOST_LOGISTICS
    public string TargetType { get; set; } = default!;     // VISIT_INSTANCE | VISIT_PARTICIPANT | GUEST_MEMBER | USER | LOGISTICS_ITEM | ...

    public ulong? TargetUserId { get; set; }
    public ulong? TargetParticipantId { get; set; }
    public ulong? TargetGuestMemberId { get; set; }
    public ulong? TargetLogisticsItemId { get; set; }
    public ulong? TargetHandoverId { get; set; }
    public ulong? TargetDepartmentId { get; set; }

    public string Name { get; set; } = default!;           // display + name snapshot base
    public string? Subtitle { get; set; }                  // compact secondary info (role, dept, qty...)
    public string? TargetRole { get; set; }                // snapshot role/group label
    public string TargetContext { get; set; } = string.Empty;

    // Existing feedback of the current user on this target (renders "Đã đánh giá").
    public bool AlreadySubmitted { get; set; }
    public byte? ExistingRating { get; set; }
    public string? ExistingComment { get; set; }
}

/// <summary>A titled group of targets ("Người tạo đoàn khách", "Setup"...).</summary>
public sealed class FeedbackGroupDto
{
    public string GroupCode { get; set; } = default!;
    public string Title { get; set; } = default!;
    /// <summary>Info-only note when the group has data but no valid DB target to rate.</summary>
    public string? InfoNote { get; set; }
    public List<FeedbackTargetDto> Targets { get; set; } = new();
}
