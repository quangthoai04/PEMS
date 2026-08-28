using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;

namespace PEMS.Infrastructure.Persistence;

/// <summary>
/// MySQL implementation of <see cref="IUserMutationLockService"/> using <c>SELECT … FOR UPDATE</c>.
/// The lock lives for the duration of the ambient EF transaction, which is exactly the window the
/// role-change protocol needs (spec §13.3).
///
/// Ids are sorted ascending before locking so any two callers acquire overlapping rows in the same
/// order — the standard deadlock-avoidance discipline required by spec §13.4.
///
/// Ids are formatted into the statement rather than parameterised. They are <see cref="ulong"/>
/// values that have already been through model binding, so no attacker-controlled text can reach
/// the SQL text; the alternative (a parameter per id) would defeat statement caching for what is a
/// hot path on every assignment flow.
/// </summary>
public sealed class MySqlUserMutationLockService : IUserMutationLockService
{
    private readonly ApplicationDbContext _db;

    public MySqlUserMutationLockService(ApplicationDbContext db) => _db = db;

    public Task LockUsersAsync(IReadOnlyCollection<ulong> userIds, CancellationToken cancellationToken)
        => LockAsync(LockTarget.Users, userIds, cancellationToken);

    public Task LockDepartmentsAsync(IReadOnlyCollection<ulong> departmentIds, CancellationToken cancellationToken)
        => LockAsync(LockTarget.Departments, departmentIds, cancellationToken);

    public Task LockVisitRequestsAsync(IReadOnlyCollection<ulong> visitRequestIds, CancellationToken cancellationToken)
        => LockAsync(LockTarget.VisitRequests, visitRequestIds, cancellationToken);

    public Task LockVisitRequestCampusesAsync(IReadOnlyCollection<ulong> visitInstanceIds, CancellationToken cancellationToken)
        => LockAsync(LockTarget.VisitRequestCampuses, visitInstanceIds, cancellationToken);

    public Task LockVisitParticipantsAsync(IReadOnlyCollection<ulong> participantIds, CancellationToken cancellationToken)
        => LockAsync(LockTarget.VisitParticipants, participantIds, cancellationToken);

    public Task LockVisitLogisticsItemsAsync(IReadOnlyCollection<ulong> logisticsItemIds, CancellationToken cancellationToken)
        => LockAsync(LockTarget.VisitLogisticsItems, logisticsItemIds, cancellationToken);

    public async Task LockEmailActionTokenGroupAsync(string? actionGroupKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(actionGroupKey)) return;
        if (!_db.Database.IsRelational()) return;

        // action_group_key is attacker-adjacent (round-tripped through a public email link), so unlike
        // the numeric-id fast path above this one is parameterised rather than formatted inline.
        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT email_action_token_id FROM email_action_tokens WHERE action_group_key = {actionGroupKey} ORDER BY email_action_token_id FOR UPDATE",
            cancellationToken);
    }

    /// <summary>
    /// Closed set of tables this service ever locks. Table/column names cannot be parameterised in
    /// SQL, so instead of taking them as strings (which a future overload or call site could pass
    /// through from somewhere less trustworthy than the 6 callers above) this enum makes it a compile
    /// error to lock anything outside this list — <see cref="Resolve"/> is the only place the actual
    /// identifiers exist.
    /// </summary>
    private enum LockTarget
    {
        Users, Departments, VisitRequests, VisitRequestCampuses, VisitParticipants, VisitLogisticsItems
    }

    private static (string Table, string KeyColumn) Resolve(LockTarget target) => target switch
    {
        LockTarget.Users => ("users", "user_id"),
        LockTarget.Departments => ("departments", "department_id"),
        LockTarget.VisitRequests => ("visit_requests", "visit_request_id"),
        LockTarget.VisitRequestCampuses => ("visit_request_campuses", "visit_instance_id"),
        LockTarget.VisitParticipants => ("visit_participants", "participant_id"),
        LockTarget.VisitLogisticsItems => ("visit_logistics_items", "logistics_item_id"),
        _ => throw new System.ArgumentOutOfRangeException(nameof(target)),
    };

    private async Task LockAsync(
        LockTarget target, IReadOnlyCollection<ulong> ids, CancellationToken cancellationToken)
    {
        if (ids is null || ids.Count == 0) return;

        // A non-relational provider (the EF InMemory context the unit suite uses) has no row locks
        // to take; correctness there comes from the tests being single-threaded.
        if (!_db.Database.IsRelational()) return;

        var (table, keyColumn) = Resolve(target);

        var ordered = ids.Distinct().OrderBy(id => id)
            .Select(id => id.ToString(CultureInfo.InvariantCulture));

        var sql =
            $"SELECT {keyColumn} FROM {table} WHERE {keyColumn} IN ({string.Join(",", ordered)}) " +
            $"ORDER BY {keyColumn} FOR UPDATE";

        await _db.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }
}
