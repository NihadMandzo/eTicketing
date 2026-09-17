using System.Data.Common;
using eTicketing.Contracts.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace eTicketing.Shared.Messaging.Tests.TestFixtures;

/// <summary>
/// Makes <see cref="TransactionalInbox{TContext}"/> lose the race for its record, deterministically.
///
/// <para>Two real consumers inserting the same key at once cannot be staged on one in-memory Sqlite
/// connection — a transaction there locks the whole database, so the second writer would fail on
/// the lock rather than on the key. So both halves of the interleaving are played here instead: the
/// inbox's insert of its record is refused as a key violation would refuse it, and — when
/// <c>winnerCommits</c> — the "other consumer's" record is committed the moment the inbox rolls back,
/// which is exactly what the inbox then finds when it looks again.</para>
///
/// <para>With <c>winnerCommits: false</c> the insert fails and no record appears, standing in for a
/// failure that has nothing to do with a race; the inbox must then not claim a duplicate.</para>
/// </summary>
public sealed class LostInboxRaceInterceptor : DbTransactionInterceptor, ISaveChangesInterceptor
{
    private readonly string _messageId;
    private readonly string _consumer;
    private readonly bool _winnerCommits;
    private bool _refused;

    public LostInboxRaceInterceptor(string messageId, string consumer, bool winnerCommits)
    {
        _messageId = messageId;
        _consumer = consumer;
        _winnerCommits = winnerCommits;
    }

    public ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        var insertsRecord = eventData.Context!.ChangeTracker.Entries<InboxMessage>()
            .Any(e => e.State == EntityState.Added);

        if (!_refused && insertsRecord)
        {
            _refused = true;
            throw new DbUpdateException("Simulirano kršenje ključa InboxMessages.");
        }

        return ValueTask.FromResult(result);
    }

    public override async Task TransactionRolledBackAsync(
        DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        if (!_refused || !_winnerCommits)
            return;

        // After the rollback, so this commits on its own — as the winning consumer's did.
        await using var command = eventData.Context!.Database.GetDbConnection().CreateCommand();
        command.CommandText =
            "INSERT INTO InboxMessages (MessageId, Consumer, CreatedAt, UpdatedAt) VALUES ($id, $consumer, $now, $now)";
        AddParameter(command, "$id", _messageId);
        AddParameter(command, "$consumer", _consumer);
        AddParameter(command, "$now", DateTime.UtcNow);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
