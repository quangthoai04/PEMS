using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.VisitNotifications;

namespace PEMS.Infrastructure.Services;

/// <summary>
/// A cross-process claim built on MySQL's named advisory locks (plan §46).
///
/// <para>
/// <c>GET_LOCK</c> is used rather than a row lock because the thing being serialised is an outbound SMTP
/// call, not a row: holding a transaction open across a mail server's response time would keep a write
/// lock on business data for as long as the provider felt like taking. An advisory lock holds nothing
/// but its own name, and the server drops it if the connection dies — so a process that is killed
/// mid-send releases its claim instead of blocking recovery until someone notices.
/// </para>
/// <para>
/// It is taken with a zero timeout. A claim that is already held means another worker is sending this
/// very message right now, and the correct answer to that is to leave it alone, not to queue behind it
/// and send a second copy the moment it finishes.
/// </para>
/// </summary>
public sealed class MySqlEmailRecoveryLock : IEmailRecoveryLock
{
    /// <summary>
    /// MySQL truncates lock names over 64 characters, and two names that collide after truncation would
    /// serialise unrelated events. The keys this is called with are well inside it; anything longer is
    /// hashed rather than silently shortened.
    /// </summary>
    private const int MaxNameLength = 64;

    private readonly IApplicationDbContext _db;
    private readonly ILogger<MySqlEmailRecoveryLock> _logger;

    public MySqlEmailRecoveryLock(IApplicationDbContext db, ILogger<MySqlEmailRecoveryLock> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<IAsyncDisposable?> TryAcquireAsync(string key, CancellationToken cancellationToken)
    {
        var name = Shorten(key);

        try
        {
            var connection = _db.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
                await _db.Database.OpenConnectionAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT GET_LOCK(@name, 0)";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@name";
            parameter.Value = name;
            command.Parameters.Add(parameter);

            var granted = await command.ExecuteScalarAsync(cancellationToken);

            // 1 = taken, 0 = somebody else holds it, NULL = the server errored deciding.
            if (granted is null || granted is DBNull || Convert.ToInt64(granted) != 1)
                return null;

            return new Claim(_db, name, _logger);
        }
        catch (Exception ex)
        {
            // A database that cannot lend a lock must not stop a notification going out: the ledger in
            // sent_emails is still what prevents a duplicate, and this only narrows the window further.
            _logger.LogWarning(ex,
                "could not take the email recovery claim {Name}; proceeding without it", name);
            return new NoClaim();
        }
    }

    private static string Shorten(string key)
        => key.Length <= MaxNameLength
            ? key
            : $"pems:visit-notify:{Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(key)))[..32]}";

    private sealed class Claim : IAsyncDisposable
    {
        private readonly IApplicationDbContext _db;
        private readonly string _name;
        private readonly ILogger _logger;

        public Claim(IApplicationDbContext db, string name, ILogger logger)
        {
            _db = db;
            _name = name;
            _logger = logger;
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                var connection = _db.Database.GetDbConnection();
                if (connection.State != ConnectionState.Open) return;

                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT RELEASE_LOCK(@name)";
                var parameter = command.CreateParameter();
                parameter.ParameterName = "@name";
                parameter.Value = _name;
                command.Parameters.Add(parameter);
                await command.ExecuteScalarAsync();
            }
            catch (Exception ex)
            {
                // Not fatal: the lock dies with the connection anyway.
                _logger.LogWarning(ex, "could not release the email recovery claim {Name}", _name);
            }
        }
    }

    /// <summary>Nothing was claimed and nothing needs releasing.</summary>
    private sealed class NoClaim : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
