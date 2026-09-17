using eTicketing.Contracts.Persistence;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace eTicketing.Contracts.Tests.Persistence;

/// <summary>A throwaway entity for exercising the interceptor without depending on any real domain model.</summary>
public class TestEntity : BaseEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
