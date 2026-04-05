using AutoMapper;
using eTicketing.Model.Enums;
using eTicketing.Model.Responses;
using eTicketing.Model.SearchObjects;
using eTicketing.Services.Database;
using eTicketing.Services.Database.Entities;
using eTicketing.Services.Helpers;
using eTicketing.Services.Interfaces;
using eTicketing.Services.Services.Shared;
using Microsoft.EntityFrameworkCore;

namespace eTicketing.Services.Services;

public class AdminUserService : BaseService<User, UserResponse, AdminUserSearchObject>, IAdminUserService
{
    private readonly JwtHelper _jwtHelper;

    public AdminUserService(
        eTicketingDbContext context,
        IMapper mapper,
        JwtHelper jwtHelper) : base(context, mapper)
    {
        _jwtHelper = jwtHelper;
    }

    protected override IQueryable<User> ApplyFilter(IQueryable<User> query, AdminUserSearchObject? search)
    {
        // Only return users with Admin or SuperAdmin roles
        query = query.Where(u => u.RoleId == (int)RoleType.SuperAdmin || 
                                  u.RoleId == (int)RoleType.Admin);

        // Include Role for RoleName mapping
        query = query.Include(u => u.Role);

        if (!string.IsNullOrWhiteSpace(search?.FTS))
        {
            query = query.Where(u =>
                u.FirstName.Contains(search.FTS) ||
                u.LastName.Contains(search.FTS) ||
                u.Username.Contains(search.FTS) ||
                u.Email.Contains(search.FTS));
        }

        if (search?.IsActive.HasValue == true)
        {
            query = query.Where(u => u.IsActive == search.IsActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(search?.RoleName))
        {
            query = query.Where(u => u.Role.Name == search.RoleName);
        }

        query = query.OrderBy(u => u.LastName).ThenBy(u => u.FirstName);

        return query;
    }

    public override async Task<UserResponse?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var user = await Context.Set<User>()
            .Include(u => u.Role)
            .Where(u => u.RoleId == (int)RoleType.SuperAdmin || u.RoleId == (int)RoleType.Admin)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

        if (user == null)
            return null;

        return Mapper.Map<UserResponse>(user);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var user = await Context.Set<User>()
            .FirstOrDefaultAsync(u => u.Id == id && 
                (u.RoleId == (int)RoleType.SuperAdmin || u.RoleId == (int)RoleType.Admin), 
                cancellationToken);

        if (user == null)
            return false;

        // Prevent deleting yourself
        var currentUserId = _jwtHelper.GetUserId();
        if (currentUserId == id)
        {
            throw new InvalidOperationException("CannotDeleteOwnAccount");
        }

        // Soft delete: deactivate the user
        user.IsActive = false;
        user.UpdatedAt = DateTime.UtcNow;
        await Context.SaveChangesAsync(cancellationToken);

        return true;
    }
}
