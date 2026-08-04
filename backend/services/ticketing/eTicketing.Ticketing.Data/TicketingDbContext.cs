using eTicketing.Contracts.Persistence;
using Microsoft.EntityFrameworkCore;

namespace eTicketing.Ticketing.Data;

public class TicketingDbContext : DbContext, IUnitOfWork
{
    public TicketingDbContext(DbContextOptions<TicketingDbContext> options) : base(options) { }

    // DbSet<EventSector>, DbSet<Ticket> dolaze u Sprintu 2/3 — vidi SPRINTS/SPRINT_2.md, SPRINT_3.md

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TicketingDbContext).Assembly);
    }
}
