using eTicketing.Catalog.Business.Events;
using eTicketing.Contracts.Results;
using eTicketing.Contracts.Validation;

namespace eTicketing.Catalog.Api.Endpoints;

/// <summary>
/// Read-only for now — no organizer-facing create/publish flow exists yet (see
/// SPRINTS/SPRINT_2.md for that future scope). These two endpoints exist purely to back the
/// superadmin organization-browsing feature: an org's event count + paginated event list, and
/// the org-list's category multiselect filter.
/// </summary>
public static class EventEndpoints
{
    public static void MapEventEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/events").WithTags("Events");

        group.MapGet("/all", GetAll).RequireAuthorization("PlatformStaff").WithValidation<EventQuery>();
        group.MapGet("/organization-ids", GetOrganizationIds).RequireAuthorization("PlatformStaff");
    }

    private static async Task<IResult> GetAll([AsParameters] EventQuery query, IEventService service, CancellationToken ct)
    {
        var result = await service.GetAllAsync(query, ct);
        return result.ToHttpResult();
    }

    /// <summary>categoryIds is a single comma-joined query param ("?categoryIds=1,2,3"),
    /// deliberately not ASP.NET Core's repeated-key array binding — matches how the desktop
    /// client builds this call.</summary>
    private static async Task<IResult> GetOrganizationIds(string? categoryIds, IEventService service, CancellationToken ct)
    {
        var ids = (categoryIds ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => int.TryParse(s, out var v) ? v : (int?)null)
            .Where(v => v.HasValue)
            .Select(v => v!.Value)
            .Distinct()
            .ToList();

        if (ids.Count == 0) return Results.Ok(new List<Guid>());

        var result = await service.GetOrganizationIdsAsync(ids, ct);
        return result.ToHttpResult();
    }
}
