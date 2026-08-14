using eTicketing.Contracts.Persistence;

namespace eTicketing.Catalog.Data.Entities;

/// <summary>
/// Read-only in this iteration — no create/publish/organizer flow exists yet (see
/// SPRINTS/SPRINT_2.md for that future scope). Only the fields needed for the superadmin
/// browsing feature (organization detail's event count + paginated event list) are populated
/// today; rows come from Seeders/EventSeeder.cs (via EventConfiguration's HasData), not created
/// through the API.
/// </summary>
public class Event : BaseEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime Date { get; set; }

    public int CategoryId { get; set; }
    public Category? Category { get; set; }

    // Organization lives in Identity's separate database/microservice — plain Guid column,
    // deliberately no FK/navigation across the service boundary.
    public Guid OrganizationId { get; set; }

    public PublishStatus Status { get; set; } = PublishStatus.Draft;
    public string? ImageUrl { get; set; }
}
