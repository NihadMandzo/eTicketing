using eTicketing.Contracts.Events;

namespace eTicketing.Ticketing.Business.Integration;

public interface IProductDeletionNotifier
{
    /// <summary>Fans a <see cref="ProductDeleted"/> out into one
    /// <see cref="ProductDeletedNotification"/> per recipient: every buyer holding a still-valid
    /// ticket, plus the owning organization when platform staff did the deleting.</summary>
    Task NotifyAsync(ProductDeleted message, CancellationToken ct = default);
}
