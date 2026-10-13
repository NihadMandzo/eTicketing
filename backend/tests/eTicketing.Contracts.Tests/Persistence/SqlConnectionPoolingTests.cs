using System.Data.Common;
using eTicketing.Contracts.Persistence;
using FluentAssertions;
using Microsoft.Extensions.Configuration;

namespace eTicketing.Contracts.Tests.Persistence;

public class SqlConnectionPoolingTests
{
    private const string Base = "Server=sqlserver,1433;Database=CatalogDb;User Id=sa;Password=P4ss;TrustServerCertificate=True";

    private static readonly IConfiguration NoConfig = new ConfigurationBuilder().Build();

    [Fact]
    public void WithPlatformPoolSize_WithoutAPoolSize_AddsTheDefault()
    {
        var result = SqlConnectionPooling.WithPlatformPoolSize(Base, NoConfig);

        Parse(result)["Max Pool Size"].Should().Be("200");
    }

    [Theory]
    [InlineData("Max Pool Size=50")]
    [InlineData("MaxPoolSize=50")]
    [InlineData("max pool size=50")]
    public void WithPlatformPoolSize_WithAnExplicitPoolSize_LeavesTheStringUntouched(string explicitSize)
    {
        var connectionString = $"{Base};{explicitSize}";

        SqlConnectionPooling.WithPlatformPoolSize(connectionString, NoConfig).Should().Be(connectionString);
    }

    [Fact]
    public void WithPlatformPoolSize_WithAConfiguredSize_UsesIt()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Database:MaxPoolSize"] = "300" })
            .Build();

        var result = SqlConnectionPooling.WithPlatformPoolSize(Base, config);

        Parse(result)["Max Pool Size"].Should().Be("300");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void WithPlatformPoolSize_WithNoConnectionString_ReturnsItAsIs(string? connectionString)
    {
        SqlConnectionPooling.WithPlatformPoolSize(connectionString, NoConfig).Should().Be(connectionString);
    }

    [Fact]
    public void WithPlatformPoolSize_KeepsEveryOtherSettingIntact_IncludingAQuotedPassword()
    {
        const string withQuotedPassword = "Server=sqlserver,1433;Database=IdentityDb;User Id=sa;Password='a;b=c';TrustServerCertificate=True";

        var result = Parse(SqlConnectionPooling.WithPlatformPoolSize(withQuotedPassword, NoConfig));

        result["Password"].Should().Be("a;b=c");
        result["Server"].Should().Be("sqlserver,1433");
        result["Database"].Should().Be("IdentityDb");
        result["TrustServerCertificate"].Should().Be("True");
    }

    private static DbConnectionStringBuilder Parse(string? connectionString) => new() { ConnectionString = connectionString };
}
