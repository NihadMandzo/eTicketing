namespace eTicketing.Ticketing.Business.Analytics;

/// <summary>How an insight should read. Drives the card's colour and the ranking — a Critical
/// card is always above a Positive one, because the organizer opening this tab needs the problem
/// before the compliment.</summary>
public enum InsightSeverity
{
    Positive,
    Neutral,
    Warning,
    Critical
}
