using eTicketing.Contracts.Persistence;
using PaymentEntity = eTicketing.Payment.Data.Entities.Payment;

namespace eTicketing.Payment.Data.Repositories;

public interface IPaymentRepository : IRepository<PaymentEntity, Guid>
{
}
