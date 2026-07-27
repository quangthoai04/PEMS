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
