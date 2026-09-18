namespace eTicketing.Catalog.Data.Entities;

/// <summary>How a user touched a product. Persisted as the integer ordinal (the convention every
/// enum in this codebase follows — see TicketStatus) and mirrored by ordinal in the frontends'
/// enum tables. Append new values only; never reorder or remove.</summary>
public enum InteractionType
{
    View,
    Purchase
}
