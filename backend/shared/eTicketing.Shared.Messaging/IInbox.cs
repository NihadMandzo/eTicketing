namespace eTicketing.Shared.Messaging;

/// <summary>
/// Runs a consumer's handler at most once per message. See
/// <see cref="TransactionalInbox{TContext}"/> for how, and eTicketing.Contracts.Messaging.InboxMessage
/// for why.
/// </summary>
public interface IInbox
{
    /// <summary>
    /// Runs <paramref name="handler"/> unless <paramref name="consumer"/> has already processed
    /// <paramref name="messageId"/>, and records it as processed in the same transaction as the
    /// handler's own writes.
    ///
    /// <para>Returns false when the message was a repeat and the handler was skipped. An exception
    /// from the handler propagates unchanged, with nothing recorded, so the consumer's retry runs the
    /// handler again.</para>
    ///
    /// <para>A message with no id is processed every time, exactly as it was before the inbox
    /// existed — there is nothing to recognise a repeat by.</para>
    /// </summary>
    Task<bool> ProcessOnceAsync(
        string? messageId, string consumer, Func<CancellationToken, Task> handler, CancellationToken ct = default);
}
