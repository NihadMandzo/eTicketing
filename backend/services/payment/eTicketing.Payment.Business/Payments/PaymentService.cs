using eTicketing.Contracts.Persistence;
using eTicketing.Contracts.Results;
using eTicketing.Payment.Data.Repositories;
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
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<PaymentResponse>.Success(new PaymentResponse(payment.Id, payment.Amount, payment.Status, payment.OrderRef, payment.CreatedAt));
    }
}
