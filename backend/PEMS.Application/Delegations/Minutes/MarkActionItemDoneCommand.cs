using MediatR;

namespace PEMS.Application.Delegations.Minutes;

/// <summary>
/// "Tích hoàn thành" on the caller's own "Quản lý việc sau tiếp khách" list — sets a
/// minute_action_item straight to DONE. One-way (no un-checking): the assignee marks their own item
/// done; only the Host can change it back via the minutes editor. Since both surfaces read/write the
/// same minute_action_items row, this is also the sync between the two views — no extra plumbing.
/// </summary>
public sealed record MarkActionItemDoneCommand(ulong ActionItemId) : IRequest<bool>;
