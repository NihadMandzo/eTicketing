using eTicketing.Catalog.Data.Entities;
using eTicketing.Contracts.Persistence;

namespace eTicketing.Catalog.Data.Repositories;

/// <summary>No extra query methods beyond generic CRUD — ProductService always reaches
/// ProductImage rows through the owning Product (via GetByIdWithCategoryAsync's Include), this
/// repository exists only so AddAsync goes through DbSet.AddAsync (explicit Added state) instead
/// of collection.Add() on an already-tracked Product's navigation, which EF Core would otherwise
/// mistake for an update (see ProductService.UploadImageAsync).</summary>
public interface IProductImageRepository : IRepository<ProductImage, Guid>;
