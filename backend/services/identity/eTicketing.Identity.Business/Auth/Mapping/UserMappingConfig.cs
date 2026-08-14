using eTicketing.Identity.Data.Entities;
using eTicketing.Identity.Data.Enums;
using Mapster;

namespace eTicketing.Identity.Business.Auth.Mapping;

/// <summary>
/// Dedicated Mapster config for the <see cref="User"/> entity's request/response mappings.
/// Discovered and registered into <c>TypeAdapterConfig.GlobalSettings</c> as soon as the
/// assembly loads (see MapsterRegistration.cs), no explicit call needed at startup.
/// </summary>
public class UserMappingConfig : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        // The one response shape shared by Auth, Admins and Organizations, so the mapping is
        // defined once here instead of being hand-rolled at every call site. OrganizationName is
        // only ever populated when the query that loaded this User also `.Include(u =>
        // u.Organization)`d it — callers that don't need it (e.g. a bare role check) don't pay
        // for the join and simply get null back, same as OrganizationId already does for a User
        // with no org.
        config.NewConfig<User, UserResponse>()
            .Map(dest => dest.RoleName, src => src.Role.ToString())
            .Map(dest => dest.OrganizationName, src => src.Organization != null ? src.Organization.Name : null);

        // Self-service registration — always a plain User (RoleType.User), unlike the
        // Admin/Organization creation flows. PasswordHash/PasswordSalt are computed separately
        // via PasswordHasher.Hash in AuthService, never through a mapping rule.
        config.NewConfig<RegisterRequest, User>()
            .Map(dest => dest.Role, src => RoleType.User)
            .Ignore(dest => dest.PasswordHash)
            .Ignore(dest => dest.PasswordSalt);
    }
}
