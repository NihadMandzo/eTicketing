namespace eTicketing.Ticketing.Business.Analytics.Insights;

public interface IInsightGenerator
{
    IReadOnlyList<BusinessInsight> Generate(InsightContext context);
}
