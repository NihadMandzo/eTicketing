using eTicketing.Contracts.Messaging;
using eTicketing.Contracts.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace eTicketing.Shared.Messaging.Tests.TestFixtures;

/// <summary>Stands in for whatever domain row a service is writing when it publishes. Exists only
/// so a test can assert that the message and the data commit together.</summary>
public class Widget : BaseEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
