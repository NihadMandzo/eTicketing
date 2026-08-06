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
        // defined once here instead of being hand-rolled at every call site.
        config.NewConfig<User, UserResponse>()
            .Map(dest => dest.RoleName, src => src.Role.ToString());

        // Self-service registration — always a plain User (RoleType.User), unlike the
        // Admin/Organization creation flows. PasswordHash/PasswordSalt are computed separately
        // via PasswordHasher.Hash in AuthService, never through a mapping rule.
        config.NewConfig<RegisterRequest, User>()
            .Map(dest => dest.Role, src => RoleType.User)
            .Ignore(dest => dest.PasswordHash)
            .Ignore(dest => dest.PasswordSalt);
    }
}
