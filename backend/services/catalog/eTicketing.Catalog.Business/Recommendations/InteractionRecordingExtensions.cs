using eTicketing.Catalog.Data.Entities;
using eTicketing.Catalog.Data.Repositories;
using eTicketing.Contracts.Persistence;
using Microsoft.EntityFrameworkCore;

namespace eTicketing.Catalog.Business.Recommendations;

/// <summary>
/// The one supported way to write an interaction signal. Both producers — the HTTP view tracker in
/// <see cref="RecommendationService"/> and the purchase consumer in
/// <see cref="PurchaseInteractionRecorder"/> — go through here rather than calling UpsertAsync and
/// SaveChangesAsync themselves, so the race below is handled once instead of twice.
/// </summary>
public static class InteractionRecordingExtensions
{
    /// <summary>
    /// Records one occurrence of (user, product, type) and commits it, surviving a lost race
    /// against a concurrent writer of the same key.
    ///
    /// UpsertAsync reads before it writes, so two callers for the same key can both find no row and
    /// both queue an insert; the unique index in UserInteractionConfiguration rejects the loser.
    /// That is not an exotic case here — POST /recommendations/views fires on every product-detail
    /// open, so two tabs or a quick back/forward is enough, and an unhandled DbUpdateException
    /// would cost the visitor a 500 *and* silently drop the signal. The winner's row is exactly
    /// what this call wanted to write to, so the recovery is to drop the rejected insert and run
    /// the upsert again: the second pass finds that row and increments it, leaving the count equal
    /// to the number of occurrences that actually happened.
    ///
    /// Same shape as the concurrent-charge recovery in eTicketing.Payment's PaymentService — the
    /// save stays in the business layer, per .claude/rules/10-backend.md.
    /// </summary>
    public static async Task RecordOccurrenceAsync(
        this IUserInteractionRepository interactions,
        IUnitOfWork unitOfWork,
        Guid userId,
        Guid productId,
        InteractionType type,
        DateTime occurredAt,
        CancellationToken ct = default)
    {
        try
        {
            await interactions.UpsertAsync(userId, productId, type, occurredAt, ct);
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            interactions.DiscardPendingInsert();

            // Deliberately not retried a second time: one loss is the race this method exists for,
            // and a second failure means something other than a concurrent insert is wrong — which
            // belongs in the global exception handler, not in a silent loop.
            await interactions.UpsertAsync(userId, productId, type, occurredAt, ct);
            await unitOfWork.SaveChangesAsync(ct);
        }
    }
}
