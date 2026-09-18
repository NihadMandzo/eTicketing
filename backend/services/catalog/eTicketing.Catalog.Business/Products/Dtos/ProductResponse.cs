using eTicketing.Contracts.Pagination;
using eTicketing.Contracts.Persistence;
using Microsoft.AspNetCore.Http;

namespace eTicketing.Catalog.Business.Products;

public record ProductResponse(
    Guid Id,
    string Name,
    string Description,
    DateTime? Date,
    int CategoryId,
    string CategoryName,
    TicketingMode TicketingMode,
    Guid OrganizationId,
    PublishStatus Status,
    double Latitude,
    double Longitude,
    City City,
    IReadOnlyList<ProductImageResponse> Images,
    DateTime CreatedAt);
