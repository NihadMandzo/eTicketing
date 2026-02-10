using AutoMapper;
using eTicketing.Model.Enums;
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

public class EventService : BaseCRUDService<Event, EventResponse, EventSearchObject, 
    EventInsertRequest, EventUpdateRequest>, IEventService
{
    private readonly JwtHelper _jwtHelper;
    private readonly AuthorizationHelper _authorizationHelper;
    private readonly IBlobStorageService _blobStorageService;
    private readonly ILogger<EventService> _logger;
    private const string EventImagesContainer = "event-images";
    private readonly Dictionary<int, List<string>> _blobsToDeleteAfterCommit = new();
    private readonly Dictionary<int, List<string>> _newlyUploadedBlobs = new();

    public EventService(
        eTicketingDbContext context, 
        IMapper mapper,
        JwtHelper jwtHelper,
        AuthorizationHelper authorizationHelper,
        IBlobStorageService blobStorageService,
        ILogger<EventService> logger) : base(context, mapper)
    {
        _jwtHelper = jwtHelper;
        _authorizationHelper = authorizationHelper;
        _blobStorageService = blobStorageService;
        _logger = logger;
    }

    protected override IQueryable<Event> ApplyFilter(IQueryable<Event> query, EventSearchObject? search)
    {
        // Authorization filtering
        var currentUserRole = _jwtHelper.GetUserRole();
        var organizationId = _jwtHelper.GetOrganizationId();

        // OrganizationSuperAdmin and OrganizationAdmin can only see their organization's events
        if (currentUserRole == "OrganizationSuperAdmin" || currentUserRole == "OrganizationAdmin")
        {
            query = organizationId.HasValue
                ? query.Where(x => x.OrganizationId == organizationId.Value)
                : query.Where(x => false); // No organization - return empty
        }
        // SuperAdmin, Admin, and User can see all events (no filtering)

        // Search filters
        if (!string.IsNullOrWhiteSpace(search?.FTS))
        {
            query = query.Where(x => 
                x.Title.Contains(search.FTS) || 
                x.Description.Contains(search.FTS) ||
                x.Location.Contains(search.FTS));
        }

        if (search?.OrganizationId.HasValue == true)
        {
            query = query.Where(x => x.OrganizationId == search.OrganizationId.Value);
        }

        if (search?.StartDate.HasValue == true)
        {
            query = query.Where(x => x.EventDateTime >= search.StartDate.Value);
        }

        if (search?.EndDate.HasValue == true)
        {
            query = query.Where(x => x.EventDateTime <= search.EndDate.Value);
        }

        if (search?.IsActive.HasValue == true)
        {
            query = query.Where(x => x.IsActive == search.IsActive.Value);
        }

        if (search?.CategoryIds != null && search.CategoryIds.Any())
        {
            query = query.Where(x => x.CategoryId.HasValue && search.CategoryIds.Contains(x.CategoryId.Value));
        }

        // Include related entities
        query = query.Include(x => x.Organization)
                     .Include(x => x.Images);

        // Order by event date descending
        query = query.OrderByDescending(x => x.EventDateTime);

        return query;
    }

    public override async Task<EventResponse?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var query = Context.Set<Event>()
            .Include(x => x.Organization)
            .Include(x => x.Images)
            .Where(x => x.Id == id);

        // Apply authorization filtering
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
                throw new UnauthorizedAccessException("Nemate pristup događajima - niste pridruženi organizaciji");
            }
        }

        var eventEntity = await query.FirstOrDefaultAsync(cancellationToken);

        if (eventEntity == null)
            return null;

        return MapToResponse(eventEntity);
    }

    public override async Task<EventResponse> UpdateAsync(int id, EventUpdateRequest request, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await base.UpdateAsync(id, request, cancellationToken);
            // Success: base class has called AfterUpdateAsync which clears both dictionaries
            return result;
        }
        catch
        {
            // On failure during BeforeUpdateAsync or SaveChangesAsync:
            // - Clean up newly uploaded blobs (not yet committed to DB)
            // - Clear old blobs queue (DB wasn't updated, so don't delete them)
            
            _blobsToDeleteAfterCommit.Remove(id);
            
            if (_newlyUploadedBlobs.Remove(id, out var newBlobs))
            {
                foreach (var blobUrl in newBlobs)
                {
                    try
                    {
                        await _blobStorageService.DeleteAsync(blobUrl, EventImagesContainer);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete newly uploaded blob after database failure: {BlobUrl}. Blob may be orphaned.", blobUrl);
                    }
                }
            }
            throw;
        }
    }

    protected override async Task BeforeCreateAsync(Event entity, EventInsertRequest request, 
        CancellationToken cancellationToken)
    {
        var currentUserRole = _jwtHelper.GetUserRole();
        
        // Only OrganizationSuperAdmin and OrganizationAdmin can create events
        if (currentUserRole != "OrganizationSuperAdmin" && currentUserRole != "OrganizationAdmin")
        {
            throw new UnauthorizedAccessException("Samo OrganizationSuperAdmin i OrganizationAdmin mogu kreirati događaje");
        }

        // Verify user belongs to the organization they're creating event for
        var userOrganizationId = _jwtHelper.GetOrganizationId();
        if (!userOrganizationId.HasValue || userOrganizationId.Value != request.OrganizationId)
        {
            throw new UnauthorizedAccessException("Možete kreirati događaje samo za svoju organizaciju");
        }

        // Verify organization exists
        var organizationExists = await Context.Set<Organization>()
            .AnyAsync(x => x.Id == request.OrganizationId, cancellationToken);

        if (!organizationExists)
        {
            throw new KeyNotFoundException($"Organizacija sa ID-om {request.OrganizationId} nije pronađena");
        }

        // Upload images if provided
        if (request.Images != null && request.Images.Any())
        {
            var imageEntities = new List<EventImage>();
            var uploadedImageUrls = new List<string>();
            bool isFirst = true;

            try
            {
                foreach (var image in request.Images)
                {
                    var imageUrl = await _blobStorageService.UploadAsync(image, EventImagesContainer);
                    uploadedImageUrls.Add(imageUrl);
                    imageEntities.Add(new EventImage
                    {
                        ImageUrl = imageUrl,
                        IsPrimary = isFirst,
                        Event = entity
                    });
                    isFirst = false;
                }

                entity.Images = imageEntities;
            }
            catch
            {
                // Rollback: Delete any uploaded blobs if there was an error
                foreach (var uploadedUrl in uploadedImageUrls)
                {
                    try
                    {
                        await _blobStorageService.DeleteAsync(uploadedUrl, EventImagesContainer);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete newly uploaded blob during rollback: {BlobUrl}. Blob may be orphaned.", uploadedUrl);
                    }
                }
                throw;
            }
        }
    }

    protected override async Task BeforeUpdateAsync(Event entity, EventUpdateRequest request, 
        CancellationToken cancellationToken)
    {
        var currentUserRole = _jwtHelper.GetUserRole();
        
        // Only OrganizationSuperAdmin and OrganizationAdmin can update events
        if (currentUserRole != "OrganizationSuperAdmin" && currentUserRole != "OrganizationAdmin")
        {
            throw new UnauthorizedAccessException("Samo OrganizationSuperAdmin i OrganizationAdmin mogu uređivati događaje");
        }

        // Verify user belongs to the organization that owns the event
        var userOrganizationId = _jwtHelper.GetOrganizationId();
        if (!userOrganizationId.HasValue || userOrganizationId.Value != entity.OrganizationId)
        {
            throw new UnauthorizedAccessException("Možete uređivati samo događaje svoje organizacije");
        }

        // Note: OrganizationId cannot be changed as it's not included in EventUpdateRequest
        // This maintains data consistency with associated EventTickets which have denormalized OrganizationId

        // Load existing images
        await Context.Entry(entity)
            .Collection(e => e.Images)
            .LoadAsync(cancellationToken);

        // Collect image URLs to delete (will be deleted after successful DB commit)
        var imageUrlsToDelete = new List<string>();
        
        // Mark specified images for deletion
        if (request.ImageIdsToDelete != null && request.ImageIdsToDelete.Any())
        {
            var imagesToDelete = entity.Images
                .Where(img => request.ImageIdsToDelete.Contains(img.Id))
                .ToList();

            foreach (var image in imagesToDelete)
            {
                imageUrlsToDelete.Add(image.ImageUrl);
                entity.Images.Remove(image);
            }
        }
        
        // Store URLs for deletion after commit (per-entity)
        if (imageUrlsToDelete.Any())
        {
            _blobsToDeleteAfterCommit[entity.Id] = imageUrlsToDelete;
        }

        // Add new images with rollback capability
        var uploadedImageUrls = new List<string>();
        if (request.NewImages != null && request.NewImages.Any())
        {
            try
            {
                bool hasPrimaryImage = entity.Images.Any(img => img.IsPrimary);

                foreach (var image in request.NewImages)
                {
                    var imageUrl = await _blobStorageService.UploadAsync(image, EventImagesContainer);
                    uploadedImageUrls.Add(imageUrl);
                    entity.Images.Add(new EventImage
                    {
                        ImageUrl = imageUrl,
                        IsPrimary = !hasPrimaryImage,
                        EventId = entity.Id
                    });
                    hasPrimaryImage = true;
                }
                
                // Store newly uploaded blobs for cleanup if database save fails
                if (uploadedImageUrls.Any())
                {
                    _newlyUploadedBlobs[entity.Id] = uploadedImageUrls;
                }
            }
            catch
            {
                // Rollback: Delete any uploaded blobs if there was an error
                foreach (var uploadedUrl in uploadedImageUrls)
                {
                    try
                    {
                        await _blobStorageService.DeleteAsync(uploadedUrl, EventImagesContainer);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete newly uploaded blob during rollback: {BlobUrl}. Blob may be orphaned.", uploadedUrl);
                    }
                }
                throw;
            }
        }

        // Ensure there's always a primary image if images exist
        if (entity.Images.Any() && !entity.Images.Any(img => img.IsPrimary))
        {
            entity.Images.First().IsPrimary = true;
        }
    }

    protected override async Task AfterUpdateAsync(Event entity, EventUpdateRequest request, 
        CancellationToken cancellationToken)
    {
        // Delete old blobs after successful DB commit
        if (_blobsToDeleteAfterCommit.Remove(entity.Id, out var imageUrlsToDelete))
        {
            foreach (var blobUrl in imageUrlsToDelete)
            {
                try
                {
                    await _blobStorageService.DeleteAsync(blobUrl, EventImagesContainer);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to delete blob {BlobUrl} for event {EventId}. Blob may be orphaned.", 
                        blobUrl, entity.Id);
                    // Continue with other deletions - consider implementing retry queue
                }
            }
        }
        
        // Clear newly uploaded blobs tracker since commit was successful
        _newlyUploadedBlobs.Remove(entity.Id);
    }

    public override async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await Context.Set<Event>()
            .Include(x => x.Images)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        
        if (entity == null)
            return false;

        var currentUserRole = _jwtHelper.GetUserRole();
        
        // Only OrganizationSuperAdmin and OrganizationAdmin can delete events
        if (currentUserRole != "OrganizationSuperAdmin" && currentUserRole != "OrganizationAdmin")
        {
            throw new UnauthorizedAccessException("Samo OrganizationSuperAdmin i OrganizationAdmin mogu brisati događaje");
        }

        // Verify user belongs to the organization that owns the event
        var userOrganizationId = _jwtHelper.GetOrganizationId();
        if (!userOrganizationId.HasValue || userOrganizationId.Value != entity.OrganizationId)
        {
            throw new UnauthorizedAccessException("Možete brisati samo događaje svoje organizacije");
        }

        // Collect image URLs to delete after DB commit
        var imageUrlsToDelete = entity.Images.Select(img => img.ImageUrl).ToList();

        // Remove entity from database first
        Context.Set<Event>().Remove(entity);
        await Context.SaveChangesAsync(cancellationToken);
        
        // Delete blobs after successful DB commit
        foreach (var imageUrl in imageUrlsToDelete)
        {
            try
            {
                await _blobStorageService.DeleteAsync(imageUrl, EventImagesContainer);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete blob {BlobUrl} for deleted event {EventId}. Blob may be orphaned.", 
                    imageUrl, id);
                // Continue with other deletions - consider implementing retry queue
            }
        }
        
        return true;
    }

    protected override EventResponse MapToResponse(Event entity)
    {
        var response = Mapper.Map<EventResponse>(entity);
        
        if (entity.Organization != null)
        {
            response.OrganizationName = entity.Organization.Name;
        }

        if (entity.Images != null && entity.Images.Any())
        {
            response.Images = entity.Images
                .OrderByDescending(img => img.IsPrimary)
                .Select(img => new EventImageResponse
                {
                    Id = img.Id,
                    ImageUrl = img.ImageUrl,
                    IsPrimary = img.IsPrimary
                })
                .ToList();
        }

        return response;
    }
}
