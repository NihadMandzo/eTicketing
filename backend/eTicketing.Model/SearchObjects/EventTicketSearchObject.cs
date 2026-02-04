namespace eTicketing.Model.SearchObjects;

public class EventTicketSearchObject : BaseSearchObject
{
    public int? EventId { get; set; }
    public int? OrganizationId { get; set; }
    public string? PriceType { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public bool? IsActive { get; set; }
    public bool? HasAvailableTickets { get; set; }
    public DateTime? SaleStartDateFrom { get; set; }
    public DateTime? SaleEndDateTo { get; set; }
}
