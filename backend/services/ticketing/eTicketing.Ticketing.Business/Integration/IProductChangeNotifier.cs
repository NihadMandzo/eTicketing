using eTicketing.Contracts.Events;

namespace eTicketing.Ticketing.Business.Integration;

public interface IProductChangeNotifier
{
    /// <summary>Fans a <see cref="ProductUpdated"/> out into one
    /// <see cref="ProductChangedNotification"/> per distinct buyer still holding a live ticket for
    /// that product.</summary>
    Task NotifyBuyersAsync(ProductUpdated message, CancellationToken ct = default);
}
