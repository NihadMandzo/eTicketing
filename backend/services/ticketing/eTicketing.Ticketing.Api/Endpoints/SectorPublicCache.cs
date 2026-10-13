namespace eTicketing.Ticketing.Api.Endpoints;

/// <summary>
/// The public sector list carries the live <c>RemainingCapacity</c> counter, so it is cached for
/// only 5 seconds and is not evicted on holds or purchases — evicting on every hold would empty the
/// cache exactly when it is needed most, during a sale. The counter can therefore be up to 5 s stale.
/// That is safe because it is only a display hint: <c>POST /sectors/{id}/hold</c> reads Redis
/// directly and answers 409 when the seats are really gone, so a stale list can never oversell.
/// </summary>
internal static class SectorPublicCache
{
    public const string Tag = "ticketing-public-sectors";

    public static readonly TimeSpan Duration = TimeSpan.FromSeconds(5);
}
