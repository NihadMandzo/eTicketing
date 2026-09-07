using System.Security.Claims;
using eTicketing.Contracts.Results;

namespace eTicketing.Ticketing.Business.Purchases;

public interface IPurchaseService
{
    /// <summary>
    /// Step one of the two-step purchase: prices the held reservation server-side and asks
    /// eTicketing.Payment for the payment object the buyer will confirm in their browser.
    ///
    /// It runs exactly the same validation <see cref="PurchaseAsync"/> does, so an expired hold or a
    /// line item that does not belong to the held sector is refused before any payment object exists
    /// at the provider at all.
    /// </summary>
    Task<Result<PurchaseIntentResponse>> CreatePaymentIntentAsync(
        CreatePaymentIntentRequest request, ClaimsPrincipal user, CancellationToken ct = default);

    /// <summary>Step two: re-validates everything, captures the payment the buyer already
    /// authorized, confirms the hold, mints one Ticket per admission unit, and publishes one
    /// TicketPurchased for the order. See .claude/rules/01-domain.md's purchase critical path and
    /// PurchaseService's own class doc for the full step-by-step.</summary>
    Task<Result<PurchaseResponse>> PurchaseAsync(PurchaseRequest request, ClaimsPrincipal user, CancellationToken ct = default);
}
