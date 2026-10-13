using System.Data.Common;
using Microsoft.Extensions.Configuration;

namespace eTicketing.Contracts.Persistence;

/// <summary>
/// Raises the SQL connection pool from ADO.NET's default of 100 to <see cref="DefaultMaxPoolSize"/>
/// (or <c>Database:MaxPoolSize</c>) for every service's connection string, in one place, so the
/// docker-compose strings and the Azure ones cannot drift apart.
///
/// <para>Under load-test traffic both Catalog and Identity ran out of pooled connections
/// ("Timeout expired... prior to obtaining a connection from the pool", 3,000+ times each). A bigger
/// pool is only half the fix — on its own it moves the queue into SQL Server — which is why it ships
/// together with <see cref="Hosting.LoadSheddingMiddleware"/>, whose default concurrency matches it.</para>
///
/// <para>A value written explicitly into the connection string wins: whoever put it there meant it.</para>
/// </summary>
public static class SqlConnectionPooling
{
    public const int DefaultMaxPoolSize = 200;

    public static string? WithPlatformPoolSize(string? connectionString, IConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return connectionString;

        var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };
        if (builder.ContainsKey("Max Pool Size") || builder.ContainsKey("MaxPoolSize"))
            return connectionString;

        builder["Max Pool Size"] = configuration.GetValue("Database:MaxPoolSize", DefaultMaxPoolSize);
        return builder.ConnectionString;
    }
}
