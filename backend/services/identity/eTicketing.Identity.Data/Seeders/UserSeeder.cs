using eTicketing.Identity.Data.Entities;
using eTicketing.Identity.Data.Enums;

namespace eTicketing.Identity.Data.Seeders;

/// <summary>
/// Dedicated seed-data file for User, referenced by UserConfiguration's HasData(). Gives a fresh
/// deployment a way to log in at all (previously there was no bootstrap SuperAdmin whatsoever)
/// plus organizer accounts for the two seeded organizations (OrganizationSeeder.cs).
///
/// PasswordHash/PasswordSalt below are NOT computed at model-build time — HasData() seed values
/// must be stable across every startup (EF diffs them into the migration snapshot), but
/// PasswordHasher.Hash() generates a fresh random salt every call, which would make the model
/// look "changed" on every single run. Each hash/salt pair here was generated once with the
/// exact same eTicketing.Identity.Business.Security.PasswordHasher algorithm (PBKDF2-SHA256,
/// 100_000 iterations) and round-trip verified against PasswordHasher.Verify() before being
/// hardcoded — the passwords documented per-user below are real and log in correctly.
///
/// Local dev credentials only (same status as the root .env's DevLocal_P4ssw0rd! — never meant
/// for a real deployment): superadmin/SuperAdmin123!, everyone else/OrgAdmin123!.
/// </summary>
public static class UserSeeder
{
    private static readonly DateTime SeedTimestamp = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    // Must match eTicketing.Identity.Data.Seeders.OrganizationSeeder's fixed Ids.
    private static readonly Guid SarajevoEventsId = Guid.Parse("a1a1a1a1-0000-0000-0000-000000000001");
    private static readonly Guid MostarSportArenaId = Guid.Parse("a1a1a1a1-0000-0000-0000-000000000002");

    public static User[] GetSeedData() =>
    [
        new User
        {
            Id = Guid.Parse("b2b2b2b2-0000-0000-0000-000000000001"),
            FirstName = "Amina",
            LastName = "Hodžić",
            Email = "superadmin@eticketing.local",
            Username = "superadmin",
            PasswordHash = "Vo2N5mMKKV5riRxNcu+xYv2AxWVVkmJLATTjr04SU3I=",
            PasswordSalt = "AdVMNBExbubfvQWaHVw5Vg==", // password: SuperAdmin123!
            Role = RoleType.SuperAdmin,
            OrganizationId = null,
            IsActive = true,
            IsEmailVerified = true,
            IsFirstLogin = false,
            CreatedAt = SeedTimestamp,
            UpdatedAt = SeedTimestamp,
        },
        new User
        {
            Id = Guid.Parse("b2b2b2b2-0000-0000-0000-000000000002"),
            FirstName = "Emir",
            LastName = "Kovačević",
            Email = "emir@sarajevo-events.ba",
            Username = "emir.kovacevic",
            PasswordHash = "A6rZYgmOEHLfpoLnHEiPWOFfkAH3yQUaWv1PqvOzC18=",
            PasswordSalt = "ucX6FvDXRYs6cWtGnpOc9Q==", // password: OrgAdmin123!
            Role = RoleType.OrganizationSuperAdmin,
            OrganizationId = SarajevoEventsId,
            IsActive = true,
            IsEmailVerified = true,
            IsFirstLogin = false,
            CreatedAt = SeedTimestamp,
            UpdatedAt = SeedTimestamp,
        },
        new User
        {
            Id = Guid.Parse("b2b2b2b2-0000-0000-0000-000000000003"),
            FirstName = "Lejla",
            LastName = "Begić",
            Email = "lejla@sarajevo-events.ba",
            Username = "lejla.begic",
            PasswordHash = "vU//j3TsZkELntInV9wUpCwUYVJmcjrLGGEXgLPNZwE=",
            PasswordSalt = "iObaAcHaXVq51zJOd1k4eA==", // password: OrgAdmin123!
            Role = RoleType.OrganizationAdmin,
            OrganizationId = SarajevoEventsId,
            IsActive = true,
            IsEmailVerified = true,
            IsFirstLogin = false,
            CreatedAt = SeedTimestamp,
            UpdatedAt = SeedTimestamp,
        },
        new User
        {
            Id = Guid.Parse("b2b2b2b2-0000-0000-0000-000000000004"),
            FirstName = "Ivan",
            LastName = "Marić",
            Email = "ivan@mostar-sport.ba",
            Username = "ivan.maric",
            PasswordHash = "ryZEw8BVHyXvX87Bj+QeMKsmzx8PRSNnufi76DdKgu0=",
            PasswordSalt = "G5uos2iJ7Kw0wnkMhLwivQ==", // password: OrgAdmin123!
            Role = RoleType.OrganizationSuperAdmin,
            OrganizationId = MostarSportArenaId,
            IsActive = true,
            IsEmailVerified = true,
            IsFirstLogin = false,
            CreatedAt = SeedTimestamp,
            UpdatedAt = SeedTimestamp,
        },
    ];
}
