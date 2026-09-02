using eTicketing.Ticketing.Data.Repositories;

namespace eTicketing.Ticketing.Business.Analytics.Segmentation;

/// <summary>
/// Groups buyers into behavioural segments — the "korisničke aktivnosti" half of the AI Uvidi tab.
///
/// <paramref name="asOf"/> is the day recency is measured against, and it is the range's last day
/// rather than today: a report for last March must describe who those buyers were in March, not
/// mark all of them dormant because months have passed since.
/// </summary>
public interface IAudienceSegmenter
{
    SegmentBlock Segment(IReadOnlyList<BuyerFacts> buyers, DateOnly asOf, string windowLabel);
}
