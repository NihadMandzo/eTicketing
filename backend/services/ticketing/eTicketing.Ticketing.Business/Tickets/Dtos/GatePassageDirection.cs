namespace eTicketing.Ticketing.Business.Tickets;

/// <summary>Which way through the gate a scan moved the holder.
///
/// Persisted nowhere — it is a per-scan answer, not ticket state — but serialized as the integer
/// ordinal like every other enum on this wire, so append only.</summary>
public enum GatePassageDirection
{
    /// <summary>Nothing moved: a rejected scan, or a one-shot ticket, which has no direction to
    /// speak of. The default, so existing clients that ignore the field are unaffected.</summary>
    None,

    /// <summary>The holder was admitted.</summary>
    Entry,

    /// <summary>A RecurringReservation holder left, re-arming their ticket for the next entry.</summary>
    Exit
}
