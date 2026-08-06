using PEMS.Application.Common.Exceptions;
using PEMS.Domain.Constants;

namespace PEMS.Application.Delegations.Common;

/// <summary>
/// One place that answers "is this campus's preparation open?" for every setup mutation.
///
/// <para>
/// Approving a campus lands it on <c>ASSIGNED</c>: it has a Host and a decision, and nothing about
/// the visit may be set up yet. Only when the current Host explicitly starts preparation does the
/// campus move to <c>BEFORE_VISIT</c> and the setup actions open. Both refusals are 409, but they are
/// NOT the same refusal, and the caller has to be able to tell them apart:
/// </para>
/// <list type="bullet">
///   <item><c>VISIT_PREPARATION_NOT_STARTED</c> — the Host simply has not pressed "Bắt đầu chuẩn bị"
///   yet. Nothing is wrong; the fix is one click, and the UI should offer it.</item>
///   <item>a plain conflict — the campus is past preparation (or was rejected/cancelled), and no
///   action of the caller's will reopen it.</item>
/// </list>
/// <para>
/// Collapsing the two into one message is how a user ends up staring at "you cannot edit the agenda"
/// on a campus that is one button away from being editable.
/// </para>
/// </summary>
public static class VisitPreparationGate
{
    /// <summary>
    /// Refuses unless the campus is in its preparation window.
    /// <paramref name="actionPhrase"/> completes "Chỉ có thể {phrase} trong giai đoạn chuẩn bị." —
    /// e.g. <c>"chỉnh sửa lịch trình"</c>.
    /// </summary>
    public static void EnsurePreparationOpen(string instanceStatus, string actionPhrase)
    {
        if (instanceStatus == VisitInstanceStatuses.BeforeVisit)
            return;

        if (instanceStatus == VisitInstanceStatuses.Assigned)
            throw new ConflictException(
                $"Host chưa bắt đầu giai đoạn chuẩn bị cho cơ sở này. Hãy bấm \"Bắt đầu chuẩn bị\" trước khi {actionPhrase}.",
                VisitRequestErrorCodes.VisitPreparationNotStarted);

        throw new ConflictException($"Chỉ có thể {actionPhrase} trong giai đoạn chuẩn bị.");
    }
}
