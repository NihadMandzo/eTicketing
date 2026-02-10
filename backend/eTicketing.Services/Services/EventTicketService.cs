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
                throw new ForbiddenException("Nemate dozvolu za pristup ovoj ulaznici");
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
            throw new ValidationException("Događaj nije pronađen");
        }

        // Check authorization - user must belong to the event's organization
        var currentUserRole = _jwtHelper.GetUserRole();
        var organizationId = _jwtHelper.GetOrganizationId();

        if (currentUserRole == "OrganizationSuperAdmin" || currentUserRole == "OrganizationAdmin")
        {
            if (!organizationId.HasValue)
            {
                throw new ForbiddenException("Nemate dodijeljenu organizaciju");
            }

            if (eventEntity.OrganizationId != organizationId.Value)
            {
                throw new ForbiddenException("Možete kreirati ulaznice samo za događaje vaše organizacije");
            }
        }

        // Set the organization ID from the event
        entity.OrganizationId = eventEntity.OrganizationId;

        // Validate dates
        if (request.SaleStartDate.HasValue && request.SaleEndDate.HasValue &&
            request.SaleStartDate >= request.SaleEndDate)
        {
            throw new ValidationException("Datum početka prodaje mora biti prije datuma kraja prodaje");
        }

        if (request.SaleEndDate.HasValue && request.SaleEndDate < DateTime.UtcNow)
        {
            throw new ValidationException("Datum kraja prodaje ne može biti u prošlosti");
        }

        // Validate purchase quantities
        if (request.MaxPurchaseQuantity.HasValue && request.MinPurchaseQuantity.HasValue &&
            request.MaxPurchaseQuantity < request.MinPurchaseQuantity)
        {
            throw new ValidationException("Maksimalna količina kupovine mora biti veća ili jednaka minimalnoj količini kupovine");
        }

        if (request.MinPurchaseQuantity.HasValue && request.TotalTickets < request.MinPurchaseQuantity)
        {
            throw new ValidationException("Minimalna količina kupovine ne može premašiti ukupan broj ulaznica");
        }

        if (request.MaxPurchaseQuantity.HasValue && request.MaxPurchaseQuantity > request.TotalTickets)
        {
            throw new ValidationException("Maksimalna količina kupovine ne može premašiti ukupan broj ulaznica");
        }

        // Validate price for free tickets
        if (request.PriceType == "Free" && request.Price != 0)
        {
            throw new ValidationException("Besplatne ulaznice moraju imati cijenu 0");
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
                throw new ForbiddenException("Nemate dodijeljenu organizaciju");
            }

            if (entity.OrganizationId != organizationId.Value)
            {
                throw new ForbiddenException("Možete ažurirati ulaznice samo za događaje vaše organizacije");
            }
        }

        // Validate dates
        if (request.SaleStartDate.HasValue && request.SaleEndDate.HasValue &&
            request.SaleStartDate >= request.SaleEndDate)
        {
            throw new ValidationException("Datum početka prodaje mora biti prije datuma kraja prodaje");
        }

        // Only enforce "not in the past" rule when actually changing the sale end date
        if (request.SaleEndDate.HasValue && 
            request.SaleEndDate != entity.SaleEndDate && 
            request.SaleEndDate < DateTime.UtcNow)
        {
            throw new ValidationException("Datum kraja prodaje ne može biti u prošlosti");
        }

        // Validate purchase quantities
        if (request.MaxPurchaseQuantity.HasValue && request.MinPurchaseQuantity.HasValue &&
            request.MaxPurchaseQuantity < request.MinPurchaseQuantity)
        {
            throw new ValidationException("Maksimalna količina kupovine mora biti veća ili jednaka minimalnoj količini kupovine");
        }

        // Compute effective minimum (use request value if provided, otherwise use existing entity value)
        var effectiveMin = request.MinPurchaseQuantity ?? entity.MinPurchaseQuantity;
        if (effectiveMin.HasValue && request.TotalTickets < effectiveMin)
        {
            throw new ValidationException("Ukupan broj ulaznica ne može biti manji od minimalne količine kupovine");
        }

        // Validate total tickets - cannot reduce below tickets already sold
        if (request.TotalTickets < entity.TicketsSold)
        {
            throw new ValidationException($"Ne možete smanjiti ukupan broj ulaznica ispod {entity.TicketsSold} (već prodano)");
        }

        if (request.MaxPurchaseQuantity.HasValue && request.MaxPurchaseQuantity > request.TotalTickets)
        {
            throw new ValidationException("Maksimalna količina kupovine ne može premašiti ukupan broj ulaznica");
        }

        // Validate price for free tickets
        if (request.PriceType == "Free" && request.Price != 0)
        {
            throw new ValidationException("Besplatne ulaznice moraju imati cijenu 0");
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
                throw new ForbiddenException("Nemate dodijeljenu organizaciju");
            }

            if (entity.OrganizationId != organizationId.Value)
            {
                throw new ForbiddenException("Možete obrisati ulaznice samo za događaje vaše organizacije");
            }
        }

        // Prevent deletion if tickets have been sold
        if (entity.TicketsSold > 0)
        {
            throw new BusinessLogicException($"Ne možete obrisati tip ulaznice. {entity.TicketsSold} ulaznica je već prodano.");
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
            throw new KeyNotFoundException($"Događaj sa ID-om {eventId} nije pronađen");
        }

        // Authorization check for organization users
        var currentUserRole = _jwtHelper.GetUserRole();
        var organizationId = _jwtHelper.GetOrganizationId();

        // Enforce organization-based authorization for organization users
        if (currentUserRole == "OrganizationSuperAdmin" || currentUserRole == "OrganizationAdmin")
        {
            if (!organizationId.HasValue)
            {
                throw new ForbiddenException("Trenutni korisnik nije povezan sa organizacijom");
            }

            if (eventEntity.OrganizationId != organizationId.Value)
            {
                throw new ForbiddenException("Nemate dozvolu za pristup ulaznicama ovog događaja");
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
