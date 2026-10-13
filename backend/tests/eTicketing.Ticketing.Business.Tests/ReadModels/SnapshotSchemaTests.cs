using eTicketing.Ticketing.Business.Tests.TestFixtures;
using eTicketing.Ticketing.Data.Entities;
using FluentAssertions;

namespace eTicketing.Ticketing.Business.Tests.ReadModels;

/// <summary>
/// Pins the two read models to the columns something actually reads. Every field here was traced to
/// a consumer — the sale gate, the ticket PDF and email, the gate config, the reports, the
/// cancellation notice — and the ones nothing read (audit timestamps, the product's organization and
/// ticketing mode, the organization's active flag) were removed. A column added back without a
/// reader fails here instead of quietly widening the projection again.
/// </summary>
public class SnapshotSchemaTests : IDisposable
{
    private readonly TicketingTestContext _fixture = new();

    private IEnumerable<string> ColumnsOf<T>() =>
        _fixture.DbContext.Model.FindEntityType(typeof(T))!.GetProperties().Select(p => p.Name);

    [Fact]
    public void ProductSnapshot_Model_HasOnlyRequiredColumns()
    {
        ColumnsOf<ProductSnapshot>().Should().BeEquivalentTo(
            nameof(ProductSnapshot.ProductId),
            nameof(ProductSnapshot.Name),
            nameof(ProductSnapshot.Date),
            nameof(ProductSnapshot.City),
            nameof(ProductSnapshot.Status),
            nameof(ProductSnapshot.ChangedAt));
    }

    [Fact]
    public void OrganizationSnapshot_Model_HasOnlyRequiredColumns()
    {
        ColumnsOf<OrganizationSnapshot>().Should().BeEquivalentTo(
            nameof(OrganizationSnapshot.OrganizationId),
            nameof(OrganizationSnapshot.Name),
            nameof(OrganizationSnapshot.Address),
            nameof(OrganizationSnapshot.Email),
            nameof(OrganizationSnapshot.PhoneNumber),
            nameof(OrganizationSnapshot.SuperAdminEmail),
            nameof(OrganizationSnapshot.ChangedAt));
    }

    public void Dispose() => _fixture.Dispose();
}
