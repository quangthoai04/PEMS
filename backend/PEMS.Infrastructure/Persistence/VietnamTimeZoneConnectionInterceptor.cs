using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace PEMS.Infrastructure.Persistence;

/// <summary>
/// Forces every application MySQL session to <c>time_zone = '+07:00'</c> so that
/// <c>CURRENT_TIMESTAMP</c> / <c>ON UPDATE CURRENT_TIMESTAMP</c> defaults and <c>NOW()</c>
/// in triggers generate Vietnam wall-clock values, matching the PEMS persistence policy
/// (all PEMS-managed DATETIME columns store Asia/Ho_Chi_Minh wall-clock).
///
/// Runs on EVERY connection open — with connection pooling MySqlConnector resets the
/// session on checkout, so a one-off <c>SET GLOBAL</c> (which managed MySQL often forbids
/// anyway) is not a substitute. Uses the session-scoped statement only; no special
/// privileges required.
/// </summary>
public sealed class VietnamTimeZoneConnectionInterceptor : DbConnectionInterceptor
{
    private const string SetSessionTimeZoneSql = "SET time_zone = '+07:00';";

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        using var command = connection.CreateCommand();
        command.CommandText = SetSessionTimeZoneSql;
        command.ExecuteNonQuery();
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = SetSessionTimeZoneSql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
