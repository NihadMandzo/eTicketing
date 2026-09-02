namespace eTicketing.Ticketing.Business.Analytics;

/// <summary>
/// Serializes every ML.NET fit in this service.
///
/// <para><b>Why, given each call already builds its own MLContext.</b> A fresh <c>MLContext</c> per
/// call is the documented way to isolate ML.NET work, and measurement showed it is not sufficient
/// for repeatability: the SSA paths returned different results for byte-identical input depending on
/// whether other ML.NET work was running at the same time. Whether that is a data race through
/// shared native state or ML.NET varying its own internal parallelism with machine load was not
/// isolated — both are consistent with what was observed, and the fix is the same either way.
/// Catalog's recommender serializes its training for the closely related documented reason (see
/// MatrixFactorizationModel on concurrent training through one MLContext), so this is the existing
/// precedent applied to fits that happen per request rather than per night.</para>
///
/// <para><b>What this does not buy.</b> It narrows the variance; it does not make ML.NET's numerics
/// exactly reproducible, and nothing here should be written as if it did. The guarantee that an
/// obviously unusual day is always reported comes from SsaAnomalyDetector running a deterministic
/// robust check alongside SSA and taking the union — not from this lock. See that class.</para>
///
/// <para><b>Why the cost is acceptable.</b> Everything held here is milliseconds — SSA over at most
/// 366 points, K-Means over a few thousand buyers — and AnalyticsService caches the whole response
/// for minutes in front of it, so realistic contention is a handful of back-office users. Nothing on
/// the purchase critical path touches this.</para>
///
/// <para>Deliberately a plain <see cref="Lock"/> rather than a semaphore: nothing inside awaits, and
/// keeping it un-awaitable is what stops anyone later holding it across an I/O call.</para>
/// </summary>
internal static class MlGate
{
    private static readonly Lock Gate = new();

    public static T Run<T>(Func<T> fit)
    {
        lock (Gate)
        {
            return fit();
        }
    }
}
