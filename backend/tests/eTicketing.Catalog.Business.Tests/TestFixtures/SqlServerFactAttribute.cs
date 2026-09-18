namespace eTicketing.Catalog.Business.Tests.TestFixtures;

/// <summary>
/// A <see cref="FactAttribute"/> that skips itself unless a real SQL Server is on offer, named by the
/// <c>ETICKETING_TEST_SQLSERVER</c> environment variable (a connection string to a server the test may
/// create and drop databases on — the docker-compose instance does).
///
/// <para>Everything else in this project runs on Sqlite in memory, per .claude/rules/10-backend.md,
/// and that is the right default: it is fast, needs nothing installed, and the behaviour under test
/// is almost always the model's rather than the engine's. The exception is a transaction that has to
/// survive a caught constraint violation, which is engine-specific — so those tests, and only those,
/// ask for the engine the platform actually runs on rather than a stand-in.</para>
/// </summary>
public sealed class SqlServerFactAttribute : FactAttribute
{
    public const string ConnectionStringVariable = "ETICKETING_TEST_SQLSERVER";

    public SqlServerFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
            Skip = $"Postavi {ConnectionStringVariable} da bi se pokrenuo test protiv pravog SQL Servera.";
    }

    public static string? ConnectionString => Environment.GetEnvironmentVariable(ConnectionStringVariable);
}
