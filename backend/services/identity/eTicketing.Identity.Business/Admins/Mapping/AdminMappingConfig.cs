using eTicketing.Identity.Data.Entities;
using eTicketing.Identity.Data.Enums;
using Mapster;

namespace eTicketing.Identity.Business.Admins.Mapping;

/// <summary>
/// Dedicated Mapster config for <see cref="CreateAdminRequest"/> -&gt; <see cref="User"/>.
/// PasswordHash/PasswordSalt are deliberately <see cref="TypeAdapterSetter{TSource,TDestination}.Ignore"/>d
/// — they're computed from the plaintext password via PasswordHasher.Hash in AdminService,
/// never through a mapping rule, so a hash never accidentally leaks into an Adapt call.
/// </summary>
public class AdminMappingConfig : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<CreateAdminRequest, User>()
            .Map(dest => dest.Role, src => RoleType.Admin)
            .Map(dest => dest.IsEmailVerified, src => true)
            .Ignore(dest => dest.PasswordHash)
            .Ignore(dest => dest.PasswordSalt);
    }
}
