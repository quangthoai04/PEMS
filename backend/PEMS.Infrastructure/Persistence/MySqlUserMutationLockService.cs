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
        => LockAsync("users", "user_id", userIds, cancellationToken);

    public Task LockDepartmentsAsync(IReadOnlyCollection<ulong> departmentIds, CancellationToken cancellationToken)
        => LockAsync("departments", "department_id", departmentIds, cancellationToken);

    public Task LockVisitRequestsAsync(IReadOnlyCollection<ulong> visitRequestIds, CancellationToken cancellationToken)
        => LockAsync("visit_requests", "visit_request_id", visitRequestIds, cancellationToken);

    public Task LockVisitRequestCampusesAsync(IReadOnlyCollection<ulong> visitInstanceIds, CancellationToken cancellationToken)
        => LockAsync("visit_request_campuses", "visit_instance_id", visitInstanceIds, cancellationToken);

    public Task LockVisitParticipantsAsync(IReadOnlyCollection<ulong> participantIds, CancellationToken cancellationToken)
        => LockAsync("visit_participants", "participant_id", participantIds, cancellationToken);

    public Task LockVisitLogisticsItemsAsync(IReadOnlyCollection<ulong> logisticsItemIds, CancellationToken cancellationToken)
        => LockAsync("visit_logistics_items", "logistics_item_id", logisticsItemIds, cancellationToken);

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

    private async Task LockAsync(
        string table, string keyColumn, IReadOnlyCollection<ulong> ids, CancellationToken cancellationToken)
    {
        if (ids is null || ids.Count == 0) return;

        // A non-relational provider (the EF InMemory context the unit suite uses) has no row locks
        // to take; correctness there comes from the tests being single-threaded.
        if (!_db.Database.IsRelational()) return;

        var ordered = ids.Distinct().OrderBy(id => id)
            .Select(id => id.ToString(CultureInfo.InvariantCulture));

        var sql =
            $"SELECT {keyColumn} FROM {table} WHERE {keyColumn} IN ({string.Join(",", ordered)}) " +
            $"ORDER BY {keyColumn} FOR UPDATE";

        await _db.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }
}
