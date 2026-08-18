using eTicketing.Catalog.Data.Entities;
using eTicketing.Contracts.Persistence;

namespace eTicketing.Catalog.Data.Repositories;

public class ProductImageRepository : Repository<ProductImage, Guid>, IProductImageRepository
{
    public ProductImageRepository(CatalogDbContext context) : base(context) { }
}
