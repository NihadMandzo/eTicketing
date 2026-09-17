using eTicketing.Ticketing.Data.Entities;
using Mapster;

namespace eTicketing.Ticketing.Business.TicketPrint.Mapping;

/// <summary>
/// The organizer's print-batch row. Sixteen members, every one of them a name match — which is
/// precisely why it was worth moving: a hand-written sixteen-argument positional constructor call is
/// the kind of thing where a transposed <c>SerialFrom</c>/<c>SerialTo</c> or
/// <c>TicketCount</c>/<c>RenderedCount</c> compiles perfectly and reads plausibly.
///
/// <para>The rendered PDF itself is deliberately absent from the response — it is streamed once
/// through the download endpoint, never inlined into a list — and Mapster drops the unmatched
/// source member without being told to.</para>
/// </summary>
public class TicketPrintMappingConfig : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<TicketPrintBatch, TicketPrintBatchResponse>();
    }
}
