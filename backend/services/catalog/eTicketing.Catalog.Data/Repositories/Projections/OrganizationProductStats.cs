namespace eTicketing.Catalog.Data.Repositories;

/// <summary>Projection, not an entity — catalogue counts for one organization.
/// <paramref name="WithoutImage"/> counts products with no gallery photo at all, which is the
/// operational gap an Admin scans that column for.</summary>
public record OrganizationProductStats(
    Guid OrganizationId, int Total, int Published, int Draft, int WithoutImage);
