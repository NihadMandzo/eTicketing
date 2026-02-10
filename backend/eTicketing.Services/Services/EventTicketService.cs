using AutoMapper;
using eTicketing.Model.Exceptions;
using eTicketing.Model.Requests;
using eTicketing.Model.Responses;
using eTicketing.Model.SearchObjects;
using eTicketing.Services.Database;
using eTicketing.Services.Database.Entities;
using eTicketing.Services.Helpers;
using eTicketing.Services.Interfaces;
using eTicketing.Services.Services.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace eTicketing.Services.Services;

public class EventTicketService : BaseCRUDService<EventTicket, EventTicketResponse, EventTicketSearchObject, 
    EventTicketInsertRequest, EventTicketUpdateRequest>, IEventTicketService
{
    private readonly JwtHelper _jwtHelper;
    private readonly AuthorizationHelper _authorizationHelper;
    private readonly ILogger<EventTicketService> _logger;

    public EventTicketService(
        eTicketingDbContext context, 
        IMapper mapper,
        JwtHelper jwtHelper,
        AuthorizationHelper authorizationHelper,
        ILogger<EventTicketService> logger) : base(context, mapper)
    {
        _jwtHelper = jwtHelper;
        _authorizationHelper = authorizationHelper;
        _logger = logger;
    }

    protected override IQueryable<EventTicket> ApplyFilter(IQueryable<EventTicket> query, EventTicketSearchObject? search)
    {
        // Authorization filtering
        var currentUserRole = _jwtHelper.GetUserRole();
        var organizationId = _jwtHelper.GetOrganizationId();

        // OrganizationSuperAdmin and OrganizationAdmin can only see their organization's tickets
        if (currentUserRole == "OrganizationSuperAdmin" || currentUserRole == "OrganizationAdmin")
        {
            query = organizationId.HasValue
                ? query.Where(x => x.OrganizationId == organizationId.Value)
                : query.Where(x => false); // No organization - return empty
        }
        // SuperAdmin, Admin, and User can see all tickets

        // Search filters
        if (!string.IsNullOrWhiteSpace(search?.FTS))
        {
            query = query.Where(x => 
                x.TicketType.Contains(search.FTS) || 
                (x.Description != null && x.Description.Contains(search.FTS)));
        }

        if (search?.EventId.HasValue == true)
        {
            query = query.Where(x => x.EventId == search.EventId.Value);
        }

        if (search?.OrganizationId.HasValue == true)
        {
            query = query.Where(x => x.OrganizationId == search.OrganizationId.Value);
        }

        if (search?.IsActive.HasValue == true)
        {
            query = query.Where(x => x.IsActive == search.IsActive.Value);
        }

        // Include related entities
        query = query.Include(x => x.Event)
                     .Include(x => x.Organization);

        // Order by creation date descending
        query = query.OrderByDescending(x => x.CreatedAt);

        return query;
    }

    public override async Task<EventTicketResponse?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var query = Context.Set<EventTicket>()
            .Include(x => x.Event)
            .Include(x => x.Organization)
            .Where(x => x.Id == id);

        // Authorization check
        var currentUserRole = _jwtHelper.GetUserRole();
        var organizationId = _jwtHelper.GetOrganizationId();

        if (currentUserRole == "OrganizationSuperAdmin" || currentUserRole == "OrganizationAdmin")
        {
            if (organizationId.HasValue)
            {
                query = query.Where(x => x.OrganizationId == organizationId.Value);
            }
            else
            {
                throw new ForbiddenException("You don't have permission to access this ticket");
            }
        }

        var entity = await query.FirstOrDefaultAsync(cancellationToken);
        
        if (entity == null)
            return null;

        return MapToResponse(entity);
    }

    protected override async Task BeforeCreateAsync(EventTicket entity, EventTicketInsertRequest request, CancellationToken cancellationToken)
    {
        // Validate Event exists
        var eventEntity = await Context.Set<Event>()
            .Include(e => e.Organization)
            .FirstOrDefaultAsync(e => e.Id == request.EventId, cancellationToken);

        if (eventEntity == null)
        {
            throw new ValidationException("Event not found");
        }

        // Check authorization - user must belong to the event's organization
        var currentUserRole = _jwtHelper.GetUserRole();
        var organizationId = _jwtHelper.GetOrganizationId();

        if (currentUserRole == "OrganizationSuperAdmin" || currentUserRole == "OrganizationAdmin")
        {
            if (!organizationId.HasValue)
            {
                throw new ForbiddenException("You don't have an organization assigned");
            }

            if (eventEntity.OrganizationId != organizationId.Value)
            {
                throw new ForbiddenException("You can only create tickets for your organization's events");
            }
        }

        // Set the organization ID from the event
        entity.OrganizationId = eventEntity.OrganizationId;

        // Validate dates
        if (request.SaleStartDate.HasValue && request.SaleEndDate.HasValue &&
            request.SaleStartDate >= request.SaleEndDate)
        {
            throw new ValidationException("Sale start date must be before sale end date");
        }

        if (request.SaleEndDate.HasValue && request.SaleEndDate < DateTime.UtcNow)
        {
            throw new ValidationException("Sale end date cannot be in the past");
        }

        // Validate purchase quantities
        if (request.MaxPurchaseQuantity.HasValue && request.MinPurchaseQuantity.HasValue &&
            request.MaxPurchaseQuantity < request.MinPurchaseQuantity)
        {
            throw new ValidationException("Maximum purchase quantity must be greater than or equal to minimum purchase quantity");
        }

        if (request.MinPurchaseQuantity.HasValue && request.TotalTickets < request.MinPurchaseQuantity)
        {
            throw new ValidationException("Minimum purchase quantity cannot exceed total tickets");
        }

        if (request.MaxPurchaseQuantity.HasValue && request.MaxPurchaseQuantity > request.TotalTickets)
        {
            throw new ValidationException("Maximum purchase quantity cannot exceed total tickets");
        }

        // Validate price for free tickets
        if (request.PriceType == "Free" && request.Price != 0)
        {
            throw new ValidationException("Free tickets must have a price of 0");
        }

        _logger.LogInformation("Creating new ticket type '{TicketType}' for event ID {EventId}", 
            request.TicketType, request.EventId);
    }

    protected override async Task BeforeUpdateAsync(EventTicket entity, EventTicketUpdateRequest request, CancellationToken cancellationToken)
    {
        // Check authorization
        var currentUserRole = _jwtHelper.GetUserRole();
        var organizationId = _jwtHelper.GetOrganizationId();

        if (currentUserRole == "OrganizationSuperAdmin" || currentUserRole == "OrganizationAdmin")
        {
            if (!organizationId.HasValue)
            {
                throw new ForbiddenException("You don't have an organization assigned");
            }

            if (entity.OrganizationId != organizationId.Value)
            {
                throw new ForbiddenException("You can only update tickets for your organization's events");
            }
        }

        // Validate dates
        if (request.SaleStartDate.HasValue && request.SaleEndDate.HasValue &&
            request.SaleStartDate >= request.SaleEndDate)
        {
            throw new ValidationException("Sale start date must be before sale end date");
        }

        // Only enforce "not in the past" rule when actually changing the sale end date
        if (request.SaleEndDate.HasValue && 
            request.SaleEndDate != entity.SaleEndDate && 
            request.SaleEndDate < DateTime.UtcNow)
        {
            throw new ValidationException("Sale end date cannot be in the past");
        }

        // Validate purchase quantities
        if (request.MaxPurchaseQuantity.HasValue && request.MinPurchaseQuantity.HasValue &&
            request.MaxPurchaseQuantity < request.MinPurchaseQuantity)
        {
            throw new ValidationException("Maximum purchase quantity must be greater than or equal to minimum purchase quantity");
        }

        // Compute effective minimum (use request value if provided, otherwise use existing entity value)
        var effectiveMin = request.MinPurchaseQuantity ?? entity.MinPurchaseQuantity;
        if (effectiveMin.HasValue && request.TotalTickets < effectiveMin)
        {
            throw new ValidationException("Total tickets cannot be less than minimum purchase quantity");
        }

        // Validate total tickets - cannot reduce below tickets already sold
        if (request.TotalTickets < entity.TicketsSold)
        {
            throw new ValidationException($"Cannot reduce total tickets below {entity.TicketsSold} (already sold)");
        }

        if (request.MaxPurchaseQuantity.HasValue && request.MaxPurchaseQuantity > request.TotalTickets)
        {
            throw new ValidationException("Maximum purchase quantity cannot exceed total tickets");
        }

        // Validate price for free tickets
        if (request.PriceType == "Free" && request.Price != 0)
        {
            throw new ValidationException("Free tickets must have a price of 0");
        }

        _logger.LogInformation("Updating ticket ID {TicketId} for event ID {EventId}", 
            entity.Id, entity.EventId);
    }

    public override async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await Context.Set<EventTicket>()
            .Include(x => x.Organization)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        
        if (entity == null)
            return false;

        // Check authorization
        var currentUserRole = _jwtHelper.GetUserRole();
        var organizationId = _jwtHelper.GetOrganizationId();

        if (currentUserRole == "OrganizationSuperAdmin" || currentUserRole == "OrganizationAdmin")
        {
            if (!organizationId.HasValue)
            {
                throw new ForbiddenException("You don't have an organization assigned");
            }

            if (entity.OrganizationId != organizationId.Value)
            {
                throw new ForbiddenException("You can only delete tickets for your organization's events");
            }
        }

        // Prevent deletion if tickets have been sold
        if (entity.TicketsSold > 0)
        {
            throw new BusinessLogicException($"Cannot delete ticket type. {entity.TicketsSold} tickets have already been sold.");
        }

        Context.Set<EventTicket>().Remove(entity);
        await Context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Deleted ticket ID {TicketId}", id);
        
        return true;
    }

    public async Task<List<EventTicketResponse>> GetByEventIdAsync(int eventId, CancellationToken cancellationToken = default)
    {
        // First, verify that the event exists
        var eventEntity = await Context.Set<Event>()
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == eventId, cancellationToken);

        if (eventEntity == null)
        {
            throw new KeyNotFoundException($"Event with id {eventId} not found");
        }

        // Authorization check for organization users
        var currentUserRole = _jwtHelper.GetUserRole();
        var organizationId = _jwtHelper.GetOrganizationId();

        // Enforce organization-based authorization for organization users
        if (currentUserRole == "OrganizationSuperAdmin" || currentUserRole == "OrganizationAdmin")
        {
            if (!organizationId.HasValue)
            {
                throw new ForbiddenException("Current user is not associated with an organization");
            }

            if (eventEntity.OrganizationId != organizationId.Value)
            {
                throw new ForbiddenException("You are not authorized to access tickets for this event");
            }
        }

        var query = Context.Set<EventTicket>()
            .Include(x => x.Event)
            .Include(x => x.Organization)
            .Where(x => x.EventId == eventId);

        var tickets = await query
            .OrderBy(x => x.Price)
            .ToListAsync(cancellationToken);

        return Mapper.Map<List<EventTicketResponse>>(tickets);
    }

    protected override EventTicketResponse MapToResponse(EventTicket entity)
    {
        return Mapper.Map<EventTicketResponse>(entity);
    }
}
