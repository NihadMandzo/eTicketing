using System.Threading.Channels;
using eTicketing.Ticketing.Business.TicketPrint;

namespace eTicketing.Ticketing.Api.Infrastructure.TicketPrint;

/// <summary>
/// In-process hand-off from a request thread to <see cref="TicketPrintRenderWorker"/>.
///
/// Unbounded and non-blocking on purpose: the durable record of outstanding work is the
/// TicketPrintBatch table, not this channel, so writing to it must never make an HTTP request wait
/// and dropping an item can never lose a batch — startup recovery finds anything that was missed.
/// </summary>
public class TicketPrintQueue : ITicketPrintQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions
    {
        // One worker loop drains this; the writers are request threads.
        SingleReader = true,
        SingleWriter = false,
    });

    public ChannelReader<Guid> Reader => _channel.Reader;

    public void Enqueue(Guid batchId) => _channel.Writer.TryWrite(batchId);
}
