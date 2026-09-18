namespace eTicketing.Catalog.Business.Products;

/// <summary>One uploaded gallery photo, URL derived from Azure Blob Storage — never a stored
/// column, same reasoning as CategoryResponse.IconUrl.</summary>
public record ProductImageResponse(Guid Id, string Url, int DisplayOrder);
