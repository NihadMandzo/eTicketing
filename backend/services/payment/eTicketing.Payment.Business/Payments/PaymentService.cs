using eTicketing.Contracts.Persistence;
using eTicketing.Contracts.Results;
using eTicketing.Payment.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using PaymentEntity = eTicketing.Payment.Data.Entities.Payment;
using PaymentStatus = eTicketing.Payment.Data.Entities.PaymentStatus;

namespace eTicketing.Payment.Business.Payments;

public class PaymentService : IPaymentService
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly IUnitOfWork _unitOfWork;

    public PaymentService(IPaymentRepository paymentRepository, IUnitOfWork unitOfWork)
    {
        _paymentRepository = paymentRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<PaymentResponse>> ChargeAsync(ChargeRequest request, CancellationToken ct = default)
    {
        // OrderRef is the idempotency key (unique index — see PaymentConfiguration). A caller can
        // legitimately retry with the same OrderRef (e.g. Ticketing's Polly retry re-sending after
        // the response to an already-successful charge was lost) — replaying must return the
        // original result rather than charging the card a second time.
        var existing = await _paymentRepository.GetByOrderRefAsync(request.OrderRef, ct);
        if (existing is not null)
            return Result<PaymentResponse>.Success(ToResponse(existing));

        // Deterministic mock per docs/payment-setup-guide.md §2.1: a card ending "0000" simulates a
        // decline, so the circuit-breaker/decline paths can be demoed without any external gateway.
        var status = request.CardNumberLast4 == "0000" ? PaymentStatus.Failed : PaymentStatus.Succeeded;

        var payment = new PaymentEntity
        {
            Id = Guid.NewGuid(),
            Amount = request.Amount,
            Status = status,
            OrderRef = request.OrderRef,
        };

        await _paymentRepository.AddAsync(payment, ct);

        try
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Two concurrent charges for the same OrderRef raced past the GetByOrderRefAsync check
            // above; the unique index rejected the loser. The loser isn't a failure — the winner's
            // row is the authoritative result, so fetch and return it instead of erroring out.
            var winner = await _paymentRepository.GetByOrderRefAsync(request.OrderRef, ct);
            if (winner is not null)
                return Result<PaymentResponse>.Success(ToResponse(winner));

            throw;
        }

        return Result<PaymentResponse>.Success(ToResponse(payment));
    }

    private static PaymentResponse ToResponse(PaymentEntity payment) =>
        new(payment.Id, payment.Amount, payment.Status, payment.OrderRef, payment.CreatedAt);
}
