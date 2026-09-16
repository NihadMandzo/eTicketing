namespace eTicketing.Ticketing.Business.Sectors;

public record HoldResult(bool Success, string? HoldId, DateTime? ExpiresAt);
