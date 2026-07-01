using MediatR;
using System.Collections.Generic;

namespace PEMS.Application.MeetingMinutes.Queries.SearchAndFilterMinutes;

public class SearchAndFilterMinutesQuery : IRequest<SearchAndFilterMinutesDto>
{
    public string? Q { get; set; }
    public string? Status { get; set; }
    public string? AttendanceStatus { get; set; }
    public string? ActionItemStatus { get; set; }
    public string? DateType { get; set; }
    public string? FromDate { get; set; }
    public string? ToDate { get; set; }
    public string? LockState { get; set; }
    public string? HasAbsentParticipants { get; set; }
    public string? ParticipantType { get; set; }
    
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string? SortBy { get; set; }
    public string? SortDir { get; set; }
}

public class SearchAndFilterMinutesDto
{
    public List<MinutesListItemDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public MinutesSummaryDto Summary { get; set; } = new();
}

public class MinutesListItemDto
{
    public ulong MinutesId { get; set; }
    public ulong VisitInstanceId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? ContentPreview { get; set; }
    public string Status { get; set; } = string.Empty;
    public ulong? EditLockedBy { get; set; }
    public string? EditLockedByName { get; set; }
    public string? EditLockedAt { get; set; }
    public string? EditLockExpiresAt { get; set; }
    public string LockState { get; set; } = "UNLOCKED";
    public uint RowVersion { get; set; }
    public string CreatedAt { get; set; } = string.Empty;
    public string? CreatedByName { get; set; }
    public string? UpdatedAt { get; set; }
    public string? UpdatedByName { get; set; }
    public string? VisitTitle { get; set; }
    public ulong? VisitRequestId { get; set; }
    public string? CampusName { get; set; }
    public string? HostName { get; set; }
    public string? PlannedStartAt { get; set; }
    public string? PlannedEndAt { get; set; }
    public int ParticipantTotal { get; set; }
    public int ParticipantPresentCount { get; set; }
    public int ParticipantAbsentCount { get; set; }
    public int ParticipantExcusedCount { get; set; }
    public int ActionItemTotal { get; set; }
    public int ActionItemTodoCount { get; set; }
    public int ActionItemInProgressCount { get; set; }
    public int ActionItemDoneCount { get; set; }
    public int ActionItemCancelledCount { get; set; }
    public int ActionItemOverdueCount { get; set; }
}

public class MinutesSummaryDto
{
    public int TotalMinutes { get; set; }
    public int DraftCount { get; set; }
    public int SavedCount { get; set; }
    public int LockedCount { get; set; }
    public int OpenActionItemCount { get; set; }
    public string? LatestUpdatedAt { get; set; }
}