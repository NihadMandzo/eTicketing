using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace eTicketing.Shared.Messaging;

/// <summary>
/// <see cref="IOutboxDispatchLock"/> over a SQL Server application lock (<c>sp_getapplock</c>).
///
/// <para><b>Why an application lock rather than a claim column.</b> A <c>ClaimedBy</c>/
/// <c>ClaimedUntil</c> lease would need a migration in each of the three services that keep an
/// outbox, plus a sweep for leases whose holder died. An application lock needs neither: it lives in
/// SQL Server's lock manager, not in a table, and a holder that dies loses it with its session.</para>
///
/// <para><b>Session-owned, so the connection stays open for the whole pass.</b> The lock belongs to
/// the connection's session rather than to a transaction, because a pass is not one transaction —
/// it publishes to the broker between the read and the delete. Opening the connection explicitly is
/// what keeps EF Core from returning it to the pool between those two steps, which would hand the
/// lock to whichever request picked the connection up next.</para>
///
/// <para>Any other provider — the Sqlite in-memory databases the test projects use — is always
/// granted. Those are single-process by construction, so there is nothing to exclude.</para>
/// </summary>
public sealed class SqlServerOutboxDispatchLock : IOutboxDispatchLock
{
    private const string SqlServerProvider = "Microsoft.EntityFrameworkCore.SqlServer";

    // @LockTimeout = 0: never wait. A replica that finds the lock taken skips the pass rather than
    // queueing behind it, so no dispatcher ever holds a connection open doing nothing.
    private const string AcquireSql = """
        DECLARE @result int;
        EXEC @result = sp_getapplock
            @Resource = @lockResource, @LockMode = 'Exclusive', @LockOwner = 'Session', @LockTimeout = 0;
        SELECT @result;
        """;

    private const string ReleaseSql =
        "EXEC sp_releaseapplock @Resource = @lockResource, @LockOwner = 'Session';";

    public async Task<bool> TryAcquireAsync(DbContext context, CancellationToken ct)
    {
        if (!IsSqlServer(context))
            return true;

        await context.Database.OpenConnectionAsync(ct);

        try
        {
            // sp_getapplock returns 0 or 1 when granted, and a negative code for timeout,
            // cancellation, deadlock or error.
            var result = Convert.ToInt32(await ExecuteAsync(context, AcquireSql, ct));
            if (result >= 0)
                return true;
        }
        catch
        {
            await context.Database.CloseConnectionAsync();
            throw;
        }

        await context.Database.CloseConnectionAsync();
        return false;
    }

    public async Task ReleaseAsync(DbContext context)
    {
        if (!IsSqlServer(context))
            return;

        try
        {
            await ExecuteAsync(context, ReleaseSql, CancellationToken.None);
        }
        catch (DbException)
        {
            // The release only fails this way when the session is already gone — the same failure
            // that usually ended the pass. SQL Server drops a session-owned lock with its session, so
            // there is nothing left to release, and rethrowing here would only replace the pass's
            // own exception in the log with this less useful one.
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }
    }

    private static bool IsSqlServer(DbContext context) =>
        context.Database.ProviderName == SqlServerProvider;

    /// <summary>One lock per service database. The context's type name keeps the resource distinct
    /// should two services ever share a database.</summary>
    private static string ResourceFor(DbContext context) =>
        $"eticketing:outbox-dispatcher:{context.GetType().Name}";

    private static async Task<object?> ExecuteAsync(DbContext context, string sql, CancellationToken ct)
    {
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;

        var resource = command.CreateParameter();
        resource.ParameterName = "@lockResource";
        resource.Value = ResourceFor(context);
        command.Parameters.Add(resource);

        return await command.ExecuteScalarAsync(ct);
    }
}
