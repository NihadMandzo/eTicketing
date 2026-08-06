namespace eTicketing.Contracts.Persistence;

/// <summary>
/// Base class for every entity that participates in the global audit-timestamp mechanism
/// (see <see cref="AuditableEntitySaveChangesInterceptor"/>).
///
/// Intentionally has NO <c>Id</c> property — each entity declares its own primary key,
/// typed per its exposure needs (e.g. <c>Guid</c> for entities whose id is returned to
/// clients or used in URLs, <c>int</c> for purely internal entities that are never
/// serialized). Do not add one here.
///
/// Also intentionally has NO <c>IsDeleted</c>/<c>DeletedAt</c> — nothing in this system is
/// soft-deletable. Deletes are always hard deletes via <see cref="Repository{T,TKey}.Remove"/>.
/// Do not add a soft-delete flag or a global query filter anywhere.
/// </summary>
public abstract class BaseEntity
{
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
