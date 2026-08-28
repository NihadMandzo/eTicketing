using eTicketing.Contracts.Persistence;

namespace eTicketing.Catalog.Business.Tests.TestFixtures;

/// <summary>
/// Lets a test win a race against the code under test. It runs <c>raceOnce</c> immediately before
/// the FIRST SaveChangesAsync and then delegates — so a competing row lands in the database in the
/// window between the code's read and its write, which is exactly the interleaving that makes
/// UserInteractionRepository.UpsertAsync's insert fail against the unique index.
///
/// Deterministic on purpose: firing two real requests at once and hoping they overlap would be a
/// flaky test of the same thing.
/// </summary>
public sealed class RacingUnitOfWork : IUnitOfWork
{
    private readonly IUnitOfWork _inner;
    private readonly Func<Task> _raceOnce;
    private bool _raced;

    public RacingUnitOfWork(IUnitOfWork inner, Func<Task> raceOnce)
    {
        _inner = inner;
        _raceOnce = raceOnce;
    }

    /// <summary>How many times the code under test tried to commit — 2 means it hit the violation
    /// and recovered rather than giving up.</summary>
    public int SaveAttempts { get; private set; }

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        SaveAttempts++;

        if (!_raced)
        {
            _raced = true;
            await _raceOnce();
        }

        return await _inner.SaveChangesAsync(ct);
    }
}
