namespace eTicketing.Notifications.Email.Templates;

public sealed record ProductChangedData(string ProductName, IReadOnlyList<ProductChangeLine> Changes);
