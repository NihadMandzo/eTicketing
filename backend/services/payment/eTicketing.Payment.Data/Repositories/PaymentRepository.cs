using eTicketing.Contracts.Persistence;
using PaymentEntity = eTicketing.Payment.Data.Entities.Payment;

namespace eTicketing.Payment.Data.Repositories;

public class PaymentRepository : Repository<PaymentEntity, Guid>, IPaymentRepository
{
    public PaymentRepository(PaymentDbContext context) : base(context) { }
}
