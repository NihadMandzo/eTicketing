using Mapster;
using PaymentEntity = eTicketing.Payment.Data.Entities.Payment;

namespace eTicketing.Payment.Business.Payments.Mapping;

/// <summary>
/// <see cref="PaymentEntity"/> → <see cref="PaymentResponse"/>: every destination member matches a
/// source member by name, so there is nothing to configure — and that is exactly why this class
/// exists rather than a hand-written projection. The mapping is discoverable in the place the other
/// services keep theirs, and the next field that needs special handling has somewhere to go.
///
/// <para>What the response deliberately does NOT carry is the point worth recording: the entity also
/// holds <c>Provider</c>, <c>UserId</c>, <c>ProviderPaymentIntentId</c> and
/// <c>ProviderSubscriptionId</c>. Mapster drops unmatched source members silently, so this comment
/// is the only thing standing between a future widening of <see cref="PaymentResponse"/> and
/// provider identifiers quietly appearing in a cross-service HTTP response.</para>
///
/// <para><c>ToSubscriptionResponse</c> stays hand-written. It is not a projection of one type onto
/// another — it takes a <see cref="PaymentResponse"/>, a subscription reference and an optional
/// gateway result, and coalesces two of its fields from a fallback when the provider was never
/// consulted. A config cannot express arguments, and <c>Adapt</c>-plus-<c>with</c> would leave a
/// non-nullable <c>SubscriptionReference</c> momentarily null on the way through.</para>
/// </summary>
public class PaymentMappingConfig : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<PaymentEntity, PaymentResponse>();
    }
}
