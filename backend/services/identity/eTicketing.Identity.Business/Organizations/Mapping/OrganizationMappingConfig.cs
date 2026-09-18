using eTicketing.Identity.Data.Entities;
using eTicketing.Identity.Data.Enums;
using eTicketing.Identity.Data.Repositories;
using Mapster;

namespace eTicketing.Identity.Business.Organizations.Mapping;

/// <summary>
/// Dedicated Mapster config for <see cref="Organization"/> -&gt; <see cref="OrganizationResponse"/>.
/// <c>UserCount</c> has no matching source property, so it's the one field that needs an
/// explicit mapping — it comes from the loaded <see cref="Organization.Users"/> collection
/// (repository methods that return an <see cref="Organization"/> for this mapping load Users
/// via Include specifically so this count is accurate; see OrganizationRepository).
/// </summary>
public class OrganizationMappingConfig : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        // LogoUrl has no matching source property (Organization stores LogoBlobName, not a URL)
        // — left at its default null here and filled in by OrganizationService via `with` when
        // a logo is present, same reasoning as UserCount.
        config.NewConfig<Organization, OrganizationResponse>()
            .Map(dest => dest.UserCount, src => src.Users.Count);

        // The list path counts users in SQL instead of loading them (see
        // OrganizationRepository.SearchAsync), so it maps from the projection rather than the
        // entity: Users is empty on these instances and reading Users.Count would report 0 for
        // every organization on the page.
        config.NewConfig<OrganizationWithUserCount, OrganizationResponse>()
            .Map(dest => dest, src => src.Organization)
            .Map(dest => dest.UserCount, src => src.UserCount);

        // LogoBlobName has no matching source on Create/UpdateOrganizationRequest (logos are
        // managed exclusively through the dedicated logo-upload endpoints) — Mapster leaves it
        // untouched on update and defaulted to null on create, no explicit Ignore() needed.
        config.NewConfig<CreateOrganizationRequest, Organization>();
        config.NewConfig<UpdateOrganizationRequest, Organization>();

        // The first organizer created alongside a new organization. Request fields are
        // "Admin"-prefixed (AdminFirstName, AdminEmail, ...) so they don't line up with User's
        // plain names by convention and need an explicit mapping. PasswordHash/PasswordSalt are
        // computed separately via PasswordHasher.Hash in OrganizationService, same reasoning as
        // AdminMappingConfig.
        config.NewConfig<CreateOrganizationRequest, User>()
            .Map(dest => dest.FirstName, src => src.AdminFirstName)
            .Map(dest => dest.LastName, src => src.AdminLastName)
            .Map(dest => dest.Email, src => src.AdminEmail)
            .Map(dest => dest.Username, src => src.AdminUsername)
            // Every organization must have exactly one OrganizationSuperAdmin, and the first
            // account created alongside a brand-new organization always is one — there is no
            // "which role should the first admin be" choice, unlike AddOrganizationUserRequest.
            .Map(dest => dest.Role, src => RoleType.OrganizationSuperAdmin)
            .Map(dest => dest.IsEmailVerified, src => true)
            .Ignore(dest => dest.PasswordHash)
            .Ignore(dest => dest.PasswordSalt);

        // A user added directly to an existing organization — plain names line up by
        // convention, only the verified-on-creation flag and the hashed password are special.
        config.NewConfig<AddOrganizationUserRequest, User>()
            .Map(dest => dest.IsEmailVerified, src => true)
            .Ignore(dest => dest.PasswordHash)
            .Ignore(dest => dest.PasswordSalt);
    }
}
